using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using BankTask.Application.Interfaces;
using BankTask.Application.Interfaces.Repositories;
using BankTask.Application.Interfaces.Services;
using BankTask.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace BankTask.Infrastructure.Services;

public class GoogleCalendarService : IGoogleCalendarService
{
    private const string CalendarEventsUrl =
        "https://www.googleapis.com/calendar/v3/calendars/primary/events";

    private const string GoogleTokenUrl =
        "https://oauth2.googleapis.com/token";

    private readonly IGoogleOAuthTokenRepository _tokenRepository;
    private readonly IGoogleTokenProtector _tokenProtector;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public GoogleCalendarService(
        IGoogleOAuthTokenRepository tokenRepository,
        IGoogleTokenProtector tokenProtector,
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _tokenRepository = tokenRepository;
        _tokenProtector = tokenProtector;
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<string> GetEventsAsync(Guid userId)
    {
        var storedToken =
            await _tokenRepository.GetByUserIdAsync(userId);

        if (storedToken is null)
        {
            throw new InvalidOperationException(
                "GOOGLE_CALENDAR_NOT_CONNECTED");
        }

        var accessToken = _tokenProtector.Unprotect(
            storedToken.GoogleAccessTokenEncrypted);

        // جدّد التوكن إذا انتهى أو بقي أقل من دقيقة على انتهائه.
        if (storedToken.AccessTokenExpiresAt <= DateTime.UtcNow.AddMinutes(1))
        {
            accessToken = await RefreshAccessTokenAsync(storedToken);
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            CalendarEventsUrl);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request);
        var responseBody =
            await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            return responseBody;
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new HttpRequestException(
                "GOOGLE_CALENDAR_UNAUTHORIZED: Google rejected the access token.");
        }

        if ((int)response.StatusCode == 429)
        {
            throw new HttpRequestException(
                "GOOGLE_CALENDAR_RATE_LIMITED: Google API rate limit was reached.");
        }

        if ((int)response.StatusCode >= 500)
        {
            throw new HttpRequestException(
                "GOOGLE_CALENDAR_UNAVAILABLE: Google API is temporarily unavailable.");
        }

        throw new HttpRequestException(
            $"GOOGLE_CALENDAR_REQUEST_FAILED: HTTP {(int)response.StatusCode}.");
    }

    private async Task<string> RefreshAccessTokenAsync(
        GoogleOAuthToken storedToken)
    {
        if (string.IsNullOrWhiteSpace(
                storedToken.GoogleRefreshTokenEncrypted))
        {
            throw new InvalidOperationException(
                "GOOGLE_REFRESH_TOKEN_MISSING");
        }

        var refreshToken = _tokenProtector.Unprotect(
            storedToken.GoogleRefreshTokenEncrypted);

        var clientId =
            _configuration["Authentication:Google:ClientId"];

        var clientSecret =
            _configuration["Authentication:Google:ClientSecret"];

        if (string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException(
                "GOOGLE_OAUTH_CONFIGURATION_MISSING");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            GoogleTokenUrl)
        {
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["refresh_token"] = refreshToken,
                    ["grant_type"] = "refresh_token"
                })
        };

        using var response = await _httpClient.SendAsync(request);
        var responseBody =
            await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"GOOGLE_TOKEN_REFRESH_FAILED: HTTP {(int)response.StatusCode}.");
        }

        var tokenResponse =
            JsonSerializer.Deserialize<GoogleTokenRefreshResponse>(
                responseBody);

        if (tokenResponse is null ||
            string.IsNullOrWhiteSpace(tokenResponse.AccessToken) ||
            tokenResponse.ExpiresIn <= 0)
        {
            throw new InvalidOperationException(
                "GOOGLE_TOKEN_REFRESH_INVALID_RESPONSE");
        }

        storedToken.GoogleAccessTokenEncrypted =
            _tokenProtector.Protect(tokenResponse.AccessToken);

        storedToken.AccessTokenExpiresAt =
            DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

        storedToken.UpdatedAt = DateTime.UtcNow;

        await _tokenRepository.UpdateAsync(storedToken);

        return tokenResponse.AccessToken;
    }

    private sealed class GoogleTokenRefreshResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}