
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
        // Log the outgoing notification (GUID only, no sensitive data)
        _logger.LogInformation(
            "Sending TransactionCreated notification to user {UserId} for transaction {TransactionId}",
            userId,
            notification.TransactionId);

        await _hubContext.Clients
            .User(userId.ToString())
            .SendAsync(
                "TransactionCreated",
                notification);
    }
}
