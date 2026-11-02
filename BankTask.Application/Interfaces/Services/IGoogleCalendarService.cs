
namespace BankTask.Application.Interfaces.Services;

public interface IGoogleCalendarService
{
    Task<string> GetEventsAsync(Guid userId);
}
