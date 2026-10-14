
namespace BankTask.Application.DTOs.Stocks;

public class StockPriceDto
{
    public string Symbol { get; set; } = string.Empty;

    public decimal Price { get; set; }
}
