
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BankTask.Api.Hubs;

[Authorize]
public class TransactionsHub : Hub
{
    private readonly ILogger<TransactionsHub> _logger;

    public TransactionsHub(ILogger<TransactionsHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation(
            "SignalR client connected. ConnectionId: {ConnectionId}, UserIdentifier: {UserIdentifier}",
            Context.ConnectionId,
            Context.UserIdentifier);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception is null)
        {
            _logger.LogInformation(
                "SignalR client disconnected. ConnectionId: {ConnectionId}, UserIdentifier: {UserIdentifier}",
                Context.ConnectionId,
                Context.UserIdentifier);
        }
        else
        {
            _logger.LogWarning(
                exception,
                "SignalR client disconnected with an error. ConnectionId: {ConnectionId}, UserIdentifier: {UserIdentifier}",
                Context.ConnectionId,
                Context.UserIdentifier);
        }

        await base.OnDisconnectedAsync(exception);
    }

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
