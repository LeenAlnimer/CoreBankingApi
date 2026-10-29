
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BankTask.Application.DTOs.Authentication;
using BankTask.Application.DTOs.Users;
using BankTask.Application.Interfaces;
using BankTask.Application.Interfaces.Repositories;
using BankTask.Application.Interfaces.Security;
using BankTask.Application.Interfaces.Services;
using BankTask.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace BankTask.Application.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IGoogleOAuthTokenRepository _googleOAuthTokenRepository;
    private readonly IGoogleTokenProtector _googleTokenProtector;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IBackgroundJobService _backgroundJobService;

    public AuthenticationService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IGoogleOAuthTokenRepository googleOAuthTokenRepository,
        IGoogleTokenProtector googleTokenProtector,
        IPasswordHasher passwordHasher,
        IJwtService jwtService,
        IAuditLogRepository auditLogRepository,
        IHttpContextAccessor httpContextAccessor,
        IBackgroundJobService backgroundJobService)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _googleOAuthTokenRepository = googleOAuthTokenRepository;
        _googleTokenProtector = googleTokenProtector;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
        _auditLogRepository = auditLogRepository;
        _httpContextAccessor = httpContextAccessor;
        _backgroundJobService = backgroundJobService;
    }

    public async Task<UserResponse> SignupAsync(SignupRequest request)
    {
        var existingUser =
            await _userRepository.GetByEmailAsync(request.Email);

        if (existingUser is not null)
        {
            throw new BankTask.Application.Exceptions.ConflictException(
                "USER_EMAIL_ALREADY_EXISTS");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName,
            Email = request.Email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null
        };

        var createdUser =
            await _userRepository.CreateAsync(user);

        await CreateAuditLogAsync(
            action: "USER_SIGNED_UP",
            userId: createdUser.Id,
            entityId: createdUser.Id,
            newValues: new
            {
                createdUser.Id,
                createdUser.FullName,
                createdUser.Email,
                createdUser.CreatedAt
            });

        _backgroundJobService.EnqueueWelcomeEmail(
            createdUser.Email,
            createdUser.FullName);

        return new UserResponse
        {
            Id = createdUser.Id,
            FullName = createdUser.FullName,
            Email = createdUser.Email,
            CreatedAt = createdUser.CreatedAt,
            UpdatedAt = createdUser.UpdatedAt
        };
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var user =
            await _userRepository.GetByEmailAsync(request.Email);

        if (user is null ||
            !_passwordHasher.Verify(
                request.Password,
                user.PasswordHash))
        {
            throw new BankTask.Application.Exceptions.UnauthorizedException(
                "AUTH_INVALID_CREDENTIALS");
        }

        var accessToken = _jwtService.GenerateToken(user);
        var rawRefreshToken = GenerateRefreshToken();

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashRefreshToken(rawRefreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = null,
            CreatedAt = DateTime.UtcNow
        };

        await _refreshTokenRepository.CreateAsync(refreshToken);

        await CreateAuditLogAsync(
            action: "USER_LOGGED_IN",
            userId: user.Id,
            entityId: user.Id,
            newValues: null);

        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken
        };
    }

    // Existing Google login flow.
    // Kept for compatibility with existing callers.
    public async Task<LoginResponse> GoogleLoginAsync(string email)
    {
        var user = await _userRepository.GetByEmailAsync(email);

        if (user is null)
        {
            throw new BankTask.Application.Exceptions.UnauthorizedException(
                "AUTH_INVALID_CREDENTIALS");
        }

        return await CreateLoginResponseAsync(
            user,
            "USER_LOGGED_IN_WITH_GOOGLE");
    }

    // Google login flow that also saves encrypted Google tokens.
    public async Task<LoginResponse> GoogleLoginAsync(
        string email,
        string googleAccessToken,
        string? googleRefreshToken,
        DateTime accessTokenExpiresAt)
    {
        if (string.IsNullOrWhiteSpace(googleAccessToken))
        {
            throw new BankTask.Application.Exceptions.UnauthorizedException(
                "GOOGLE_ACCESS_TOKEN_MISSING");
        }

        var user = await _userRepository.GetByEmailAsync(email);

        if (user is null)
        {
            throw new BankTask.Application.Exceptions.UnauthorizedException(
                "AUTH_INVALID_CREDENTIALS");
        }

        var existingToken =
            await _googleOAuthTokenRepository.GetByUserIdAsync(user.Id);

        var now = DateTime.UtcNow;

        var googleToken = new GoogleOAuthToken
        {
            Id = existingToken?.Id ?? Guid.NewGuid(),
            UserId = user.Id,

            GoogleAccessTokenEncrypted =
                _googleTokenProtector.Protect(googleAccessToken),

            GoogleRefreshTokenEncrypted =
                string.IsNullOrWhiteSpace(googleRefreshToken)
                    ? null
                    : _googleTokenProtector.Protect(googleRefreshToken),

            AccessTokenExpiresAt =
                accessTokenExpiresAt.Kind == DateTimeKind.Utc
                    ? accessTokenExpiresAt
                    : accessTokenExpiresAt.ToUniversalTime(),

            CreatedAt = existingToken?.CreatedAt ?? now,
            UpdatedAt = existingToken is null ? null : now
        };

        if (existingToken is null)
        {
            await _googleOAuthTokenRepository.SaveAsync(googleToken);
        }
        else
        {
            await _googleOAuthTokenRepository.UpdateAsync(googleToken);
        }

        return await CreateLoginResponseAsync(
            user,
            "USER_LOGGED_IN_WITH_GOOGLE");
    }

    public async Task<LoginResponse> RefreshAsync(
        RefreshTokenRequest request)
    {
        var tokenHash =
            HashRefreshToken(request.RefreshToken);

        var refreshToken =
            await _refreshTokenRepository.GetByTokenHashAsync(
                tokenHash);

        if (refreshToken is null ||
            refreshToken.RevokedAt is not null ||
            refreshToken.ExpiresAt <= DateTime.UtcNow)
        {
            throw new BankTask.Application.Exceptions.UnauthorizedException(
                "AUTH_INVALID_CREDENTIALS");
        }

        var user =
            await _userRepository.GetByIdAsync(refreshToken.UserId);

        if (user is null)
        {
            throw new BankTask.Application.Exceptions.UnauthorizedException(
                "AUTH_INVALID_CREDENTIALS");
        }

        await _refreshTokenRepository.RevokeAsync(
            refreshToken.Id);

        var newAccessToken =
            _jwtService.GenerateToken(user);

        var rawNewRefreshToken = GenerateRefreshToken();

        var newRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashRefreshToken(rawNewRefreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = null,
            CreatedAt = DateTime.UtcNow
        };

        await _refreshTokenRepository.CreateAsync(
            newRefreshToken);

        return new LoginResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = rawNewRefreshToken
        };
    }

    private async Task<LoginResponse> CreateLoginResponseAsync(
        User user,
        string auditAction)
    {
        var accessToken = _jwtService.GenerateToken(user);
        var rawRefreshToken = GenerateRefreshToken();

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashRefreshToken(rawRefreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = null,
            CreatedAt = DateTime.UtcNow
        };

        await _refreshTokenRepository.CreateAsync(refreshToken);

        await CreateAuditLogAsync(
            action: auditAction,
            userId: user.Id,
            entityId: user.Id,
            newValues: null);

        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken
        };
    }

    private static string GenerateRefreshToken()
    {
        return Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(64));
    }

    private static string HashRefreshToken(string token)
    {
        var hash =
            SHA256.HashData(
                Encoding.UTF8.GetBytes(token));

        return Convert.ToHexString(hash);
    }

    private async Task CreateAuditLogAsync(
        string action,
        Guid userId,
        Guid entityId,
        object? newValues)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            UserId = userId,
            Action = action,
            EntityType = "User",
            EntityId = entityId,
            OldValues = null,
            NewValues =
                newValues is null
                    ? null
                    : JsonSerializer.Serialize(newValues),
            IpAddress =
                httpContext?.Connection.RemoteIpAddress?.ToString(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _auditLogRepository.CreateAsync(auditLog);
    }
}
