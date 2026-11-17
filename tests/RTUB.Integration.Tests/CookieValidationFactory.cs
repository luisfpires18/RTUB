using System.Data.Common;
using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RTUB.Application.Data;
using RTUB.Web.Extensions;
using Xunit;

namespace RTUB.Integration.Tests;

/// <summary>
/// The shared factory plus a recorder for the SQL its <see cref="ApplicationDbContext"/>s execute.
/// The context is re-registered against <see cref="TestWebApplicationFactory.ConnectionString"/> —
/// the same database — purely so the interceptor can be attached.
/// </summary>
public class CookieValidationFactory : TestWebApplicationFactory
{
    public SqlRecorder Sql { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite(ConnectionString);
                options.EnableSensitiveDataLogging();
                options.AddInterceptors(new RecordingCommandInterceptor(Sql));
            });
        });
    }
}
