using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using TestService.Api.Configuration;
using TestService.Api.Models;

namespace TestService.Api.Services;

public interface IMessageBusService
{
    Task PublishAsync<T>(T message, string routingKey = "");
    void StartConsuming(Func<TestData, Task> messageHandler);
    void StopConsuming();
}

public class MessageBusService : IMessageBusService, IDisposable
{
    private readonly RabbitMqSettings _settings;
    private IConnection? _connection;
    private IChannel? _channel;
    private AsyncEventingBasicConsumer? _consumer;
    private string? _consumerTag;

    public MessageBusService(RabbitMqSettings settings)
    {
        _settings = settings;
    }

    private async Task EnsureConnectionAsync()
    {
        if (_connection == null || !_connection.IsOpen)
        {
            var factory = new ConnectionFactory
            {
                HostName = _settings.HostName,
                Port = _settings.Port,
                UserName = _settings.UserName,
                Password = _settings.Password
            };

            _connection = await factory.CreateConnectionAsync();
            _channel = await _connection.CreateChannelAsync();

            await _channel.ExchangeDeclareAsync(
                exchange: _settings.ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false);

            await _channel.QueueDeclareAsync(
                queue: _settings.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false);

            await _channel.QueueBindAsync(
                queue: _settings.QueueName,
                exchange: _settings.ExchangeName,
                routingKey: "testdata.*");
        }
    }

    public async Task PublishAsync<T>(T message, string routingKey = "")
    {
        await EnsureConnectionAsync();

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        
        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        await _channel!.BasicPublishAsync(
            exchange: _settings.ExchangeName,
            routingKey: string.IsNullOrEmpty(routingKey) ? "testdata.created" : routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body);
    }

    public void StartConsuming(Func<TestData, Task> messageHandler)
    {
        Task.Run(async () =>
        {
            await EnsureConnectionAsync();

            _consumer = new AsyncEventingBasicConsumer(_channel!);
            _consumer.ReceivedAsync += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var testData = JsonSerializer.Deserialize<TestData>(message);

                    if (testData != null)
                    {
                        await messageHandler(testData);
                    }

                    await _channel!.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing message: {ex.Message}");
                    await _channel!.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                }
            };

            _consumerTag = await _channel!.BasicConsumeAsync(
                queue: _settings.QueueName,
                autoAck: false,
                consumer: _consumer);
        });
    }

    public void StopConsuming()
    {
        if (!string.IsNullOrEmpty(_consumerTag) && _channel != null)
        {
            _channel.BasicCancelAsync(_consumerTag).GetAwaiter().GetResult();
        }
    }

    public void Dispose()
    {
        StopConsuming();
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
