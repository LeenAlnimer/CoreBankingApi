using BankTask.Application.DTOs.Authentication;
using BankTask.Application.DTOs.Users;

namespace BankTask.Application.Interfaces.Services;

public interface IAuthenticationService
{
    Task<UserResponse> SignupAsync(SignupRequest request);

    Task<LoginResponse> LoginAsync(LoginRequest request);

    Task<LoginResponse> RefreshAsync(RefreshTokenRequest request);
}