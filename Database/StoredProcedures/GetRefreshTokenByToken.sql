CREATE OR ALTER PROCEDURE dbo.GetRefreshTokenByToken
    @TokenHash NVARCHAR(64)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        Id,
        UserId,
        TokenHash,
        ExpiresAt,
        RevokedAt,
        CreatedAt
    FROM RefreshTokens
    WHERE TokenHash = @TokenHash;
END;
GO
