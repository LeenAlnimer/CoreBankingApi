
using BankTask.Application.DTOs.Authentication;
using BankTask.Application.DTOs.Users;

namespace BankTask.Application.Interfaces.Services;

public interface IAuthenticationService
{
    Task<UserResponse> SignupAsync(SignupRequest request);

    Task<LoginResponse> LoginAsync(LoginRequest request);

    Task<LoginResponse> GoogleLoginAsync(string email);

    Task<LoginResponse> GoogleLoginAsync(
        string email,
        string googleAccessToken,
        string? googleRefreshToken,
        DateTime accessTokenExpiresAt);

    Task<LoginResponse> RefreshAsync(
        RefreshTokenRequest request);
}
