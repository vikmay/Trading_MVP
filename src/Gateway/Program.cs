using Gateway.Hubs;
using Gateway.Workers;
using Gateway.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "TradingMvpSuperSecretKeyForJWTTokenGeneration2024!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "TradingMvpAuth";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "TradingMvpClient";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtSecret)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };    // Handle JWT in SignalR
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            // Log the attempt
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("OnMessageReceived: Path={Path}, TokenPresent={TokenPresent}", path, !string.IsNullOrEmpty(accessToken));

            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hub"))
            {
                context.Token = accessToken;
                logger.LogInformation("JWT Token set from query string for SignalR connection");
            }
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            // Log successful token validation
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var userId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var email = context.Principal?.FindFirst(ClaimTypes.Email)?.Value;
            logger.LogInformation("JWT Token validated for user: {UserId}, Email: {Email}", userId, email);
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            // Log authentication failures
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError("JWT Authentication failed: {Error}", context.Exception.Message);
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// CORS configuration
builder.Services.AddCors(opts =>
{
    opts.AddPolicy("CorsPolicy", cors =>
    {
        cors
            .WithOrigins(
                "http://localhost",
                "http://localhost:80",
                "http://localhost:5173") // Vite dev server
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddSignalR();
builder.Services.AddSingleton<IPriceCache, PriceCache>();
builder.Services.AddHostedService<GatewayWorker>();

var app = builder.Build();

// Middleware pipeline - order is critical!
app.UseRouting();
app.UseCors("CorsPolicy");
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<MarketHub>("/hub/market");

// Test endpoint to verify JWT authentication
app.MapGet("/api/test", (HttpContext context) =>
{
    var user = context.User;
    var isAuthenticated = user?.Identity?.IsAuthenticated ?? false;
    var userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var email = user?.FindFirst(ClaimTypes.Email)?.Value;

    return new
    {
        IsAuthenticated = isAuthenticated,
        UserId = userId,
        Email = email,
        Claims = user?.Claims?.Select(c => new { c.Type, c.Value }).ToArray()
    };
}).RequireAuthorization();

// Health check endpoint
app.MapGet("/healthz", () => Results.Ok(new { Status = "Healthy", Service = "Gateway", Timestamp = DateTime.UtcNow }));

app.Run();
