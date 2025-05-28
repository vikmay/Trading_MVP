using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Common;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/healthz", () => Results.Ok("OK"));

app.MapGet("/ws/ticker", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    using var ws = await context.WebSockets.AcceptWebSocketAsync();
    var rnd = new Random();
    long seq = 0;                                     // 🔹 running number

    while (ws.State == WebSocketState.Open)
    {
        // generate prices
        double bid = rnd.NextDouble() * 1_000 + 30_000;
        double ask = rnd.NextDouble() * 1_000 + 30_000;

        var tick = new RawTick(
            symbol: "BTCUSDT",
            bid: bid,
            ask: ask,
            tsMs: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            seq: ++seq);                                // 🔹 increment

        var json = JsonSerializer.Serialize(tick);
        var buffer = Encoding.UTF8.GetBytes(json);

        await ws.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
        await Task.Delay(500);                             // 🔹 lower frequency
    }
});

app.UseWebSockets();
app.Run("http://0.0.0.0:8080");
