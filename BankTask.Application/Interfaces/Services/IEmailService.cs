namespace BankTask.Application.Interfaces.Services;

public interface IEmailService
{
    Task SendAsync(
        string to,
        string subject,
        string htmlBody);

    Task SendWelcomeEmailAsync(
        string to,
        string userName);
}