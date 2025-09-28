using Amazon.SQS;
using Amazon.SQS.Model;
using Application.Interfaces.Services;
using Npgsql.Internal;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.DTOs.Commands;
using Shared.DTOs.Responses;
using System.Text;
using System.Text.Json;

namespace Presentation.Handlers;

public class PurchaseCommandsHandler(
    IServiceScopeFactory _scopeFactory, 
    ILogger<PurchaseCommandsHandler> _logger,
    IAmazonSQS _sqsClient,
    IConfiguration _configuration) : IHostedService, IDisposable
{
    private readonly string _queueUrl = _configuration["AWS:PurchaseCommandQueueUrl"];
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Purchase Commands Handler is starting.");
        Task.Run(() => PollQueueAsync(cancellationToken), cancellationToken);
        return Task.CompletedTask;
    }

    private async Task PollQueueAsync(CancellationToken cancellationToken)
    {
        var serializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var receiveRequest = new ReceiveMessageRequest
                {
                    QueueUrl = _queueUrl,
                    MaxNumberOfMessages = 10,
                    WaitTimeSeconds = 20,
                    MessageAttributeNames = new List<string> { "All" }
                };
                var response = await _sqsClient.ReceiveMessageAsync(receiveRequest, cancellationToken);

                foreach (var message in response.Messages)
                {
                    await ProcessMessageAsync(message, serializerOptions);
                    await _sqsClient.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, cancellationToken);
                }
            }
            catch (AmazonSQSException sqsEx)
            {
                _logger.LogError(sqsEx, "An AWS SQS error occurred. Error Code: {ErrorCode}, AWS Request ID: {RequestId}", sqsEx.ErrorCode, sqsEx.RequestId);
                await Task.Delay(5000, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling Purchase SQS queue. Waiting 5 seconds before retry.");
                await Task.Delay(5000, cancellationToken);
            }
        }
    }

    private async Task ProcessMessageAsync(Message message, JsonSerializerOptions options)
    {
        message.MessageAttributes.TryGetValue("ReplyTo", out var replyToAttr);
        message.MessageAttributes.TryGetValue("CorrelationId", out var correlationIdAttr);

        if (replyToAttr == null || correlationIdAttr == null)
        {
            throw new InvalidOperationException("Message is missing required RPC attributes (ReplyTo, CorrelationId).");
        }

        var envelope = JsonSerializer.Deserialize<CommandEnvelope>(message.Body, options);
        var commandType = envelope?.CommandType;
        var payload = envelope?.Payload?.GetRawText();

        if (string.IsNullOrEmpty(commandType) || string.IsNullOrEmpty(payload))
        {
            _logger.LogWarning("Invalid message format received in Purchase queue. Deleting message.");
            return;
        }

        _logger.LogInformation("New purchase command received: {CommandType}", commandType);

        using var scope = _scopeFactory.CreateScope();
        var purchaseService = scope.ServiceProvider.GetRequiredService<IPurchaseApplicationService>();
        Purchase purchaseResult = null;
        RefundResponse refundResult = null;
        switch (commandType)
        {
            case "create-purchase":
                var purchaseCmd = JsonSerializer.Deserialize<CreatePurchaseCommand>(payload, options);
                if (purchaseCmd != null)
                    purchaseResult = await purchaseService.CreatePurchaseAsync(purchaseCmd);
                break;
            case "create-refund":
                var refundCmd = JsonSerializer.Deserialize<CreateRefundCommand>(payload, options);
                if (refundCmd != null)
                    refundResult = await purchaseService.CreateRefundAsync(refundCmd);
                break;
            default:
                _logger.LogWarning("Unsupported command type '{CommandType}' in Purchase queue. Message will be discarded.", commandType);
                return;
        }

        if (purchaseResult != null)
        {
            var confirmation = new PurchaseConfirmationResponse
            {
                UserId = purchaseResult.UserId,
                PaymentTransactionId = purchaseResult.Id,
                Games = purchaseResult.Items.Select(item =>
                {
                    var originalCmd = JsonSerializer.Deserialize<CreatePurchaseCommand>(payload, options);
                    var originalItem = originalCmd.Games.FirstOrDefault(g => g.GameId == item.GameId);
                    return new PurchaseConfirmationItem
                    {
                        GameId = item.GameId,
                        Price = item.OriginalPrice,
                        Discount = item.DiscountPercentage,
                        PromotionId = originalCmd.Games.FirstOrDefault(g => g.GameId == item.GameId)?.PromotionId,
                        HistoryPaymentId = originalItem?.HistoryPaymentId ?? Guid.Empty
                    };
                }).ToList()
            };

            await SendRpcReplyAsync(replyToAttr.StringValue, correlationIdAttr.StringValue, confirmation);
        }
        else if (refundResult != null)
        {
            await SendRpcReplyAsync(replyToAttr.StringValue, correlationIdAttr.StringValue, refundResult);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Purchase Commands Handler is stopping.");
        return Task.CompletedTask;
    }

    public void Dispose() { }

    private class CommandEnvelope { public string CommandType { get; set; } public JsonElement? Payload { get; set; } }

    private async Task SendRpcReplyAsync(string replyToQueue, string correlationId, object responseData)
    {
        _logger.LogInformation("Sending RPC reply with CorrelationId {CorrelationId} to queue: {ReplyQueue}", correlationId, replyToQueue);

        var sendMessageRequest = new SendMessageRequest
        {
            QueueUrl = replyToQueue,
            MessageBody = JsonSerializer.Serialize(responseData),
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
        {
            { "CorrelationId", new MessageAttributeValue { StringValue = correlationId, DataType = "String" } }
        }
        };
        await _sqsClient.SendMessageAsync(sendMessageRequest);
    }
}
