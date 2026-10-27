namespace BankTask.Application.Caching;

public static class CacheKeys
{
    public const string AllAccounts = "accounts:all";
    public const string AllTransactions = "transactions:all";

    private const string AccountIdPrefix = "accounts:id:";
    private const string TransactionIdPrefix = "transactions:id:";

    public static string AccountById(Guid accountId) =>
        $"{AccountIdPrefix}{accountId:D}";

    public static string TransactionById(Guid transactionId) =>
        $"{TransactionIdPrefix}{transactionId:D}";

    public static string ExchangeRate(string fromCurrency, string toCurrency) =>
        $"exchange-rate:{fromCurrency.ToLowerInvariant()}:{toCurrency.ToLowerInvariant()}";

    public static bool IsValidInvalidationKey(string? key)
    {
        if (key is null)
        {
            return false;
        }

        if (key == AllAccounts || key == AllTransactions)
        {
            return true;
        }

        return IsValidIdKey(key, AccountIdPrefix) ||
            IsValidIdKey(key, TransactionIdPrefix);
    }

    private static bool IsValidIdKey(string key, string prefix) =>
        key.StartsWith(prefix, StringComparison.Ordinal) &&
        Guid.TryParseExact(key[prefix.Length..], "D", out _);
}
