namespace BankTask.Application.Events;

public class CacheInvalidationEvent
{
    public const string QueueName = "cache-invalidation";
    public const string DeadLetterQueueName = "cache-invalidation-dead-letter";

    public Guid EventId { get; set; }

    public List<string>? Keys { get; set; }
}
