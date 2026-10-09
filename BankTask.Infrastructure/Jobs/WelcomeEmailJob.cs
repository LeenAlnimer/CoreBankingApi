using BankTask.Application.Interfaces.Services;

namespace BankTask.Infrastructure.Jobs;

public class WelcomeEmailJob
{
    private readonly IEmailService _emailService;

    public WelcomeEmailJob(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task ExecuteAsync(
        string to,
        string userName)
    {
        await _emailService.SendTemplatedEmailAsync(
            to,
            "Welcome to BankTask",
            "WelcomeEmail.html",
            new Dictionary<string, string>
            {
                ["{{UserName}}"] = userName
            });
    }
}