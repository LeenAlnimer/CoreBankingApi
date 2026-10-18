
namespace BankTask.Application.DTOs.Notifications;

public class TransactionNotificationDto
{
    public Guid TransactionId { get; set; }

    public string TransactionType { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string ReferenceNumber { get; set; } = string.Empty;

    public Guid? SourceAccountId { get; set; }

    public Guid? DestinationAccountId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
