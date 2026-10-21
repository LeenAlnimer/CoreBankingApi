
using System.Text.Json;
using BankTask.Application.Events;
using BankTask.Application.Interfaces.Repositories;
using BankTask.Application.Interfaces.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BankTask.Api.Consumers;

public class TransactionEmailConsumer : BackgroundService
{
    private const string QueueName = "transaction-email";

    private readonly ConnectionFactory _connectionFactory;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<TransactionEmailConsumer> _logger;

    public TransactionEmailConsumer(
        ConnectionFactory connectionFactory,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<TransactionEmailConsumer> logger)
    {
        _connectionFactory = connectionFactory;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await using var connection =
            await _connectionFactory.CreateConnectionAsync(
                stoppingToken);

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
                var transactionEvent =
                    JsonSerializer.Deserialize<TransactionEmailEvent>(
                        eventArgs.Body.ToArray());

                if (transactionEvent is null)
                {
                    throw new InvalidOperationException(
                        "Could not deserialize TransactionEmailEvent.");
                }

                using var scope =
                    _serviceScopeFactory.CreateScope();

                var userRepository =
                    scope.ServiceProvider
                        .GetRequiredService<IUserRepository>();

                var emailService =
                    scope.ServiceProvider
                        .GetRequiredService<IEmailService>();

                foreach (var userId in
                    transactionEvent.RecipientUserIds.Distinct())
                {
                    var user =
                        await userRepository.GetByIdAsync(userId);

                    if (user is null)
                    {
                        _logger.LogWarning(
                            "User {UserId} was not found. Email skipped.",
                            userId);

                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(user.Email))
                    {
                        _logger.LogWarning(
                            "User {UserId} has no email address.",
                            userId);

                        continue;
                    }

                    var placeholders =
                        new Dictionary<string, string>
                        {
                            ["{{UserName}}"] = user.FullName,
                            ["{{TransactionId}}"] =
                                transactionEvent.TransactionId.ToString(),
                            ["{{TransactionType}}"] =
                                transactionEvent.TransactionType,
                            ["{{Amount}}"] =
                                transactionEvent.Amount.ToString("N2"),
                            ["{{Currency}}"] =
                                transactionEvent.Currency,
                            ["{{ReferenceNumber}}"] =
                                transactionEvent.ReferenceNumber,
                            ["{{CreatedAt}}"] =
                                transactionEvent.CreatedAt.ToString(
                                    "yyyy-MM-dd HH:mm:ss 'UTC'")
                        };

                    await emailService.SendTemplatedEmailAsync(
                        user.Email,
                        $"BankTask - {transactionEvent.TransactionType} Notification",
                        "TransactionEmail.html",
                        placeholders,
                        stoppingToken);

                    _logger.LogInformation(
                        "Transaction email sent to user {UserId} for transaction {TransactionId}.",
                        userId,
                        transactionEvent.TransactionId);
                }

                await channel.BasicAckAsync(
                    deliveryTag: eventArgs.DeliveryTag,
                    multiple: false,
                    cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to process transaction email message.");

                await channel.BasicNackAsync(
                    deliveryTag: eventArgs.DeliveryTag,
                    multiple: false,
                    requeue: false,
                    cancellationToken: stoppingToken);
            }
        };

        await channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation(
            "TransactionEmailConsumer is listening on queue {QueueName}.",
            QueueName);

        await Task.Delay(
            Timeout.Infinite,
            stoppingToken);
    }
}
