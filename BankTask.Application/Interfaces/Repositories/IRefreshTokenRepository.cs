using BankTask.Domain.Entities;

namespace BankTask.Application.Interfaces.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshToken> CreateAsync(RefreshToken refreshToken);

    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash);

    Task<bool> RevokeAsync(Guid id);
}