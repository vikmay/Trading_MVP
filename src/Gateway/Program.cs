using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Channels;
using Common;
using Gateway.Hubs;

var builder = WebApplication.CreateBuilder(args);

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy
            .WithOrigins("http://localhost", "http://localhost:80")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Channel для передачі тіків
var channel = Channel.CreateUnbounded<UiTick>();
builder.Services.AddSingleton(channel.Reader);
builder.Services.AddSingleton(channel.Writer);

builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;  // Для кращої діагностики
});

var app = builder.Build();

app.UseCors("CorsPolicy");
app.UseWebSockets();

// Отримуємо IHubContext і ChannelReader для роботи з SignalR
var hubContext = app.Services.GetRequiredService<IHubContext<MarketHub>>();
var reader = app.Services.GetRequiredService<ChannelReader<UiTick>>();

// Фоновий таск для трансляції тікерів
_ = Task.Run(async () =>
{
    try
    {
        await foreach (var tick in reader.ReadAllAsync())
        {
            try
            {
                await hubContext.Clients.All.SendAsync("tick", tick);
                app.Logger.LogInformation("Sent tick to clients: {Tick}", tick);
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "Error sending tick to clients");
            }
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error in tick broadcast loop");
    }
});

// WebSocket endpoint для Collector
app.Map("/ws/collector", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        var writer = context.RequestServices.GetRequiredService<ChannelWriter<UiTick>>();
        var buffer = new byte[1024 * 4];
        
        try
        {
            while (ws.State == System.Net.WebSockets.WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(
                    new ArraySegment<byte>(buffer), 
                    CancellationToken.None
                );

                if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Text)
                {
                    var jsonString = System.Text.Encoding.UTF8.GetString(
                        buffer, 
                        0, 
                        result.Count
                    );
                    var tick = System.Text.Json.JsonSerializer.Deserialize<UiTick>(jsonString);
                    
                    if (tick != null)
                    {
                        await writer.WriteAsync(tick);
                        app.Logger.LogInformation("Received tick from collector: {Tick}", tick);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "WebSocket error");
        }
        finally
        {
            if (ws.State == System.Net.WebSockets.WebSocketState.Open)
            {
                await ws.CloseAsync(
                    System.Net.WebSockets.WebSocketCloseStatus.NormalClosure,
                    "Closing",
                    CancellationToken.None
                );
            }
        }
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

// Health check endpoint
app.MapGet("/healthz", () => Results.Ok("OK"));

// SignalR hub
app.MapHub<MarketHub>("/hub/market");

// Запуск застосунку
app.Run("http://0.0.0.0:8080");