using BankTask.Application.DTOs.ExchangeRates;
using BankTask.Application.Interfaces.Services;
using BankTask.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace BankTask.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExchangeRatesController : ControllerBase
{
    private readonly IExchangeRateService _exchangeRateService;

    public ExchangeRatesController(
        IExchangeRateService exchangeRateService)
    {
        _exchangeRateService = exchangeRateService;
    }

    [HttpGet("{from}/{to}")]
    public async Task<IActionResult> GetRate(
        Currency from,
        Currency to)
    {
        var result =
            await _exchangeRateService.GetRateAsync(from, to);

        return Ok(result);
    }
}