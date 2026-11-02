
namespace BankTask.Domain.Entities;

public class GoogleOAuthToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string GoogleAccessTokenEncrypted { get; set; }
        = string.Empty;

    public string? GoogleRefreshTokenEncrypted { get; set; }

    public DateTime AccessTokenExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
