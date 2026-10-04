using BankTask.Application.DTOs.Authentication;
using BankTask.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace BankTask.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;

    public AuthController(
        IAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
    }

    [HttpPost("signup")]
    public async Task<IActionResult> Signup(
        SignupRequest request)
    {
        var result =
            await _authenticationService.SignupAsync(request);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        LoginRequest request)
    {
        var result =
            await _authenticationService.LoginAsync(request);

        return Ok(result);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(
        RefreshTokenRequest request)
    {
        var result =
            await _authenticationService.RefreshAsync(request);

        return Ok(result);
    }
}