using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Common;

namespace Normaliser
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _kafkaBootstrapServers;
        private readonly IConsumer<string, string> _consumer;
        private readonly IProducer<string, string> _producer;
        private const string INPUT_TOPIC = "ticks.raw";
        private const string OUTPUT_TOPIC = "ticks.norm";
        private const string CONSUMER_GROUP = "normaliser";
        private const string EXCHANGE_ID = "EXA"; // Example exchange identifier

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _kafkaBootstrapServers = _configuration["KAFKA_BOOTSTRAP_SERVERS"] ?? "kafka:9092";

            // Configure Consumer
            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = _kafkaBootstrapServers,
                GroupId = CONSUMER_GROUP,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false // Manual commit for better control
            };

            // Configure Producer
            var producerConfig = new ProducerConfig
            {
                BootstrapServers = _kafkaBootstrapServers,
                EnableIdempotence = true,
                Acks = Acks.All
            };

            _consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
            _producer = new ProducerBuilder<string, string>(producerConfig).Build();

            _logger.LogInformation("Normaliser initialized with bootstrap servers: {Servers}", 
                _kafkaBootstrapServers);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _consumer.Subscribe(INPUT_TOPIC);
                _logger.LogInformation("Subscribed to topic: {Topic}", INPUT_TOPIC);

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var consumeResult = _consumer.Consume(stoppingToken);

                        if (consumeResult?.Message == null) continue;

                        var rawTick = JsonSerializer.Deserialize<RawTick>(consumeResult.Message.Value);
                        
                        if (rawTick != null)
                        {
                            // Transform RawTick to UiTick
                            var uiTick = NormalizeTick(rawTick);

                            // Produce normalized tick
                            await ProduceNormalizedTickAsync(uiTick, stoppingToken);

                            // Commit offset only after successful production
                            _consumer.Commit(consumeResult);

                            _logger.LogInformation(
                                "Processed tick: {Symbol} Bid={Bid} Ask={Ask} Mid={Mid} Spread={Spread}%",
                                uiTick.Symbol, uiTick.Bid, uiTick.Ask, uiTick.Mid, uiTick.SpreadPct
                            );
                        }
                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogError(ex, "Error consuming message");
                        await Task.Delay(1000, stoppingToken); // Brief pause before retry
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message");
                        await Task.Delay(1000, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Normaliser stopping...");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in Normaliser");
                throw;
            }
            finally
            {
                await CleanupAsync();
            }
        }

        private UiTick NormalizeTick(RawTick rawTick)
        {
            var mid = (rawTick.Bid + rawTick.Ask) / 2;
            var spreadPct = (rawTick.Ask - rawTick.Bid) / mid * 100;

            return new UiTick(
                Symbol: rawTick.Symbol,
                Bid: rawTick.Bid,
                Ask: rawTick.Ask,
                Mid: mid,
                SpreadPct: spreadPct,
                TsMs: rawTick.TsMs
            );
        }

        private async Task ProduceNormalizedTickAsync(UiTick uiTick, CancellationToken cancellationToken)
        {
            try
            {
                var message = new Message<string, string>
                {
                    Key = uiTick.Symbol,
                    Value = JsonSerializer.Serialize(uiTick)
                };

                var deliveryResult = await _producer.ProduceAsync(
                    OUTPUT_TOPIC, 
                    message, 
                    cancellationToken
                );

                _logger.LogDebug(
                    "Produced normalized tick to {Topic} [{Partition}] at offset {Offset}",
                    deliveryResult.Topic,
                    deliveryResult.Partition,
                    deliveryResult.Offset
                );
            }
            catch (ProduceException<string, string> ex)
            {
                _logger.LogError(ex, "Failed to produce normalized tick");
                throw;
            }
        }

        private async Task CleanupAsync()
        {
            try
            {
                _consumer.Close(); // Triggers partition rebalance
                _consumer.Dispose();

                _producer.Flush(TimeSpan.FromSeconds(5));
                _producer.Dispose();

                _logger.LogInformation("Normaliser cleanup completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Normaliser cleanup");
            }
        }
    }
}