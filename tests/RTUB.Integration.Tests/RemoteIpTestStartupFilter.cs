using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace RTUB.Integration.Tests;

/// <summary>
/// Test-host shim that lets a test represent a distinct client IP.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Microsoft.AspNetCore.TestHost.TestServer"/> has no transport, so
/// <c>Connection.RemoteIpAddress</c> is <see langword="null"/> for every request and every caller
/// lands in the same rate limiting partition. That makes any IP-partitioned policy untestable and
/// makes tests in one class interfere with each other.
/// </para>
/// <para>
/// This filter sets the same <c>Connection.RemoteIpAddress</c> property that the transport sets in
/// production, and it runs before the whole application pipeline, so the policy under test reads
/// exactly what it reads in production. It is registered only by
/// <see cref="TestWebApplicationFactory"/> and is a no-op unless a request carries
/// <see cref="HeaderName"/>, so it cannot affect a test that does not opt in.
/// </para>
/// <para>
/// It is <b>not</b> a forwarded-headers implementation. The application deliberately never reads a
/// client-supplied address; behind a reverse proxy that correction belongs to the host. See
/// <c>ServiceCollectionExtensions.AddLoginRateLimiting</c>.
/// </para>
/// </remarks>
internal sealed class RemoteIpTestStartupFilter : IStartupFilter
{
    internal const string HeaderName = "X-Test-Remote-Ip";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => builder =>
    {
        builder.Use(async (context, continuation) =>
        {
            if (context.Request.Headers.TryGetValue(HeaderName, out var value) &&
                IPAddress.TryParse(value.ToString(), out var address))
            {
                context.Connection.RemoteIpAddress = address;
            }

            await continuation();
        });

        next(builder);
    };
}
