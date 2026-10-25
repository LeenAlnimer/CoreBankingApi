
using System.Text.Json;
using BankTask.Api.Messaging;
using BankTask.Application.DTOs.Notifications;
using BankTask.Application.Events;
using BankTask.Application.Interfaces.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BankTask.Api.Consumers;

public class TransactionCreatedConsumer : BackgroundService
{
    private const string QueueName = "transaction-created";

    private readonly RabbitMqConnectionManager _connectionManager;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<TransactionCreatedConsumer> _logger;

    public TransactionCreatedConsumer(
        RabbitMqConnectionManager connectionManager,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<TransactionCreatedConsumer> logger)
    {
        _connectionManager = connectionManager;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "TransactionCreatedConsumer ExecuteAsync started.");

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
                    "RabbitMQ received a message from {QueueName}.",
                    QueueName);

                var transactionEvent =
                    JsonSerializer.Deserialize<TransactionCreatedEvent>(
                        eventArgs.Body.ToArray());

                if (transactionEvent is null)
                {
                    throw new InvalidOperationException(
                        "Could not deserialize TransactionCreatedEvent.");
                }

                _logger.LogInformation(
                    "Transaction event received. EventId: {EventId}, TransactionId: {TransactionId}, RecipientsCount: {RecipientsCount}",
                    transactionEvent.EventId,
                    transactionEvent.TransactionId,
                    transactionEvent.RecipientUserIds?.Count ?? 0);

                using var scope = _serviceScopeFactory.CreateScope();

                var notificationService =
                    scope.ServiceProvider.GetRequiredService<
                        ITransactionNotificationService>();

                var notification = new TransactionNotificationDto
                {
                    TransactionId = transactionEvent.TransactionId,
                    TransactionType = transactionEvent.TransactionType,
                    Amount = transactionEvent.Amount,
                    Currency = transactionEvent.Currency,
                    ReferenceNumber = transactionEvent.ReferenceNumber,
                    SourceAccountId = transactionEvent.SourceAccountId,
                    DestinationAccountId = transactionEvent.DestinationAccountId,
                    CreatedAt = transactionEvent.CreatedAt
                };

                var recipientUserIds =
                    transactionEvent.RecipientUserIds ?? new List<Guid>();

                if (recipientUserIds.Count == 0)
                {
                    _logger.LogWarning(
                        "Transaction event {EventId} has no recipients. No SignalR notification will be sent.",
                        transactionEvent.EventId);
                }

                foreach (var userId in recipientUserIds.Distinct())
                {
                    _logger.LogInformation(
                        "Attempting SignalR notification. UserId: {UserId}, TransactionId: {TransactionId}",
                        userId,
                        notification.TransactionId);

                    await notificationService.NotifyTransactionCreatedAsync(
                        userId,
                        notification);

                    _logger.LogInformation(
                        "SignalR notification send call completed. UserId: {UserId}, TransactionId: {TransactionId}",
                        userId,
                        notification.TransactionId);
                }

                await channel.BasicAckAsync(
                    deliveryTag: eventArgs.DeliveryTag,
                    multiple: false,
                    cancellationToken: stoppingToken);

                _logger.LogInformation(
                    "RabbitMQ message acknowledged. EventId: {EventId}, TransactionId: {TransactionId}",
                    transactionEvent.EventId,
                    transactionEvent.TransactionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to process transaction-created message.");

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
                        "Failed to negatively acknowledge transaction-created message.");
                }
            }
        };

        await channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation(
            "TransactionCreatedConsumer is listening on queue {QueueName}.",
            QueueName);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
