
using System.Text.Json;
using BankTask.Api.Messaging;
using BankTask.Application.DTOs.Notifications;
using BankTask.Application.Events;
using BankTask.Application.Interfaces.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BankTask.Api.Consumers;

public class LargeTransactionAlertConsumer : BackgroundService
{
    private const string QueueName = "large-transaction-alert";

    private readonly RabbitMqConnectionManager _connectionManager;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<LargeTransactionAlertConsumer> _logger;

    public LargeTransactionAlertConsumer(
        RabbitMqConnectionManager connectionManager,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<LargeTransactionAlertConsumer> logger)
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
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);

        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            try
            {
                _logger.LogInformation(
                    "RabbitMQ received a large transaction alert message from {QueueName}.",
                    QueueName);

                var alertEvent =
                    JsonSerializer.Deserialize<LargeTransactionAlertEvent>(
                        eventArgs.Body.ToArray());

                if (alertEvent is null)
                {
                    throw new InvalidOperationException(
                        "Could not deserialize LargeTransactionAlertEvent.");
                }

                using var scope = _serviceScopeFactory.CreateScope();

                var notificationService =
                    scope.ServiceProvider.GetRequiredService<
                        ITransactionNotificationService>();

                var notification = new TransactionNotificationDto
                {
                    TransactionId = alertEvent.TransactionId,
                    TransactionType = alertEvent.TransactionType,
                    Amount = alertEvent.Amount,
                    Currency = alertEvent.Currency,
                    ReferenceNumber = alertEvent.ReferenceNumber,
                    SourceAccountId = alertEvent.SourceAccountId,
                    DestinationAccountId = alertEvent.DestinationAccountId,
                    CreatedAt = alertEvent.CreatedAt
                };

                var recipientUserIds =
                    alertEvent.RecipientUserIds ?? new List<Guid>();

                if (recipientUserIds.Count == 0)
                {
                    _logger.LogWarning(
                        "Large transaction alert {EventId} has no recipients.",
                        alertEvent.EventId);
                }

                foreach (var userId in recipientUserIds.Distinct())
                {
                    await notificationService.NotifyLargeTransactionAlertAsync(
                        userId,
                        notification);

                    _logger.LogInformation(
                        "LargeTransactionAlert sent through SignalR. UserId: {UserId}, TransactionId: {TransactionId}",
                        userId,
                        alertEvent.TransactionId);
                }

                await channel.BasicAckAsync(
                    deliveryTag: eventArgs.DeliveryTag,
                    multiple: false,
                    cancellationToken: stoppingToken);

                _logger.LogInformation(
                    "Large transaction alert message acknowledged. EventId: {EventId}",
                    alertEvent.EventId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to process large-transaction-alert message. It will be requeued.");

                try
                {
                    await channel.BasicNackAsync(
                        deliveryTag: eventArgs.DeliveryTag,
                        multiple: false,
                        requeue: true,
                        cancellationToken: CancellationToken.None);
                }
                catch (Exception nackException)
                {
                    _logger.LogError(
                        nackException,
                        "Failed to negatively acknowledge large transaction alert message.");
                }
            }
        };

        await channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation(
            "LargeTransactionAlertConsumer is listening on queue {QueueName}.",
            QueueName);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
