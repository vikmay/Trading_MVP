using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Common;

namespace Normaliser;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConsumer<string, string> _consumer;
    private readonly IProducer<string, string> _producer;

    private const string INPUT_TOPIC = "ticks.raw";
    private const string OUTPUT_TOPIC = "ticks.norm";
    private const string CONSUMER_GROUP = "normaliser";

    public Worker(ILogger<Worker> logger, IConfiguration cfg)
    {
        _logger = logger;

        var bootstrap = cfg["KAFKA_BOOTSTRAP_SERVERS"] ?? "kafka:9092";

        /* ---------- Kafka consumer ---------- */
        var cConf = new ConsumerConfig
        {
            BootstrapServers = bootstrap,
            GroupId = CONSUMER_GROUP,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };
        _consumer = new ConsumerBuilder<string, string>(cConf).Build();

        /* ---------- Kafka producer ---------- */
        var pConf = new ProducerConfig
        {
            BootstrapServers = bootstrap,
            EnableIdempotence = true,
            Acks = Acks.All
        };
        _producer = new ProducerBuilder<string, string>(pConf).Build();

        _logger.LogInformation("Normaliser wired to Kafka @ {Bootstrap}", bootstrap);
    }

    /* ------------------------------------------------------------------ */
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        _consumer.Subscribe(INPUT_TOPIC);
        _logger.LogInformation("Subscribed to {Topic}", INPUT_TOPIC);

        while (!stop.IsCancellationRequested)
        {
            try
            {
                var cr = _consumer.Consume(stop);
                if (cr?.Message == null) continue;

                var raw = JsonSerializer.Deserialize<RawTick>(cr.Message.Value);
                if (raw is null) continue;

                // ➜ convert with our extension method
                var ui = raw.ToUi();

                await ProduceAsync(ui, stop);
                _consumer.Commit(cr);

                _logger.LogInformation(
                    "Tick {Sym}  Mid={Mid:F2}  Spread={Spr:F2}%",
                    ui.Symbol, ui.Mid, ui.SpreadPct);
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Kafka consume error – retrying");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Normaliser processing error");
            }
        }
    }

    /* ------------------------------------------------------------------ */
    private Task ProduceAsync(UiTick ui, CancellationToken ct) =>
        _producer.ProduceAsync(
            OUTPUT_TOPIC,
            new Message<string, string>
            {
                Key = ui.Symbol,
                Value = JsonSerializer.Serialize(ui)
            },
            ct);

    /* ------------------------------------------------------------------ */
    public override Task StopAsync(CancellationToken ct)
    {
        _consumer.Close();
        _consumer.Dispose();

        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();

        _logger.LogInformation("Normaliser stopped cleanly");
        return base.StopAsync(ct);
    }
}
