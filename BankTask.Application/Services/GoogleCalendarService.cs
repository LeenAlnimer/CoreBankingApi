
using System.Net.Http.Headers;
using BankTask.Application.Interfaces;
using BankTask.Application.Interfaces.Repositories;
using BankTask.Application.Interfaces.Services;

namespace BankTask.Infrastructure.Services;

public class GoogleCalendarService : IGoogleCalendarService
{
    private readonly IGoogleOAuthTokenRepository _tokenRepository;
    private readonly IGoogleTokenProtector _tokenProtector;
    private readonly HttpClient _httpClient;

    public GoogleCalendarService(
        IGoogleOAuthTokenRepository tokenRepository,
        IGoogleTokenProtector tokenProtector,
        HttpClient httpClient)
    {
        _tokenRepository = tokenRepository;
        _tokenProtector = tokenProtector;
        _httpClient = httpClient;
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

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://www.googleapis.com/calendar/v3/calendars/primary/events");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request);

        var responseBody =
            await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"GOOGLE_CALENDAR_REQUEST_FAILED: {(int)response.StatusCode}");
        }

        return responseBody;
    }
}
    