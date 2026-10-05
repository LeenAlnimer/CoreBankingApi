using System.Net.Http.Json;
using BankTask.Application.DTOs.ExchangeRates;
using BankTask.Application.Interfaces.Services;
using BankTask.Domain.Enums;

namespace BankTask.Application.Services;

public class ExchangeRateService : IExchangeRateService
{
    private readonly HttpClient _httpClient;

    public ExchangeRateService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ExchangeRateResponse> GetRateAsync(
        Currency from,
        Currency to)
    {
        var fromCurrency = from.ToString().ToLowerInvariant();
        var toCurrency = to.ToString().ToLowerInvariant();

        var url =
            $"https://api.frankfurter.dev/v2/rate/{fromCurrency}/{toCurrency}";

        var response =
            await _httpClient.GetFromJsonAsync<ExchangeRateResponse>(url);

        return response
            ?? throw new InvalidOperationException(
                "Exchange rate response was empty.");
    }
}