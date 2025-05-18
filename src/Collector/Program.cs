using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading.Channels;
using Common;
using Collector;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        // Створюємо канал для передачі UiTick
        var channel = Channel.CreateUnbounded<UiTick>();
        services.AddSingleton(channel.Reader);
        services.AddSingleton(channel.Writer);
        
        // Додаємо наш Worker
        services.AddHostedService<Worker>();
    });

var host = builder.Build();
await host.RunAsync();