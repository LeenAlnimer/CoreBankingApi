namespace BankTask.Application.Interfaces.Services;

public interface IBackgroundJobService
{
    void EnqueueWelcomeEmail(
        string to,
        string userName);
}