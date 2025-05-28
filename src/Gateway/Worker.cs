using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Common;                  // RawTick
using Gateway.Services;        // IPriceCache

namespace Gateway.Workers;

/// <summary>
/// Consumes normalised ticks from RabbitMQ and pushes them to every
/// connected WebSocket client via <see cref="MarketHub"/>,
/// while caching them for replay.
/// </summary>
public sealed class GatewayWorker : BackgroundService
{
    private readonly ILogger<GatewayWorker> _log;
    private readonly IHubContext<Gateway.Hubs.MarketHub> _hub;
    private readonly IPriceCache _cache;    // 🔹 new
    private readonly IModel _ch;
    private readonly string _queue;

    public GatewayWorker(ILogger<GatewayWorker> log,
                         IConfiguration cfg,
                         IHubContext<Gateway.Hubs.MarketHub> hub,
                         IPriceCache cache)                               // 🔹 inject
    {
        _log = log;
        _hub = hub;
        _cache = cache;
        _queue = cfg["RABBIT_QUEUE"] ?? "market.ticks";

        var factory = new ConnectionFactory
        {
            HostName = cfg["RABBIT_HOST"] ?? "rabbitmq"
        };

        var conn = factory.CreateConnection();
        _ch = conn.CreateModel();

        _ch.QueueDeclare(queue: _queue,
                         durable: false,
                         exclusive: false,
                         autoDelete: false,
                         arguments: null);

        _log.LogInformation("✅ Connected to RabbitMQ, queue={Q}", _queue);
    }

    protected override Task ExecuteAsync(CancellationToken token)
    {
        var consumer = new EventingBasicConsumer(_ch);

        consumer.Received += async (_, ea) =>
        {
            if (token.IsCancellationRequested) return;

            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            var tick = JsonSerializer.Deserialize<RawTick>(json);

            if (tick is null) return;                // defensive

            _cache.Add(tick);                        // 🔹 store for replay

            await _hub.Clients.All.SendAsync("tick", tick, token);

            _log.LogDebug("--> tick forwarded seq={Seq}", tick.Seq);
        };

        _ch.BasicConsume(queue: _queue, autoAck: true, consumer: consumer);
        _log.LogInformation("📡  Started consuming from {Q}", _queue);

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _ch?.Close();
        base.Dispose();
    }
}
