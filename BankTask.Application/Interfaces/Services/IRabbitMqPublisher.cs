
namespace BankTask.Application.Interfaces.Services;

public interface IRabbitMqPublisher
{
    Task PublishAsync<T>(
        string queueName,
        T message,
        CancellationToken cancellationToken = default);
}
