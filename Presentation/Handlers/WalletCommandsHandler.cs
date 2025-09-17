using Application.Interfaces.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.DTOs.Commands;
using System.Text;
using System.Text.Json;

namespace Presentation.Handlers;

public class WalletCommandsHandler(IServiceScopeFactory _scopeFactory,IConnection _connection) : IHostedService, IDisposable
{
    private const string ExchangeName = "payment_exchange";
    private const string QueueName = "wallet_commands_queue";
    private IModel _channel;
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
                    var walletService = scope.ServiceProvider.GetRequiredService<IWalletApplicationService>();

                    if (routingKey == "wallet.command.deposit")
                    {
                        var command = JsonSerializer.Deserialize<CreateDepositCommand>(message);
                        if (command != null) await walletService.CreateDepositAsync(command);
                    }
                    else if (routingKey == "wallet.command.withdraw")
                    {
                        var command = JsonSerializer.Deserialize<CreateWithdrawalCommand>(message);
                        if (command != null) await walletService.CreateWithdrawalAsync(command);
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
        _connection?.Close();
    }

    private void StartModel()
    {
        _channel = _connection.CreateModel();

        _channel.ExchangeDeclare(exchange: ExchangeName, type: ExchangeType.Topic, durable: true);
        _channel.QueueDeclare(queue: QueueName, durable: true, exclusive: false, autoDelete: false);

        _channel.QueueBind(queue: QueueName, exchange: ExchangeName, routingKey: "wallet.command.deposit");
        _channel.QueueBind(queue: QueueName, exchange: ExchangeName, routingKey: "wallet.command.withdraw");
    }
}
