using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Common;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/healthz", () => Results.Ok("OK"));

app.MapGet("/ws/ticker", async (HttpContext context) =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var ws = await context.WebSockets.AcceptWebSocketAsync();
        var rnd = new Random();
        while (ws.State == System.Net.WebSockets.WebSocketState.Open)
        {
            var tick = new RawTick(
                "BTCUSDT",
                (decimal)(rnd.NextDouble() * 1000 + 30000),
                (decimal)(rnd.NextDouble() * 1000 + 30000),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            );
            var json = System.Text.Json.JsonSerializer.Serialize(tick);
            var buffer = System.Text.Encoding.UTF8.GetBytes(json);
            await ws.SendAsync(buffer, System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None);
            await Task.Delay(200);
        }
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

app.UseWebSockets();
app.Run("http://0.0.0.0:8080");