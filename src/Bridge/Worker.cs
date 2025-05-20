using System.Net;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using RabbitMQ.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Bridge;

public record UiTick(
    string Symbol,
    decimal Bid,
    decimal Ask,
    decimal Mid,
    decimal SpreadPct,
    long TsMs,
    string ExchangeId
);

public class BridgeWorker : BackgroundService
{
    private readonly ILogger<BridgeWorker> _logger;
    private readonly string _kafkaBootstrap;
    private readonly string _topicIn;
    private readonly string _rabbitHost;
    private readonly string _exchangeName;
    private IConsumer<string, string>? _consumer;
    private IConnection? _connection;
    private IModel? _channel;
    private HttpListener? _httpListener;

    public BridgeWorker(ILogger<BridgeWorker> logger)
    {
        _logger = logger;
        _kafkaBootstrap = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS") ?? "kafka:9092";
        _topicIn = Environment.GetEnvironmentVariable("TOPIC_IN") ?? "ticks.norm";
        _rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "rabbitmq";
        _exchangeName = Environment.GetEnvironmentVariable("RABBIT_EXCHANGE") ?? "market-data";
    }

    private async Task<bool> InitializeRabbitMQWithRetry(CancellationToken stoppingToken)
    {
        var maxRetries = 10;
        var delay = TimeSpan.FromSeconds(5);

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                _logger.LogInformation("Attempting to connect to RabbitMQ at {Host} (attempt {Attempt}/{Max})...",
                    _rabbitHost, i + 1, maxRetries);

                var factory = new ConnectionFactory { HostName = _rabbitHost };
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                _channel.ExchangeDeclare(
                    exchange: _exchangeName,
                    type: ExchangeType.Topic,
                    durable: true
                );

                _channel.BasicQos(0, 500, false);

                _logger.LogInformation("Successfully connected to RabbitMQ!");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to connect to RabbitMQ (attempt {Attempt}/{Max}). Retrying in {Delay}s. Error: {Error}",
                    i + 1, maxRetries, delay.TotalSeconds, ex.Message);

                if (i == maxRetries - 1)
                {
                    _logger.LogError("Could not connect to RabbitMQ after {Max} attempts.", maxRetries);
                    return false;
                }

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Operation was canceled while waiting to retry RabbitMQ connection.");
                    return false;
                }
            }
        }

        return false;
    }

    private async Task InitializeKafkaWithRetry(CancellationToken stoppingToken)
    {
        var maxRetries = 10;
        var delay = TimeSpan.FromSeconds(5);

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                _logger.LogInformation("Attempting to connect to Kafka at {Bootstrap} (attempt {Attempt}/{Max})...",
                    _kafkaBootstrap, i + 1, maxRetries);

                var config = new ConsumerConfig
                {
                    BootstrapServers = _kafkaBootstrap,
                    GroupId = "bridge",
                    AutoOffsetReset = AutoOffsetReset.Earliest,
                    EnableAutoCommit = true,
                    EnableAutoOffsetStore = false
                };

                _consumer = new ConsumerBuilder<string, string>(config).Build();
                _consumer.Subscribe(_topicIn);

                _logger.LogInformation("Successfully connected to Kafka!");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to connect to Kafka (attempt {Attempt}/{Max}). Retrying in {Delay}s. Error: {Error}",
                    i + 1, maxRetries, delay.TotalSeconds, ex.Message);

                if (i == maxRetries - 1)
                {
                    throw new Exception($"Could not connect to Kafka after {maxRetries} attempts.", ex);
                }

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    throw new OperationCanceledException("Operation was canceled while waiting to retry Kafka connection.");
                }
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initialize health check endpoint
        _httpListener = new HttpListener();
        _httpListener.Prefixes.Add("http://*:8083/healthz/");
        _httpListener.Start();

        // Start health check endpoint
        var healthCheckTask = Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested && _httpListener.IsListening)
            {
                try
                {
                    var context = await _httpListener.GetContextAsync();
                    var response = context.Response;
                    var buffer = Encoding.UTF8.GetBytes("OK");
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, stoppingToken);
                    response.Close();
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Error in health check endpoint");
                }
            }
        }, stoppingToken);

        try
        {
            // Initialize connections with retry
            await InitializeKafkaWithRetry(stoppingToken);
            if (!await InitializeRabbitMQWithRetry(stoppingToken))
            {
                throw new Exception("Failed to initialize RabbitMQ connection");
            }

            _logger.LogInformation("Bridge service started successfully");

            // Main processing loop
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = _consumer!.Consume(stoppingToken);
                    if (consumeResult?.Message?.Value == null) continue;

                    var tick = JsonSerializer.Deserialize<UiTick>(consumeResult.Message.Value);
                    if (tick == null)
                    {
                        _logger.LogWarning("Failed to deserialize message: {Message}", consumeResult.Message.Value);
                        continue;
                    }

                    var body = Encoding.UTF8.GetBytes(consumeResult.Message.Value);

                    _channel!.BasicPublish(
                        exchange: "",
                        routingKey: "market.ticks",
                        basicProperties: null,
                        body: body
                    );

                    _consumer.StoreOffset(consumeResult);

                    _logger.LogInformation(
                        "Published tick for {Symbol}. Bid: {Bid}, Ask: {Ask}, Exchange: {Exchange}",
                        tick.Symbol, tick.Bid, tick.Ask, tick.ExchangeId
                    );
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Error consuming from Kafka");
                    await Task.Delay(1000, stoppingToken); // Back off on error
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error processing message");
                    await Task.Delay(1000, stoppingToken); // Back off on error
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Bridge service is shutting down...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Bridge service");
            throw;
        }
        finally
        {
            try
            {
                _consumer?.Close();
                _channel?.Close();
                _connection?.Close();
                _httpListener?.Stop();
                _httpListener?.Close();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup");
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _consumer?.Close();
            _channel?.Close();
            _connection?.Close();
            _httpListener?.Stop();
            _httpListener?.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during service shutdown");
        }

        await base.StopAsync(cancellationToken);
    }
}

public class Program
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                services.AddHostedService<BridgeWorker>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            });
}