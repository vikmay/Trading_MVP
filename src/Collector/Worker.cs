using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Common;

namespace Collector
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private ClientWebSocket _exchangeWs;
        private ClientWebSocket _gatewayWs;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
            _exchangeWs = new ClientWebSocket();
            _gatewayWs = new ClientWebSocket();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                // Підключаємося до ExchangeStub
                _exchangeWs = await ConnectWithRetry(
                    "ws://exchange_stub:8081/ws/ticker?symbol=BTCUSDT",
                    "ExchangeStub",
                    stoppingToken
                );

                // Підключаємося до Gateway з retry
                _gatewayWs = await ConnectWithRetry(
                    "ws://gateway:8080/ws/collector",
                    "Gateway",
                    stoppingToken
                );

                var buffer = new byte[1024 * 4];
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        // Отримуємо тік від ExchangeStub
                        var result = await _exchangeWs.ReceiveAsync(
                            new ArraySegment<byte>(buffer),
                            stoppingToken
                        );

                        if (result.MessageType == WebSocketMessageType.Text)
                        {
                            var jsonString = System.Text.Encoding.UTF8.GetString(
                                buffer,
                                0,
                                result.Count
                            );
                            var rawTick = JsonSerializer.Deserialize<RawTick>(jsonString);

                            if (rawTick != null)
                            {
                                // Трансформуємо в UiTick
                                var mid = (rawTick.Bid + rawTick.Ask) / 2;
                                var spreadPct = (rawTick.Ask - rawTick.Bid) / mid * 100;
                                var uiTick = new UiTick(
                                    rawTick.Symbol,
                                    rawTick.Bid,
                                    rawTick.Ask,
                                    mid,
                                    spreadPct,
                                    rawTick.TsMs
                                );

                                // Відправляємо в Gateway через WebSocket
                                var tickJson = JsonSerializer.Serialize(uiTick);
                                var tickBytes = System.Text.Encoding.UTF8.GetBytes(tickJson);
                                await _gatewayWs.SendAsync(
                                    new ArraySegment<byte>(tickBytes),
                                    WebSocketMessageType.Text,
                                    true,
                                    stoppingToken
                                );
                                _logger.LogInformation("Sent tick to Gateway: {Tick}", uiTick);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing tick");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Worker stopped");
            }
            finally
            {
                if (_exchangeWs.State == WebSocketState.Open)
                {
                    await _exchangeWs.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Worker stopped",
                        CancellationToken.None
                    );
                }

                if (_gatewayWs.State == WebSocketState.Open)
                {
                    await _gatewayWs.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Worker stopped",
                        CancellationToken.None
                    );
                }
            }
        }

        private async Task<ClientWebSocket> ConnectWithRetry(
            string uri,
            string name,
            CancellationToken stoppingToken
        )
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var ws = new ClientWebSocket();
                    await ws.ConnectAsync(new Uri(uri), stoppingToken);
                    _logger.LogInformation("Connected to {Name} WebSocket", name);
                    return ws;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to connect to {Name}, retrying in 2s...",
                        name
                    );
                    await Task.Delay(2000, stoppingToken);
                }
            }
            throw new OperationCanceledException();
        }
    }
}