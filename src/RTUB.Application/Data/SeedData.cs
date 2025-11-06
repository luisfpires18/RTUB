using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Data;

/// <summary>
/// Seeds the database with initial data such as administrator role and user.
/// </summary>
public static partial class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        // Try to get environment from multiple sources
        var environment = configuration["ASPNETCORE_ENVIRONMENT"] 
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") 
            ?? "Development";
        var isProduction = environment.Equals("Production", StringComparison.OrdinalIgnoreCase);

        // Ensure roles exist (OWNER, ADMIN, and MEMBER)
        string[] roles = new[] { "Owner", "Admin", "Member" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // Check if there's any OWNER already
        var existingOwners = await userManager.GetUsersInRoleAsync("Owner");
        if (existingOwners.Any())
        {
            // Skip seed if owner already exists
            return;
        }

        // Create default OWNER user with username "rtub"
        var defaultUsername = configuration["AdminUser:Username"] ?? "rtub";
        var defaultEmail = configuration["AdminUser:Email"] ?? "admin@rtub.pt";
        var adminPassword = configuration["AdminUser:Password"] ?? "Admin123!";

        var ownerUser = await userManager.FindByNameAsync(defaultUsername);
        if (ownerUser == null)
        {
            ownerUser = new ApplicationUser
            {
                UserName = defaultUsername,
                Email = defaultEmail,
                EmailConfirmed = true,
                FirstName = "Admin",
                LastName = "RTUB",
                Nickname = "Admin",
                PhoneContact = "000000000",
            };

            var result = await userManager.CreateAsync(ownerUser, adminPassword);
            if (result.Succeeded)
            {
                // Owner gets both OWNER and ADMIN roles for access to all features
                await userManager.AddToRoleAsync(ownerUser, "Owner");
                await userManager.AddToRoleAsync(ownerUser, "Admin");
            }
            else
            {
                throw new Exception($"Unable to create owner user: {string.Join(", ", result.Errors)}");
            }
        }

        // Skip everything else if production
        if (isProduction)
        {
            return;
        }

        await SeedSlideshowsAsync(dbContext);

        await SeedLabelsAsync(dbContext);

        await SeedMembersAsync(dbContext, userManager);

        await SeedEventsAsync(dbContext);

        await SeedMusicAsync(dbContext);

        await SeedInventoryAsync(dbContext);
    }
}