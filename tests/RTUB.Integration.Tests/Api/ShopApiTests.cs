using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Core.Entities;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// The shop on /api/shop (React track 021, was the Blazor /shop) through the real host: public and authenticated reads, real login, antiforgery,
/// SQLite and the old ProductService / ProductReservationService. Image storage is the recording fake of
/// <see cref="EventsApiFactory"/>: nothing reaches R2; the shop sends no notifications. Every test scopes its own
/// products with a unique tag, since the class shares one database.
/// </summary>
public partial class ShopApiTests : IClassFixture<EventsApiFactory>
{
    private static readonly byte[] Webp = { (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P', 1, 2 };
    private readonly EventsApiFactory _factory;

    public ShopApiTests(EventsApiFactory factory)
    {
        _factory = factory;
        _factory.Storage.Setup(s => s.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), "products", It.IsAny<string>()))
            .ReturnsAsync(() => $"https://pub-test.r2.dev/images/test/products/{Guid.NewGuid():N}.webp");
    }

    private static string PortalSrc()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "RTUB.Web", "portal"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "src", "RTUB.Web", "portal", "src");
    }

    // ---------- helpers ----------

    private static string Tag() => "shp" + Guid.NewGuid().ToString("N")[..8];

    private sealed record InputBody(string? Name, string? Type, decimal Price, int Stock, bool IsPublic, string? Description, bool? AvailableAllYears = null);

    private static async Task<int> CreateAsync(HttpClient client, InputBody body)
    {
        var response = await client.PostAsJsonAsync("/api/shop/products", body);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    private async Task EnsureFiscalYearAsync(int start)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!await db.FiscalYears.AnyAsync(f => f.StartYear == start))
        {
            db.FiscalYears.Add(FiscalYear.Create(start, start + 1));
            await db.SaveChangesAsync();
        }
    }

    private static InputBody Input(string name, string type) => new(name, type, 9.99m, 1, true, null);

    private static IEnumerable<string?> Names(JsonElement body) =>
        body.GetProperty("products").EnumerateArray().Select(p => p.GetProperty("name").GetString());

    private static MultipartFormDataContent Multipart(byte[] bytes, string type)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(type);
        return new MultipartFormDataContent { { content, "image", "product-image.webp" } };
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<int> AddAsync(string name, string type, int stock = 1, bool isPublic = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = Product.Create(name, type, 10m, stock);
        product.SetPublicVisibility(isPublic);
        product.SetAvailability(stock > 0);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    private async Task SetCreatedAsync(int id, DateTime createdAt)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Products.Where(p => p.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.CreatedAt, createdAt));
    }

    private async Task<Product?> ProductAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id);
    }

    private static int _ip;

    private Task<(HttpClient Client, ApplicationUser User)> SignInAsync(string? role = null)
    {
        var n = Interlocked.Increment(ref _ip);
        return CookieTestSession.SignInAsync(_factory, $"shp{Guid.NewGuid():N}"[..20], $"10.89.{n / 250}.{n % 250 + 1}", role);
    }

    private HttpClient Anonymous() => AreaHttp.Anonymous(_factory, "10.90.0.1");
}
