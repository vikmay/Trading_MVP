using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Auth.Data;
using Auth.Models;

namespace Auth.Services;

public class DatabaseInitializerService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseInitializerService> _logger;

    public DatabaseInitializerService(IServiceProvider serviceProvider, ILogger<DatabaseInitializerService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();

        var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        try
        {
            // Ensure database is created
            await context.Database.EnsureCreatedAsync(cancellationToken);

            // Seed roles
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

            _logger.LogInformation("Database initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during database initialization");
            // Don't throw to allow the application to start
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
