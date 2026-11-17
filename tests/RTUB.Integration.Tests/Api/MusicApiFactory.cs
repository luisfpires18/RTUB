using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RTUB.Application.Interfaces;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>The real host with fake Cloudflare R2 storage: nothing is uploaded, signed or deleted for real.</summary>
public sealed class MusicApiFactory : TestWebApplicationFactory
{
    public FakeMusicStorage Storage { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAudioStorageService>();
            services.RemoveAll<ILyricStorageService>();
            services.RemoveAll<IImageStorageService>();
            services.RemoveAll<ISongVideoStorageService>();
            services.AddSingleton<IAudioStorageService>(Storage);
            services.AddSingleton<ILyricStorageService>(Storage);
            services.AddSingleton<IImageStorageService>(Storage);
            services.AddSingleton<ISongVideoStorageService>(Storage);
        });
    }
}
