using System.Net.Http.Json;
using BankTask.Application.DTOs.ExchangeRates;
using BankTask.Application.Interfaces.Services;
using BankTask.Domain.Enums;

namespace BankTask.Application.Services;

public class ExchangeRateService : IExchangeRateService
{
    private readonly HttpClient _httpClient;
    private readonly ICacheService _cacheService;

    public ExchangeRateService(
        HttpClient httpClient,
        ICacheService cacheService)
    {
        _httpClient = httpClient;
        _cacheService = cacheService;
    }

    public async Task<ExchangeRateResponse> GetRateAsync(
        Currency from,
        Currency to)
    {
        var fromCurrency = from.ToString().ToLowerInvariant();
        var toCurrency = to.ToString().ToLowerInvariant();
        var cacheKey =
            $"exchange-rate:{fromCurrency}:{toCurrency}"; var cachedRate =
        await _cacheService.GetAsync<ExchangeRateResponse>(
            cacheKey);

        if (cachedRate is not null)
        {
            return cachedRate;
        }


        var url =
            $"https://api.frankfurter.dev/v2/rate/{fromCurrency}/{toCurrency}";

        var response =
        await _httpClient.GetFromJsonAsync<ExchangeRateResponse>(url);

        if (response is null)
        {
            throw new InvalidOperationException(
                "Exchange rate response was empty.");
        }

        await _cacheService.SetAsync(
            cacheKey,
            response,
            TimeSpan.FromMinutes(10));

        return response;
    }
}