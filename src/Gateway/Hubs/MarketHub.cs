using Microsoft.AspNetCore.SignalR;
using Common;

namespace Gateway.Hubs
{
    public class MarketHub : Hub
    {
        private readonly ILogger<MarketHub> _logger;

        public MarketHub(ILogger<MarketHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}