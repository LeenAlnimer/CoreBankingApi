namespace BankTask.Application.Interfaces.Services;

public interface IEmailService
{
    Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default);

    Task SendTemplatedEmailAsync(
        string to,
        string subject,
        string templateName,
        IReadOnlyDictionary<string, string> placeholders,
        CancellationToken cancellationToken = default);
}
