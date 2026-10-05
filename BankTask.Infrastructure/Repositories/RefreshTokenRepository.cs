using System.Data;
using Dapper;
using BankTask.Application.Interfaces.Repositories;
using BankTask.DBManager;
using BankTask.Domain.Entities;

namespace BankTask.Infrastructure.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IConnectionFactory _connectionFactory;

    private const string CreateSp = "CreateRefreshToken";
    private const string GetByTokenSp = "GetRefreshTokenByToken";
    private const string RevokeSp = "RevokeRefreshToken";

    public RefreshTokenRepository(
        IConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private IDbConnection CreateConnection() =>
        _connectionFactory.CreateConnection(DatabaseType.SqlServer);

    public async Task<RefreshToken> CreateAsync(
        RefreshToken refreshToken)
    {
        using var connection = CreateConnection();

        return await connection.QuerySingleAsync<RefreshToken>(
            CreateSp,
            new
            {
                refreshToken.Id,
                refreshToken.UserId,
                refreshToken.TokenHash,
                refreshToken.ExpiresAt,
                refreshToken.RevokedAt,
                refreshToken.CreatedAt
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<RefreshToken?> GetByTokenHashAsync(
        string tokenHash)
    {
        using var connection = CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<RefreshToken>(
            GetByTokenSp,
            new
            {
                TokenHash = tokenHash
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<bool> RevokeAsync(Guid id)
    {
        using var connection = CreateConnection();

        var rowsAffected = await connection.QuerySingleAsync<int>(
            RevokeSp,
            new
            {
                Id = id
            },
            commandType: CommandType.StoredProcedure);

        return rowsAffected == 1;
    }
}
