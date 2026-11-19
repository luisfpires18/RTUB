using Microsoft.AspNetCore.Identity;

namespace RTUB.Integration.Tests;

/// <summary>
/// <see cref="CookieValidationFactory"/> — same database, same SQL recorder — with the security
/// stamp validator's clock pushed past <see cref="SecurityStampValidatorOptions.ValidationInterval"/>,
/// so every request is one on which Identity's refresh falls due.
///
/// <para>The clock is moved rather than the interval, so the tests exercise the real, unmodified
/// 30-minute framework default instead of a value invented for them.</para>
/// </summary>
public class RefreshingCookieFactory : CookieValidationFactory
{
    /// <summary>Comfortably past the 30-minute default, and nothing else reads this clock.</summary>
    private static readonly TimeSpan PastTheValidationInterval = TimeSpan.FromMinutes(31);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // Runs after the application's own registrations, and PostConfigureSecurityStampValidatorOptions
        // only fills TimeProvider when it is still null, so this value is the one that survives.
        builder.ConfigureServices(services =>
            services.Configure<SecurityStampValidatorOptions>(
                options => options.TimeProvider = new OffsetTimeProvider(PastTheValidationInterval)));
    }

    private sealed class OffsetTimeProvider(TimeSpan offset) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + offset;
    }
}
