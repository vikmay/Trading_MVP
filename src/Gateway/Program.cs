using Gateway.Hubs;                       // <— your hub namespace
using Gateway.Workers;                    // <— the RabbitMQ worker

var builder = WebApplication.CreateBuilder(args);

// 1️⃣  Tell ASP.NET about the front-end origins
builder.Services.AddCors(opts =>
{
    opts.AddPolicy("CorsPolicy", cors =>
    {
        cors
            .WithOrigins(
                "http://localhost",     // nginx / vite / dev server
                "http://localhost:80")  // explicit port, just to be safe
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();       // required by SignalR
    });
});

builder.Services.AddSignalR();          // or AddSignalR().AddJsonProtocol() …

// … the rest of your services …

var app = builder.Build();

// 2️⃣  Pipeline order matters!
app.UseRouting();
app.UseCors("CorsPolicy");              // ⬅ MUST be *after* UseRouting and *before* MapHub
// app.UseAuthentication();             // if you have it
// app.UseAuthorization();

app.MapHub<MarketHub>("/hub/market");   // SignalR endpoint

app.Run();
