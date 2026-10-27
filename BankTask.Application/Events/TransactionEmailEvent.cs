
namespace BankTask.Application.Events;

public class TransactionEmailEvent
{
    public Guid EventId { get; set; }

    public Guid TransactionId { get; set; }

    public string TransactionType { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string ReferenceNumber { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public List<Guid> RecipientUserIds { get; set; } = new();
}
