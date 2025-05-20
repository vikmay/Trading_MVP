// src/Collector/Worker.cs  ────────────────────────────────────────────────────
using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Common;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Collector
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _cfg;
        private ClientWebSocket _ws = new();
        private readonly IProducer<string, string> _producer;

        private const string TOPIC_NAME = "ticks.raw";

        public Worker(ILogger<Worker> logger, IConfiguration cfg)
        {
            _logger = logger;
            _cfg = cfg;

            // ────── Kafka producer ──────
            var pcfg = new ProducerConfig
            {
                BootstrapServers = _cfg["KAFKA_BOOTSTRAP_SERVERS"] ?? "kafka:9092",
                ClientId = "collector",
                Acks = Acks.All,
                EnableIdempotence = true
            };
            _producer = new ProducerBuilder<string, string>(pcfg).Build();

            _logger.LogInformation("Kafka producer ready (bootstrap-servers = {Servers})",
                                   pcfg.BootstrapServers);
        }

        protected override async Task ExecuteAsync(CancellationToken stop)
        {
            // ────── figure out which WS we’re supposed to hit ──────
            var wsUrl = _cfg["EXCHANGE_WS_URL"] ??
                        "ws://exchange_stub:8081/ws/ticker?symbol=BTCUSDT";

            _logger.LogInformation("Using Exchange WS URL: {Url}", wsUrl);

            _ws = await ConnectWithRetry(wsUrl, "Exchange WS", stop);

            var buffer = new byte[8 * 1024];        // 8 KB just in case

            while (!stop.IsCancellationRequested)
            {
                WebSocketReceiveResult res;
                int bytes = 0;

                try
                {
                    // ── read 1 complete frame (stub sends 1 JSON per frame)
                    res = await _ws.ReceiveAsync(buffer, stop);
                    bytes = res.Count;
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "WebSocket receive failed – reconnecting…");
                    _ws = await ConnectWithRetry(wsUrl, "Exchange WS", stop);
                    continue;
                }

                if (res.MessageType != WebSocketMessageType.Text) continue;

                var json = Encoding.UTF8.GetString(buffer, 0, bytes);

                // 🔎  **NEW**: see exactly what the stub sent
                _logger.LogInformation("⇢ WS payload: {Payload}", json);

                var tick = JsonSerializer.Deserialize<RawTick>(json);

                if (tick is null)
                {
                    _logger.LogWarning("Couldn’t deserialize payload – skipping");
                    continue;
                }

                await ProduceAsync(tick, stop);
            }

            await CleanupAsync();
        }

        // ────────────────────────────────────────────────────────────── helpers
        private async Task ProduceAsync(RawTick t, CancellationToken ct)
        {
            try
            {
                var dr = await _producer.ProduceAsync(
                             TOPIC_NAME,
                             new Message<string, string>
                             {
                                 Key = t.Symbol,
                                 Value = JsonSerializer.Serialize(t)
                             }, ct);

                _logger.LogInformation("✔ Produced raw tick {Sym} @ {Ts} → {Topic}/{Part}/{Off}",
                                       t.Symbol, t.TsMs, dr.Topic, dr.Partition, dr.Offset);
            }
            catch (ProduceException<string, string> ex)
            {
                _logger.LogError(ex,
                    "❌  Kafka produce failed – message will be retried by caller");
                throw;
            }
        }

        private async Task<ClientWebSocket> ConnectWithRetry(
            string uri, string name, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var ws = new ClientWebSocket();
                    await ws.ConnectAsync(new Uri(uri), ct);
                    _logger.LogInformation("✅  Connected to {Name}", name);
                    return ws;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Cannot connect to {Name}. Retrying in 2 s…", name);
                    await Task.Delay(2000, ct);
                }
            }
            throw new OperationCanceledException();
        }

        private async Task CleanupAsync()
        {
            try
            {
                if (_ws.State == WebSocketState.Open)
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure,
                                         "shutdown", CancellationToken.None);
            }
            catch { /* ignore */ }

            _producer.Flush();   // block ≤ 5 s by default
            _producer.Dispose();
        }
    }
}
