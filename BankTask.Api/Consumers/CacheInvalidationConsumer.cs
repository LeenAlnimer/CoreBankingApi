
using System.Text.Json;
using BankTask.Api.Messaging;
using BankTask.Application.Caching;
using BankTask.Application.Events;
using BankTask.Application.Interfaces.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BankTask.Api.Consumers;

public class CacheInvalidationConsumer : BackgroundService
{
    private const int MaximumKeyCount = 100;

    private readonly RabbitMqConnectionManager _connectionManager;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<CacheInvalidationConsumer> _logger;

    public CacheInvalidationConsumer(
        RabbitMqConnectionManager connectionManager,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<CacheInvalidationConsumer> logger)
    {
        _connectionManager = connectionManager;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var connection =
            await _connectionManager.GetConnectionAsync(stoppingToken);

        await using var channel =
            await connection.CreateChannelAsync(
                cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(
            queue: CacheInvalidationEvent.DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);

        var queueArguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = string.Empty,
            ["x-dead-letter-routing-key"] =
                CacheInvalidationEvent.DeadLetterQueueName
        };

        await channel.QueueDeclareAsync(
            queue: CacheInvalidationEvent.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArguments,
            cancellationToken: stoppingToken);

        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            CacheInvalidationEvent? invalidationEvent = null;

            try
            {
                _logger.LogInformation(
                    "Received cache invalidation message from {QueueName}.",
                    CacheInvalidationEvent.QueueName);

                invalidationEvent =
                    JsonSerializer.Deserialize<CacheInvalidationEvent>(
                        eventArgs.Body.ToArray());

                var keys = invalidationEvent?.Keys;

                if (keys is null ||
                    keys.Count == 0 ||
                    keys.Count > MaximumKeyCount ||
                    keys.Any(key =>
                        !CacheKeys.IsValidInvalidationKey(key)))
                {
                    throw new InvalidOperationException(
                        "Cache invalidation message contains an invalid key list.");
                }

                using var scope = _serviceScopeFactory.CreateScope();

                var cacheService =
                    scope.ServiceProvider.GetRequiredService<ICacheService>();

                foreach (var key in keys.Distinct(StringComparer.Ordinal))
                {
                    await cacheService.RemoveAsync(key, stoppingToken);
                }

                await channel.BasicAckAsync(
                    deliveryTag: eventArgs.DeliveryTag,
                    multiple: false,
                    cancellationToken: stoppingToken);

                _logger.LogInformation(
                    "Invalidated {KeyCount} cache entries for event {EventId}.",
                    keys.Distinct(StringComparer.Ordinal).Count(),
                    invalidationEvent.EventId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Cache invalidation message failed and will be dead-lettered. EventId: {EventId}",
                    invalidationEvent?.EventId);

                try
                {
                    await channel.BasicNackAsync(
                        deliveryTag: eventArgs.DeliveryTag,
                        multiple: false,
                        requeue: false,
                        cancellationToken: CancellationToken.None);
                }
                catch (Exception nackException)
                {
                    _logger.LogError(
                        nackException,
                        "Failed to negatively acknowledge cache invalidation message.");
                }
            }
        };

        await channel.BasicConsumeAsync(
            queue: CacheInvalidationEvent.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation(
            "CacheInvalidationConsumer is listening on queue {QueueName}.",
            CacheInvalidationEvent.QueueName);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
