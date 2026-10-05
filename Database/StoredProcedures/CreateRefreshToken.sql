CREATE OR ALTER PROCEDURE dbo.CreateRefreshToken
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @TokenHash NVARCHAR(64),
    @ExpiresAt DATETIME2,
    @RevokedAt DATETIME2 = NULL,
    @CreatedAt DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO RefreshTokens
    (
        Id,
        UserId,
        TokenHash,
        ExpiresAt,
        RevokedAt,
        CreatedAt
    )
    VALUES
    (
        @Id,
        @UserId,
        @TokenHash,
        @ExpiresAt,
        @RevokedAt,
        @CreatedAt
    );

    SELECT
        Id,
        UserId,
        TokenHash,
        ExpiresAt,
        RevokedAt,
        CreatedAt
    FROM RefreshTokens
    WHERE Id = @Id;
END;
GO
