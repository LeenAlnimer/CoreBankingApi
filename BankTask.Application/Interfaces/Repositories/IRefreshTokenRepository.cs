using BankTask.Domain.Entities;

namespace BankTask.Application.Interfaces.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshToken> CreateAsync(RefreshToken refreshToken);

    Task<RefreshToken?> GetByTokenAsync(string token);

    Task<bool> RevokeAsync(Guid id);
}