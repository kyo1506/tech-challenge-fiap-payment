using Amazon.SQS;
using Application.Interfaces.RabbitMQ;
using Amazon.SimpleNotificationService;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Infrastructure.MessageBus;

public class SnsMessageBusClient : IMessageBusClient
{
    private readonly IAmazonSimpleNotificationService _snsClient;
    private readonly IConfiguration _configuration;

    public SnsMessageBusClient(IAmazonSimpleNotificationService snsClient, IConfiguration configuration)
    {
        _snsClient = snsClient;
        _configuration = configuration;
    }

    public void Publish(object message, string routingKey)
    {
        throw new NotImplementedException();
    }

    public async Task PublishAsync(object message, string routingKey) 
    {
        var topicArn = _configuration["AWS:SnsTopicArn"];
        if (string.IsNullOrEmpty(topicArn))
        {
            throw new Exception("SNS Topic ARN not configured in appsettings.json");
        }

        var messageJson = JsonSerializer.Serialize(message);

        var publishRequest = new Amazon.SimpleNotificationService.Model.PublishRequest
        {
            TopicArn = topicArn,
            Message = messageJson,
        };

        await _snsClient.PublishAsync(publishRequest);
    }
}
