using BankTask.Application.DTOs.Authentication;
using IAuthenticationService =
    BankTask.Application.Interfaces.Services.IAuthenticationService;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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

    [HttpGet("google-login")]
    public IActionResult GoogleLogin()
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = "/api/auth/google-response"
        };

        return Challenge(properties, "Google");
    }

    [HttpGet("google-response")]
    public async Task<IActionResult> GoogleResponse()
    {
        var externalResult =
            await HttpContext.AuthenticateAsync("External");

        if (!externalResult.Succeeded ||
            externalResult.Principal is null)
        {
            await HttpContext.SignOutAsync("External");

            return Unauthorized(new
            {
                Message = "GOOGLE_AUTHENTICATION_FAILED"
            });
        }

        var principal = externalResult.Principal;

        var email = principal.FindFirst(ClaimTypes.Email)?.Value;

        var verifiedEmail =
            principal.FindFirst("urn:google:email_verified")?.Value;

        if (string.IsNullOrWhiteSpace(email) ||
            !string.Equals(
                verifiedEmail,
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            await HttpContext.SignOutAsync("External");

            return Unauthorized(new
            {
                Message = "GOOGLE_EMAIL_NOT_VERIFIED"
            });
        }

        // Clear the temporary external authentication cookie.
        await HttpContext.SignOutAsync("External");

        // Only existing users in our database can sign in.
        var result =
            await _authenticationService.GoogleLoginAsync(email);

        return Ok(result);
    }
}
