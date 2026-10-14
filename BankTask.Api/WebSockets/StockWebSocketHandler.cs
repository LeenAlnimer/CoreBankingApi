using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using BankTask.Application.Interfaces.Services;

namespace BankTask.Api.WebSockets;

public class StockWebSocketHandler
{
    private readonly IStockPriceService _stockPriceService;

    public StockWebSocketHandler(
        IStockPriceService stockPriceService)
    {
        _stockPriceService = stockPriceService;
    }

    public async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode =
                StatusCodes.Status400BadRequest;

            return;
        }

        using var webSocket =
            await context.WebSockets.AcceptWebSocketAsync();

        using var cancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                context.RequestAborted);

        var cancellationToken =
            cancellationTokenSource.Token;

        try
        {
            var sendingTask = SendStockUpdatesAsync(
                webSocket,
                cancellationToken);

            var receivingTask = ReceiveMessagesAsync(
                webSocket,
                cancellationToken);

            await Task.WhenAny(sendingTask, receivingTask);

            cancellationTokenSource.Cancel();

            await Task.WhenAll(sendingTask, receivingTask);
        }
        catch (OperationCanceledException)
        {
            // The connection or its operations were cancelled.
        }
        catch (WebSocketException)
        {
            // The WebSocket connection was interrupted.
        }
    }

    private async Task SendStockUpdatesAsync(
        WebSocket webSocket,
        CancellationToken cancellationToken)
    {
        while (webSocket.State == WebSocketState.Open)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var stock = _stockPriceService.GetStockPrice();

            var message = JsonSerializer.Serialize(stock);

            var messageBytes = Encoding.UTF8.GetBytes(message);

            await webSocket.SendAsync(
                new ArraySegment<byte>(messageBytes),
                WebSocketMessageType.Text,
                true,
                cancellationToken);

            await Task.Delay(
                TimeSpan.FromSeconds(2),
                cancellationToken);
        }
    }

    private async Task ReceiveMessagesAsync(
        WebSocket webSocket,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];

        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                if (webSocket.State == WebSocketState.CloseReceived)
                {
                    await webSocket.CloseOutputAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Connection closed by client",
                        CancellationToken.None);
                }

                return;
            }
        }
    }
}