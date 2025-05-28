using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Common;                 // RawTick
using Gateway.Services;       // IPriceCache

namespace Gateway.Hubs;

public sealed class MarketHub : Hub
{
    private readonly ILogger<MarketHub> _log;
    private readonly IPriceCache _cache;

    public MarketHub(IPriceCache cache, ILogger<MarketHub> log)
    {
        _cache = cache;
        _log = log;
    }

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

    // 🚀  Browser calls this right after (re)connect
    public IEnumerable<RawTick> NeedTicksSince(long lastSeq)
        => _cache.GetSince(lastSeq);
}
