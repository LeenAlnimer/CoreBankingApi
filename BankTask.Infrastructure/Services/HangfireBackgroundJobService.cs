using BankTask.Application.Interfaces.Services;
using Hangfire;

namespace BankTask.Infrastructure.Services;

public class HangfireBackgroundJobService : IBackgroundJobService
{
    public void EnqueueWelcomeEmail(
        string to,
        string userName)
    {
        BackgroundJob.Enqueue<Jobs.WelcomeEmailJob>(
            job => job.ExecuteAsync(
                to,
                userName));
    }
}