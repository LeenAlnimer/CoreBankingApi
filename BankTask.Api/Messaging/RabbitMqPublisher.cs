
using System.Text;
using System.Text.Json;
using BankTask.Application.Events;
using BankTask.Application.Interfaces.Services;
using RabbitMQ.Client;

namespace BankTask.Api.Messaging;

public class RabbitMqPublisher : IRabbitMqPublisher
{
    private readonly ConnectionFactory _connectionFactory;
    private readonly ILogger<RabbitMqPublisher> _logger;

    public RabbitMqPublisher(
        ConnectionFactory connectionFactory,
        ILogger<RabbitMqPublisher> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task PublishAsync<T>(
        string queueName,
        T message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(queueName))
        {
            throw new ArgumentException(
                "Queue name cannot be empty.",
                nameof(queueName));
        }

        await using var connection =
            await _connectionFactory.CreateConnectionAsync(
                cancellationToken);

        await using var channel =
            await connection.CreateChannelAsync(
                cancellationToken: cancellationToken);

        IDictionary<string, object?>? queueArguments = null;

        if (queueName == CacheInvalidationEvent.QueueName)
        {
            await channel.QueueDeclareAsync(
                queue: CacheInvalidationEvent.DeadLetterQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            queueArguments = new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = string.Empty,
                ["x-dead-letter-routing-key"] =
                    CacheInvalidationEvent.DeadLetterQueueName
            };
        }

        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArguments,
            cancellationToken: cancellationToken);

        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            body: body,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Published message to RabbitMQ queue {QueueName}",
            queueName);
    }
}
