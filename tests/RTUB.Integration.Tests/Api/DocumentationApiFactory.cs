using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RTUB.Application.Interfaces;

namespace RTUB.Integration.Tests.Api;

/// <summary>The test host with document storage replaced by an in-memory bucket: nothing is written to R2.</summary>
public sealed class DocumentationApiFactory : TestWebApplicationFactory
{
    public FakeDocumentStorage Storage { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDocumentStorageService>();
            services.AddSingleton<IDocumentStorageService>(Storage);
        });
    }
}
