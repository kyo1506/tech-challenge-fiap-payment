using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Amazon.SQS.Model;
using Application.Interfaces.Event;
using Application.Interfaces.RabbitMQ;
using Application.Interfaces.Services;
using Application.Services;
using Domain.Exceptions;
using Domain.Interfaces.Repositories;
using Infrastructure.Data.EventSourcing;
using Infrastructure.Data.Repositories;
using Infrastructure.MessageBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using Serilog;
using Shared.DTOs.Commands;
using System;
using System.Text.Json;


[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace PaymentService.Processor;

public class Function
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<Function> _logger;
    private readonly IConfiguration _configuration;
    public Function()
    {
        var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
            _logger = _serviceProvider.GetRequiredService<ILogger<Function>>();
            _configuration = _serviceProvider.GetRequiredService<IConfiguration>();
    }


    public async Task FunctionHandler(SQSEvent evnt, ILambdaContext context)
    {
        try
        {
            _logger.LogInformation($"Processing {evnt.Records.Count} messages...");

            foreach (var message in evnt.Records)
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    try
                    {
                        await ProcessMessageAsync(message, scope.ServiceProvider);
                    }
                    catch (DomainException ex)
                    {
                        _logger.LogWarning(ex, "Business rule violation while processing message ID {MessageId}: {ErrorMessage}", message.MessageId, ex.Message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process message with ID: {MessageId}. Message will return to queue.", message.MessageId);
                        throw;
                    }
                }
            }
        }
        catch (Exception e)
        {

            throw;
        }
    }

    private async Task ProcessMessageAsync(
        SQSEvent.SQSMessage message,
        IServiceProvider serviceProvider)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        _logger.LogInformation("Processing message body: {MessageBody}", message.Body);

        try
        {
            var envelope = JsonSerializer.Deserialize<CommandEnvelope>(message.Body, options);
            var commandType = envelope?.CommandType;
            var payload = envelope?.Payload?.GetRawText();

            if (string.IsNullOrEmpty(commandType) || string.IsNullOrEmpty(payload))
            {
                _logger.LogWarning("Invalid message format. Discarding message.");
                return;
            }
            object? result = null;
            switch (commandType)
            {
                case "create-deposit":
                    var walletService = serviceProvider.GetRequiredService<IWalletApplicationService>();
                    var depositCmd = JsonSerializer.Deserialize<CreateDepositCommand>(payload);
                    if (depositCmd != null)
                        result = await walletService.CreateDepositAsync(depositCmd);
                    break;
                case "create-withdraw":
                    var walletServiceWithdraw = serviceProvider.GetRequiredService<IWalletApplicationService>();
                    var withdrawCmd = JsonSerializer.Deserialize<CreateWithdrawalCommand>(payload);
                    if (withdrawCmd != null)
                        result = await walletServiceWithdraw.CreateWithdrawalAsync(withdrawCmd);
                    break;
                case "create-purchase":
                    var purchaseService = serviceProvider.GetRequiredService<IPurchaseApplicationService>();
                    var purchaseCmd = JsonSerializer.Deserialize<CreatePurchaseCommand>(payload);
                    if (purchaseCmd != null)
                        result = await purchaseService.CreatePurchaseAsync(purchaseCmd);
                    break;
                case "create-refund":
                    var purchaseServiceRefund = serviceProvider.GetRequiredService<IPurchaseApplicationService>();
                    var refundCmd = JsonSerializer.Deserialize<CreateRefundCommand>(payload);
                    if (refundCmd != null)
                        result = await purchaseServiceRefund.CreateRefundAsync(refundCmd);
                    break;
                default:
                    _logger.LogWarning($"Unsupported command type '{commandType}'. Message will be discarded.");
                    break;
            }

            if (result != null)
            {
                var sqsClient = serviceProvider.GetRequiredService<IAmazonSQS>();
                var replyQueueName = _configuration["AWS:ReplyQueueName"];

                if (string.IsNullOrWhiteSpace(replyQueueName))
                {
                    _logger.LogWarning("ReplyQueueName is not configured. Skipping RPC reply.");
                    return;
                }
                var queueUrlResponse = await sqsClient.GetQueueUrlAsync(replyQueueName);

                await SendRpcReplyAsync(sqsClient, queueUrlResponse.QueueUrl, result);
            }
            else
            {
                _logger.LogInformation("Command {CommandType} processed without RPC reply (attributes missing or no result).", commandType);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error processing message: {ex.Message}");
            throw;
        }
    }

    private async Task SendRpcReplyAsync(IAmazonSQS sqsClient, string replyToQueue, object result)
    {
        _logger.LogWarning("Sending RPC reply to queue: {ReplyQueue}", replyToQueue);

        var responseMessage = JsonSerializer.Serialize(result);
        var sendRequest = new SendMessageRequest
        {
            QueueUrl = replyToQueue,
            MessageBody = responseMessage
        };
        await sqsClient.SendMessageAsync(sendRequest);
    }

    private void ConfigureServices(IServiceCollection services)
    {
        var configuration = new ConfigurationBuilder()
            //.SetBasePath(Directory.GetCurrentDirectory())
            //.AddJsonFile("appsettings.json", optional: false)
            //.AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        var logger = new LoggerConfiguration()
           .ReadFrom.Configuration(configuration)
           .CreateLogger();
        services.AddLogging(builder =>
        {
            //builder.AddLambdaLogger();
            builder.ClearProviders();
            builder.AddSerilog(logger, dispose: true);
        });

        services.AddDbContext<EventStoreDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IEventStoreUnitOfWork, EventStoreUnitOfWork>();

        services.AddDefaultAWSOptions(configuration.GetAWSOptions());
        services.AddAWSService<IAmazonSimpleNotificationService>();
        services.AddAWSService<IAmazonSQS>();

        services.AddSingleton<IMessageBusClient, SnsMessageBusClient>();
        services.AddScoped<IWalletRepository, EfWalletRepository>();
        services.AddScoped<IPurchaseRepository, EfPurchaseRepository>();
        services.AddScoped<IWalletApplicationService, WalletApplicationService>();
        services.AddScoped<IPurchaseApplicationService, PurchaseApplicationService>();
    }
    private class CommandEnvelope { 
        public string? CommandType { get; set; } public JsonElement? Payload { get; set; } 
    }

}