using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Auth.Models;

namespace Auth.Data;

public class AuthDbContext : IdentityDbContext<ApplicationUser>
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Seed default admin user
        var hasher = new PasswordHasher<ApplicationUser>();
        var adminUser = new ApplicationUser
        {
            Id = "1",
            UserName = "admin@trading.com",
            NormalizedUserName = "ADMIN@TRADING.COM",
            Email = "admin@trading.com",
            NormalizedEmail = "ADMIN@TRADING.COM",
            EmailConfirmed = true,
            FirstName = "System",
            LastName = "Administrator",
            Role = "Admin"
        };
        adminUser.PasswordHash = hasher.HashPassword(adminUser, "Admin123!");

        var traderUser = new ApplicationUser
        {
            Id = "2",
            UserName = "trader@trading.com",
            NormalizedUserName = "TRADER@TRADING.COM",
            Email = "trader@trading.com",
            NormalizedEmail = "TRADER@TRADING.COM",
            EmailConfirmed = true,
            FirstName = "Demo",
            LastName = "Trader",
            Role = "Trader"
        };
        traderUser.PasswordHash = hasher.HashPassword(traderUser, "Trader123!");

        builder.Entity<ApplicationUser>().HasData(adminUser, traderUser);
    }
}
