using Amazon.SQS;
using Amazon.SQS.Model;
using Application.Interfaces.MessageBus;
using Microsoft.Extensions.Configuration;
using Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Infrastructure.MessageBus;

public class SqsCommandPublisher(
    IAmazonSQS _sqsClient,
    IConfiguration _configuration) : ICommandPublisher
{

    public async Task SendCommandAsync<T>(string commandType, T payload, string queueConfigKey)
    {
        var queueUrl = _configuration[$"AWS:{queueConfigKey}"];
        var commandWrapper = new SqsCommandWrapper<T>(commandType, payload);
        var messageRequest = new Amazon.SQS.Model.SendMessageRequest
        {
            QueueUrl = queueUrl,
            MessageBody = JsonSerializer.Serialize(commandWrapper)
        };
        await _sqsClient.SendMessageAsync(messageRequest);
    }
}