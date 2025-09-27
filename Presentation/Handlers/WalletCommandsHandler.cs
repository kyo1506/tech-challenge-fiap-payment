using Amazon.SQS;
using Amazon.SQS.Model;
using Application.Interfaces.Services;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.DTOs.Commands;
using Shared.DTOs.Responses;
using System.Text;
using System.Text.Json;

namespace Presentation.Handlers;

public class WalletCommandsHandler(
    IServiceScopeFactory _scopeFactory,
    ILogger<WalletCommandsHandler> _logger,
    IAmazonSQS _sqsClient,
    IConfiguration _configuration) : IHostedService, IDisposable
{
    private readonly string _queueUrl = _configuration["AWS:WalletCommandQueueUrl"];
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Wallet Commands Handler is starting.");
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
                _logger.LogError(ex, "Error polling Wallet SQS queue. Waiting 5 seconds before retry.");
                await Task.Delay(5000, cancellationToken);
            }
        }
    }

    private async Task ProcessMessageAsync(Message message, JsonSerializerOptions options)
    {
        var envelope = JsonSerializer.Deserialize<CommandEnvelope>(message.Body, options);
        var commandType = envelope?.CommandType;
        var payload = envelope?.Payload?.GetRawText();

        if (string.IsNullOrEmpty(commandType) || string.IsNullOrEmpty(payload))
        {
            _logger.LogWarning("Invalid message format received in Wallet queue. Deleting message.");
            return;
        }

        _logger.LogInformation("New wallet command received: {CommandType}", commandType);

        using var scope = _scopeFactory.CreateScope();
        var walletService = scope.ServiceProvider.GetRequiredService<IWalletApplicationService>();
        TransactionResponse? transactionResult = null;

        switch (commandType)
        {
            case "create-deposit":
                var depositCmd = JsonSerializer.Deserialize<CreateDepositCommand>(payload, options);
                if (depositCmd != null)
                    transactionResult = await walletService.CreateDepositAsync(depositCmd);
                break;
            case "create-withdraw":
                var withdrawCmd = JsonSerializer.Deserialize<CreateWithdrawalCommand>(payload, options);
                if (withdrawCmd != null)
                    transactionResult = await walletService.CreateWithdrawalAsync(withdrawCmd);
                break;
            default:
                _logger.LogWarning("Unsupported command type '{CommandType}' in Wallet queue. Message will be discarded.", commandType);
                break;
        }

        message.MessageAttributes.TryGetValue("ReplyTo", out var replyToAttr);
        message.MessageAttributes.TryGetValue("CorrelationId", out var correlationIdAttr);

        if (transactionResult != null && replyToAttr != null && correlationIdAttr != null)
        {
            await SendRpcReplyAsync(replyToAttr.StringValue, correlationIdAttr.StringValue, transactionResult);
        }
        else
        {
            _logger.LogInformation("Command {CommandType} processed without RPC reply (ReplyTo or CorrelationId attribute was missing).", commandType);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Wallet Commands Handler is stopping.");
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
