
using BankTask.Api.Hubs;
using BankTask.Application.DTOs.Notifications;
using BankTask.Application.Interfaces.Services;
using Microsoft.AspNetCore.SignalR;

namespace BankTask.Api.Services;

public class TransactionNotificationService
    : ITransactionNotificationService
{
    private readonly IHubContext<TransactionsHub> _hubContext;
    private readonly ILogger<TransactionNotificationService> _logger;

    public TransactionNotificationService(
        IHubContext<TransactionsHub> hubContext,
        ILogger<TransactionNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyTransactionCreatedAsync(
        Guid userId,
        TransactionNotificationDto notification)
    {
        _logger.LogInformation(
            "Sending SignalR event {SignalREventName} to user {UserId} for transaction {TransactionId}",
            "TransactionCreated",
            userId,
            notification.TransactionId);

        try
        {
            await _hubContext.Clients
                .User(userId.ToString())
                .SendAsync(
                    "TransactionCreated",
                    notification);

            _logger.LogInformation(
                "SignalR event {SignalREventName} send completed for user {UserId}, transaction {TransactionId}",
                "TransactionCreated",
                userId,
                notification.TransactionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "SignalR event {SignalREventName} send failed for user {UserId}, transaction {TransactionId}",
                "TransactionCreated",
                userId,
                notification.TransactionId);
            throw;
        }
    }

    public async Task NotifyLargeTransactionAlertAsync(
        Guid userId,
        TransactionNotificationDto notification)
    {
        _logger.LogInformation(
            "Sending SignalR event {SignalREventName} to user {UserId} for transaction {TransactionId}",
            "LargeTransactionAlert",
            userId,
            notification.TransactionId);

        try
        {
            await _hubContext.Clients
                .User(userId.ToString())
                .SendAsync(
                    "LargeTransactionAlert",
                    notification);

            _logger.LogInformation(
                "SignalR event {SignalREventName} send completed for user {UserId}, transaction {TransactionId}",
                "LargeTransactionAlert",
                userId,
                notification.TransactionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "SignalR event {SignalREventName} send failed for user {UserId}, transaction {TransactionId}",
                "LargeTransactionAlert",
                userId,
                notification.TransactionId);
            throw;
        }
    }
}
