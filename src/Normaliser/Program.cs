using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Normaliser;

Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();   // <- registers your Worker
    })
    .Build()
    .Run();
