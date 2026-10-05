using BankTask.Application.DTOs.ExchangeRates;
using BankTask.Domain.Enums;

namespace BankTask.Application.Interfaces.Services;

public interface IExchangeRateService
{
    Task<ExchangeRateResponse> GetRateAsync(
        Currency from,
        Currency to);
}