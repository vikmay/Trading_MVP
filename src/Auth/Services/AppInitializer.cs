using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Auth.Data;
using Auth.Models;
using OpenIddict.Abstractions;

namespace Auth.Services;

public class AppInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AppInitializer> _logger;

    public AppInitializer(IServiceProvider serviceProvider, ILogger<AppInitializer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var services = scope.ServiceProvider;

        try
        {
            // Initialize database
            var context = services.GetRequiredService<AuthDbContext>();
            await context.Database.EnsureCreatedAsync(cancellationToken);

            // Initialize OpenIddict applications
            await InitializeOpenIddictApplicationsAsync(services, cancellationToken);

            // Initialize users and roles
            await InitializeUsersAndRolesAsync(services);

            _logger.LogInformation("Application initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during application initialization");
        }
    }

    private async Task InitializeOpenIddictApplicationsAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var manager = services.GetRequiredService<IOpenIddictApplicationManager>();

        // Trading Web UI Client
        if (await manager.FindByClientIdAsync("trading-webui", cancellationToken) is null)
        {
            await manager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "trading-webui",
                ClientSecret = "trading-webui-secret",
                ConsentType = OpenIddictConstants.ConsentTypes.Explicit,
                DisplayName = "Trading Web UI",
                ClientType = OpenIddictConstants.ClientTypes.Confidential,
                PostLogoutRedirectUris =
                {
                    new Uri("http://localhost/signout-callback-oidc"),
                    new Uri("http://localhost:3000/signout-callback-oidc")
                },
                RedirectUris =
                {
                    new Uri("http://localhost/signin-oidc"),
                    new Uri("http://localhost:3000/signin-oidc")
                },
                Requirements =
                {
                    OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange
                },
                Permissions =
                {
                    OpenIddictConstants.Permissions.Endpoints.Authorization,
                    OpenIddictConstants.Permissions.Endpoints.Logout,
                    OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                    OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                    OpenIddictConstants.Permissions.ResponseTypes.Code,
                    OpenIddictConstants.Permissions.Scopes.Email,
                    OpenIddictConstants.Permissions.Scopes.Profile,
                    OpenIddictConstants.Permissions.Scopes.Roles,
                    "trading-api"
                }
            }, cancellationToken);
        }

        // Initialize scopes
        var scopeManager = services.GetRequiredService<IOpenIddictScopeManager>();
        if (await scopeManager.FindByNameAsync("trading-api", cancellationToken) is null)
        {
            await scopeManager.CreateAsync(new OpenIddictScopeDescriptor
            {
                Name = "trading-api",
                DisplayName = "Trading API Access",
                Description = "Access to trading platform API",
                Resources = { "trading-api" }
            }, cancellationToken);
        }
    }

    private async Task InitializeUsersAndRolesAsync(IServiceProvider services)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        // Create roles
        string[] roleNames = { "Admin", "Trader", "Viewer" };
        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
                _logger.LogInformation("Created role: {RoleName}", roleName);
            }
        }

        // Create default admin user
        var adminEmail = "admin@tradingmvp.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FirstName = "Admin",
                LastName = "User",
                Role = "Admin"
            };

            var result = await userManager.CreateAsync(adminUser, "Admin123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                _logger.LogInformation("Created admin user: {Email}", adminEmail);
            }
            else
            {
                _logger.LogError("Failed to create admin user: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        // Create default trader user  
        var traderEmail = "trader@tradingmvp.com";
        var traderUser = await userManager.FindByEmailAsync(traderEmail);
        if (traderUser == null)
        {
            traderUser = new ApplicationUser
            {
                UserName = traderEmail,
                Email = traderEmail,
                EmailConfirmed = true,
                FirstName = "Trader",
                LastName = "User",
                Role = "Trader"
            };

            var result = await userManager.CreateAsync(traderUser, "Trader123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(traderUser, "Trader");
                _logger.LogInformation("Created trader user: {Email}", traderEmail);
            }
            else
            {
                _logger.LogError("Failed to create trader user: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
