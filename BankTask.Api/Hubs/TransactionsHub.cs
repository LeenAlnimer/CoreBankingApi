
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BankTask.Api.Hubs;

[Authorize]
public class TransactionsHub : Hub
{
    public async Task SendTestNotification()
    {
        await Clients.Caller.SendAsync(
            "ReceiveNotification",
            "Hello from TransactionsHub!");
    }

    public async Task SendTransactionTest()
    {
        await Clients.Caller.SendAsync(
            "TransactionCreated",
            new
            {
                TransactionId = Guid.NewGuid(),
                TransactionType = "Deposit",
                Amount = 10,
                Currency = "JOD"
            });
    }

    public Task<object> GetConnectionDiagnostics()
    {
        var isAuthenticated = Context.User?.Identity?.IsAuthenticated ?? false;

        var nameIdentifier = Context.User?
            .FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
            ?.Value;

        var sub = Context.User?.FindFirst("sub")?.Value;

        var nameid = Context.User?.FindFirst("nameid")?.Value;

        var result = new
        {
            UserIdentifier = Context.UserIdentifier,
            IsAuthenticated = isAuthenticated,
            NameIdentifierClaim = nameIdentifier,
            SubClaim = sub,
            NameIdClaim = nameid
        };

        return Task.FromResult<object>(result);
    }
}
