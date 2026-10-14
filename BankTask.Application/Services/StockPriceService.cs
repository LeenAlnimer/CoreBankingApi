
using BankTask.Application.DTOs.Stocks;
using BankTask.Application.Interfaces.Services;

namespace BankTask.Application.Services;

public class StockPriceService : IStockPriceService
{
    private readonly Random _random = new();

    private decimal _currentPrice = 231.20m;

    public StockPriceDto GetStockPrice()
    {
        decimal priceChange = (decimal)(_random.NextDouble() * 2 - 1);

        _currentPrice += priceChange;

        if (_currentPrice < 1)
        {
            _currentPrice = 1;
        }

        return new StockPriceDto
        {
            Symbol = "AAPL",
            Price = Math.Round(_currentPrice, 2)
        };
    }
}
