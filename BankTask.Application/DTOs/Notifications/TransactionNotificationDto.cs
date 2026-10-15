
namespace BankTask.Application.DTOs.Notifications;

public class TransactionNotificationDto
{
    public Guid TransactionId { get; set; }

    public string TransactionType { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string ReferenceNumber { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
