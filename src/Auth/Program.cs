using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Auth.Data;
using Auth.Models;
using Auth.Services;
using OpenIddict.Validation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    // Fallback to in-memory database for development
    builder.Services.AddDbContext<AuthDbContext>(options =>
    {
        options.UseInMemoryDatabase("AuthDb");
        options.UseOpenIddict();
    });
}
else
{
    // Use PostgreSQL
    builder.Services.AddDbContext<AuthDbContext>(options =>
    {
        options.UseNpgsql(connectionString);
        options.UseOpenIddict();
    });
}

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;

    // User settings
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<AuthDbContext>()
.AddDefaultTokenProviders();

// Configure Identity to use cookies
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/account/login";
    options.LogoutPath = "/account/logout";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(50);
});

// Add Google Authentication
var googleClientId = builder.Configuration["Google:ClientId"] ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
var googleClientSecret = builder.Configuration["Google:ClientSecret"] ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET");

if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
{
    builder.Services.AddAuthentication()
        .AddGoogle(options =>
        {
            options.ClientId = googleClientId;
            options.ClientSecret = googleClientSecret;
            options.CallbackPath = "/signin-google";
        });
}

// OpenIddict
builder.Services.AddOpenIddict()
    // Register the OpenIddict core components.
    .AddCore(options =>
    {
        // Configure OpenIddict to use the Entity Framework Core stores and models.
        options.UseEntityFrameworkCore()
               .UseDbContext<AuthDbContext>();
    })
    // Register the OpenIddict server components.
    .AddServer(options =>
    {
        // Enable the authorization, logout, token and userinfo endpoints.
        options.SetAuthorizationEndpointUris("/connect/authorize")
               .SetLogoutEndpointUris("/connect/logout")
               .SetTokenEndpointUris("/connect/token")
               .SetUserinfoEndpointUris("/connect/userinfo");

        // Mark the "email", "profile" and "roles" scopes as supported scopes.
        options.RegisterScopes(OpenIddict.Abstractions.OpenIddictConstants.Scopes.Email,
                               OpenIddict.Abstractions.OpenIddictConstants.Scopes.Profile,
                               OpenIddict.Abstractions.OpenIddictConstants.Scopes.Roles,
                               "trading-api");

        // Note: this sample only uses the authorization code flow but you can enable
        // the other flows if you need to support implicit, password or client credentials.
        options.AllowAuthorizationCodeFlow()
               .AllowRefreshTokenFlow();

        // Register the signing and encryption credentials.
        options.AddDevelopmentEncryptionCertificate()
               .AddDevelopmentSigningCertificate();

        // Register the ASP.NET Core host and configure the ASP.NET Core-specific options.
        options.UseAspNetCore()
               .EnableAuthorizationEndpointPassthrough()
               .EnableLogoutEndpointPassthrough()
               .EnableTokenEndpointPassthrough()
               .EnableUserinfoEndpointPassthrough()
               .EnableStatusCodePagesIntegration();
    })
    // Register the OpenIddict validation components.
    .AddValidation(options =>
    {
        // Import the configuration from the local OpenIddict server instance.
        options.UseLocalServer();

        // Register the ASP.NET Core host.
        options.UseAspNetCore();
    });

// Register the worker responsible for seeding the database.
builder.Services.AddHostedService<OpenIddictInitializer>();
builder.Services.AddHostedService<DatabaseInitializerService>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebUI", cors =>
    {
        cors.WithOrigins(
                "http://localhost",
                "http://localhost:80",
                "http://localhost:5173",
                "http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});


var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Initialize database and seed data
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    try
    {
        // Create database if it doesn't exist
        await context.Database.EnsureCreatedAsync();

        // Seed roles
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        }
        if (!await roleManager.RoleExistsAsync("User"))
        {
            await roleManager.CreateAsync(new IdentityRole("User"));
        }

        // Seed users
        await SeedUsersAsync(userManager);

        Console.WriteLine("✅ Database initialized and seeded successfully!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error initializing database: {ex.Message}");
        // Continue startup even if seeding fails
    }
}

app.UseCors("AllowWebUI");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health check endpoint
app.MapGet("/healthz", () => Results.Ok(new { Status = "Healthy", Service = "Auth", Timestamp = DateTime.UtcNow }));

app.Run();

// Helper method to seed users
static async Task SeedUsersAsync(UserManager<ApplicationUser> userManager)
{
    var testUsers = new[]
    {
        new { Email = "admin@trading.com", Password = "Admin123!", Role = "Admin", FirstName = "Admin", LastName = "User" },
        new { Email = "trader@trading.com", Password = "Trader123!", Role = "User", FirstName = "John", LastName = "Trader" },
        new { Email = "demo@trading.com", Password = "Demo123!", Role = "User", FirstName = "Demo", LastName = "User" }
    };

    foreach (var testUser in testUsers)
    {
        var existingUser = await userManager.FindByEmailAsync(testUser.Email);
        if (existingUser == null)
        {
            var user = new ApplicationUser
            {
                UserName = testUser.Email,
                Email = testUser.Email,
                EmailConfirmed = true,
                FirstName = testUser.FirstName,
                LastName = testUser.LastName
            };

            var result = await userManager.CreateAsync(user, testUser.Password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, testUser.Role);
                Console.WriteLine($"✅ Created user: {testUser.Email} with role: {testUser.Role}");
            }
            else
            {
                Console.WriteLine($"❌ Failed to create user {testUser.Email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }
    }
}
