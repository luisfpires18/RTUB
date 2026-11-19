using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Data;

public static partial class SeedData
{
    private static async Task<ApplicationUser> EnsureOwnerAsync(
        IConfiguration configuration, UserManager<ApplicationUser> userManager)
    {
        var defaultUsername = configuration["AdminUser:Username"] ?? "rtub";
        var defaultEmail = configuration["AdminUser:Email"] ?? "admin@rtub.pt";
        var adminPassword = configuration["AdminUser:Password"];

        var ownerUser = await userManager.FindByNameAsync(defaultUsername);
        if (ownerUser == null)
        {
            // Fail closed: never bootstrap a privileged account with a password that is
            // absent, blank, or one of the documented placeholders. There is deliberately
            // no fallback default. The value itself is never logged.
            RequireSeedPassword(adminPassword, "AdminUser:Password", "bootstrap the Owner account");

            ownerUser = new ApplicationUser
            {
                UserName = defaultUsername,
                Email = defaultEmail,
                EmailConfirmed = true,
                FirstName = "Luís",
                LastName = "Pires",
                Nickname = "Jeans",
                PhoneNumber = "936854524",
                Positions = new List<Position>(),
                Categories = new List<MemberCategory> { MemberCategory.Tuno },
                IsRetired = MemberActivityStatusEvaluator.InitialIsRetired(new[] { MemberCategory.Tuno }),
                YearLeitao = 2013,
                YearCaloiro = 2017,
                YearTuno = 2019,
                Degree = "Engenharia Informática",
                City = "Bragança",
                Subscribed = true,
            };

            var result = await userManager.CreateAsync(ownerUser, adminPassword!);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(ownerUser, "Owner");
                await userManager.AddToRoleAsync(ownerUser, "Admin");
            }
            else
            {
                throw new Exception($"Unable to create owner user: {string.Join(", ", result.Errors)}");
            }
        }

        return ownerUser;
    }

    /// <summary>
    /// Throws unless <paramref name="password"/> is a real, externally supplied secret. Each
    /// caller checks only on the path that actually creates users, so a database that needs no
    /// seeding still starts without any of these settings configured.
    /// </summary>
    private static void RequireSeedPassword(string? password, string configKey, string what)
    {
        if (!string.IsNullOrWhiteSpace(password) && !SeedPasswordPlaceholders.Contains(password))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Cannot {what}: the configuration value '{configKey}' (environment variable " +
            $"'{configKey.Replace(":", "__", StringComparison.Ordinal)}') is missing, blank, or " +
            "still set to a placeholder. Supply a real password from a secure configuration " +
            "source before seeding. There is no default password.");
    }

    private static readonly HashSet<string> SeedPasswordPlaceholders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "your-admin-password",
            "changeme",
            "change-me",
            "password",
        };
}
