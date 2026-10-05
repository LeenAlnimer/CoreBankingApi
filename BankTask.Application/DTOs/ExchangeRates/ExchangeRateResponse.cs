namespace BankTask.Application.DTOs.ExchangeRates;

public class ExchangeRateResponse
{
    public DateTime Date { get; set; }

    public string Base { get; set; } = string.Empty;

    public string Quote { get; set; } = string.Empty;

    public decimal Rate { get; set; }
}