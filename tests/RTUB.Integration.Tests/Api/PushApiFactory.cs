using System.Buffers.Text;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;

namespace RTUB.Integration.Tests.Api;

public sealed class PushApiFactory : TestWebApplicationFactory
{
    /// <summary>A well-formed P-256 public key shape; never used to send.</summary>
    public static readonly string PublicKey = Base64Url.EncodeToString(Enumerable.Repeat((byte)4, 1).Concat(Enumerable.Range(1, 64).Select(i => (byte)i)).ToArray());

    /// <summary>A sentinel: the tests assert it never appears in an answer.</summary>
    public const string PrivateKey = "PRIVATE-VAPID-SENTINEL-must-never-leave-the-server";

    public WebPushOptions Options { get; } = new();

    public void Reset()
    {
        Options.Enabled = true;
        Options.VapidSubject = "mailto:push-tests@rtub.invalid";
        Options.VapidPublicKey = PublicKey;
        Options.VapidPrivateKey = PrivateKey;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        Reset();
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IOptions<WebPushOptions>>();
            services.AddSingleton<IOptions<WebPushOptions>>(Microsoft.Extensions.Options.Options.Create(Options));
        });
    }
}
