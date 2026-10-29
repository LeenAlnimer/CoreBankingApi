
using BankTask.Domain.Entities;

namespace BankTask.Application.Interfaces.Repositories;

public interface IGoogleOAuthTokenRepository
{
    Task<GoogleOAuthToken?> GetByUserIdAsync(Guid userId);

    Task SaveAsync(GoogleOAuthToken token);

    Task UpdateAsync(GoogleOAuthToken token);

    Task DeleteByUserIdAsync(Guid userId);
}
