using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Gateway.Hubs;

public sealed class MarketHub : Hub
{
    private readonly ILogger<MarketHub> _log;
    public MarketHub(ILogger<MarketHub> log) => _log = log;

    public override Task OnConnectedAsync()
    {
        _log.LogInformation("Client connected: {Id}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? ex)
    {
        _log.LogInformation("Client disconnected: {Id}", Context.ConnectionId);
        return base.OnDisconnectedAsync(ex);
    }
}
