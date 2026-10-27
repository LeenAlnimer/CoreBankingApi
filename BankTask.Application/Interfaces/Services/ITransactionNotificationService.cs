
using BankTask.Application.DTOs.Notifications;

namespace BankTask.Application.Interfaces.Services;

public interface ITransactionNotificationService
{
    Task NotifyTransactionCreatedAsync(
        Guid userId,
        TransactionNotificationDto notification);

    Task NotifyLargeTransactionAlertAsync(
        Guid userId,
        TransactionNotificationDto notification);
}
