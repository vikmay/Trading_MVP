using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Common;
using Gateway.Services;

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
        var connectionId = Context.ConnectionId;
        var userInfo = Context.User?.Identity?.IsAuthenticated == true
            ? $"User: {Context.User.Identity.Name}"
            : "Anonymous";

        _log.LogInformation("Client connected: {Id} ({UserInfo})", connectionId, userInfo);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? ex)
    {
        _log.LogInformation("Client disconnected: {Id}", Context.ConnectionId);
        return base.OnDisconnectedAsync(ex);
    }

    // Public endpoint - no authentication required
    public IEnumerable<RawTick> NeedTicksSince(long lastSeq)
        => _cache.GetSince(lastSeq);

    // Protected endpoint - requires authentication
    [Authorize]
    public async Task JoinUserGroup()
    {
        var userId = Context.User?.Identity?.Name;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            _log.LogInformation("User {UserId} joined their private group", userId);
        }
    }

    // Protected endpoint - requires authentication
    [Authorize]
    public async Task LeaveUserGroup()
    {
        var userId = Context.User?.Identity?.Name;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
            _log.LogInformation("User {UserId} left their private group", userId);
        }
    }

    // Protected endpoint - get user info
    [Authorize]
    public object GetUserInfo()
    {
        return new
        {
            UserId = Context.User?.Identity?.Name,
            IsAuthenticated = Context.User?.Identity?.IsAuthenticated ?? false,
            ConnectionId = Context.ConnectionId
        };
    }
}
