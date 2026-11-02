
using System.Data;
using BankTask.Application.Interfaces.Repositories;
using BankTask.DBManager;
using BankTask.Domain.Entities;
using Dapper;

namespace BankTask.Infrastructure.Repositories;

public class GoogleOAuthTokenRepository
    : IGoogleOAuthTokenRepository
{
    private readonly IConnectionFactory _connectionFactory;

    public GoogleOAuthTokenRepository(
        IConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private IDbConnection CreateConnection() =>
        _connectionFactory.CreateConnection(
            DatabaseType.SqlServer);

    public async Task<GoogleOAuthToken?> GetByUserIdAsync(
        Guid userId)
    {
        const string sql = """
            SELECT
                Id,
                UserId,
                GoogleAccessTokenEncrypted,
                GoogleRefreshTokenEncrypted,
                AccessTokenExpiresAt,
                CreatedAt,
                UpdatedAt
            FROM dbo.GoogleOAuthTokens
            WHERE UserId = @UserId;
            """;

        using var connection = CreateConnection();

        return await connection.QuerySingleOrDefaultAsync
            <GoogleOAuthToken>(
                sql,
                new { UserId = userId });
    }

    public async Task SaveAsync(GoogleOAuthToken token)
    {
        const string sql = """
            INSERT INTO dbo.GoogleOAuthTokens
            (
                Id,
                UserId,
                GoogleAccessTokenEncrypted,
                GoogleRefreshTokenEncrypted,
                AccessTokenExpiresAt,
                CreatedAt,
                UpdatedAt
            )
            VALUES
            (
                @Id,
                @UserId,
                @GoogleAccessTokenEncrypted,
                @GoogleRefreshTokenEncrypted,
                @AccessTokenExpiresAt,
                @CreatedAt,
                @UpdatedAt
            );
            """;

        using var connection = CreateConnection();

        await connection.ExecuteAsync(sql, token);
    }

    public async Task UpdateAsync(GoogleOAuthToken token)
    {
        const string sql = """
            UPDATE dbo.GoogleOAuthTokens
            SET
                GoogleAccessTokenEncrypted =
                    @GoogleAccessTokenEncrypted,
                GoogleRefreshTokenEncrypted =
                    COALESCE(
                        @GoogleRefreshTokenEncrypted,
                        GoogleRefreshTokenEncrypted),
                AccessTokenExpiresAt = @AccessTokenExpiresAt,
                UpdatedAt = @UpdatedAt
            WHERE UserId = @UserId;
            """;

        using var connection = CreateConnection();

        var rowsAffected =
            await connection.ExecuteAsync(sql, token);

        if (rowsAffected == 0)
        {
            throw new InvalidOperationException(
                "GOOGLE_OAUTH_TOKEN_NOT_FOUND");
        }
    }

    public async Task DeleteByUserIdAsync(Guid userId)
    {
        const string sql = """
            DELETE FROM dbo.GoogleOAuthTokens
            WHERE UserId = @UserId;
            """;

        using var connection = CreateConnection();

        await connection.ExecuteAsync(
            sql,
            new { UserId = userId });
    }
}
