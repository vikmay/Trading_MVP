using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Common;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Collector
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;
        private ClientWebSocket _exchangeWs;
        private IProducer<string, string> _producer;
        private const string TOPIC_NAME = "ticks.raw";

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _exchangeWs = new ClientWebSocket();

            // Initialize Kafka producer
            var producerConfig = new ProducerConfig
            {
                BootstrapServers = _configuration["KAFKA_BOOTSTRAP_SERVERS"] ?? "kafka:9092",
                ClientId = "collector",
                // Optional: Add these for better reliability
                Acks = Acks.All,                    // Wait for all replicas
                EnableIdempotence = true,           // Prevent duplicates
                MessageSendMaxRetries = 3,          // Retry on temporary failures
                RetryBackoffMs = 1000              // Wait between retries
            };

            _producer = new ProducerBuilder<string, string>(producerConfig).Build();
            _logger.LogInformation("Kafka producer initialized with bootstrap servers: {Servers}", 
                producerConfig.BootstrapServers);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                // Connect to ExchangeStub
                _exchangeWs = await ConnectWithRetry(
                    "ws://exchange_stub:8080/ws/ticker?symbol=BTCUSDT",
                    "ExchangeStub",
                    stoppingToken
                );

                var buffer = new byte[1024 * 4];
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        // Receive tick from ExchangeStub
                        var result = await _exchangeWs.ReceiveAsync(
                            new ArraySegment<byte>(buffer),
                            stoppingToken
                        );

                        if (result.MessageType == WebSocketMessageType.Text)
                        {
                            var jsonString = Encoding.UTF8.GetString(
                                buffer,
                                0,
                                result.Count
                            );
                            var rawTick = JsonSerializer.Deserialize<RawTick>(jsonString);

                            if (rawTick != null)
                            {
                                // Produce to Kafka
                                await ProduceToKafkaAsync(rawTick, stoppingToken);
                                _logger.LogInformation("Produced raw tick to Kafka: {Symbol} @ {Timestamp}", 
                                    rawTick.Symbol, rawTick.TsMs);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing tick");
                        // Optional: Add delay before retry
                        await Task.Delay(1000, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Worker stopped");
            }
            finally
            {
                await CleanupAsync();
            }
        }

        private async Task ProduceToKafkaAsync(RawTick tick, CancellationToken cancellationToken)
        {
            try
            {
                var message = new Message<string, string>
                {
                    Key = tick.Symbol,  // Using symbol as partition key ensures order per symbol
                    Value = JsonSerializer.Serialize(tick)
                };

                var deliveryResult = await _producer.ProduceAsync(
                    TOPIC_NAME,
                    message,
                    cancellationToken
                );

                if (deliveryResult.Status == PersistenceStatus.Persisted)
                {
                    _logger.LogDebug("Message delivered to {Topic} [{Partition}] at offset {Offset}",
                        deliveryResult.Topic,
                        deliveryResult.Partition,
                        deliveryResult.Offset);
                }
            }
            catch (ProduceException<string, string> ex)
            {
                _logger.LogError(ex, "Failed to produce message to Kafka");
                throw; // Let the caller handle retry logic
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

        private async Task CleanupAsync()
        {
            try
            {
                if (_exchangeWs.State == WebSocketState.Open)
                {
                    await _exchangeWs.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Worker stopped",
                        CancellationToken.None
                    );
                }

                // Flush any remaining messages
                _producer.Flush(TimeSpan.FromSeconds(5));
                _producer.Dispose();
                
                _logger.LogInformation("Cleanup completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup");
            }
        }
    }
}