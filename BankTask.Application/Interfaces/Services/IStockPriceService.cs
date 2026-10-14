
using BankTask.Application.DTOs.Stocks;

namespace BankTask.Application.Interfaces.Services;

public interface IStockPriceService
{
    StockPriceDto GetStockPrice();
}
