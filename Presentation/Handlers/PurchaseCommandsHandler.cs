using Application.Interfaces.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.DTOs.Commands;
using System.Text;
using System.Text.Json;

namespace Presentation.Handlers;

public class PurchaseCommandsHandler(IServiceScopeFactory _scopeFactory, IConnection _connection) : IHostedService, IDisposable
{
    private IModel _channel;
    private const string ExchangeName = "payment_exchange";
    private const string QueueName = "purchase_commands_queue";
    public Task StartAsync(CancellationToken cancellationToken)
    {
        StartModel(); 
        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            var routingKey = ea.RoutingKey;

            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var purchaseService = scope.ServiceProvider.GetRequiredService<IPurchaseApplicationService>();

                    if (routingKey == "purchase.command.create")
                    {
                        var command = JsonSerializer.Deserialize<CreatePurchaseCommand>(message);
                        if (command != null) await purchaseService.CreatePurchaseAsync(command);
                    }
                    else if (routingKey == "purchase.command.refund")
                    {
                        var command = JsonSerializer.Deserialize<CreateRefundCommand>(message);
                        if (command != null) await purchaseService.CreateRefundAsync(command);
                    }
                }

                _channel.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
            }
        };

        _channel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel?.Close();
    }

    private void StartModel()
    {
        _channel = _connection.CreateModel();

        _channel.ExchangeDeclare(exchange: ExchangeName, type: ExchangeType.Topic, durable: true);

        _channel.QueueDeclare(queue: QueueName, durable: true, exclusive: false, autoDelete: false);

        _channel.QueueBind(queue: QueueName, exchange: ExchangeName, routingKey: "purchase.command.create");
        _channel.QueueBind(queue: QueueName, exchange: ExchangeName, routingKey: "purchase.command.refund");

    }
}
