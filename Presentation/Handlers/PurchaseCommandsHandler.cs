using Amazon.SQS;
using Amazon.SQS.Model;
using Application.Interfaces.Services;
using Npgsql.Internal;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.DTOs.Commands;
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
                    WaitTimeSeconds = 20
                };
                var response = await _sqsClient.ReceiveMessageAsync(receiveRequest, cancellationToken);

                foreach (var message in response.Messages)
                {
                    await ProcessMessageAsync(message, serializerOptions);
                    await _sqsClient.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, cancellationToken);
                }
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

        switch (commandType)
        {
            case "create-purchase":
                var purchaseCmd = JsonSerializer.Deserialize<CreatePurchaseCommand>(payload, options);
                if (purchaseCmd != null) await purchaseService.CreatePurchaseAsync(purchaseCmd);
                break;
            case "create-refund":
                var refundCmd = JsonSerializer.Deserialize<CreateRefundCommand>(payload, options);
                if (refundCmd != null) await purchaseService.CreateRefundAsync(refundCmd);
                break;
            default:
                _logger.LogWarning("Unsupported command type '{CommandType}' in Purchase queue. Message will be discarded.", commandType);
                break;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Purchase Commands Handler is stopping.");
        return Task.CompletedTask;
    }

    public void Dispose() { }

    private class CommandEnvelope { public string CommandType { get; set; } public JsonElement? Payload { get; set; } }
}
