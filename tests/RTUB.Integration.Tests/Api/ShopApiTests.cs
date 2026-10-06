using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// The shop on /api/shop (React track 021, was the Blazor /shop) through the real host: real login, antiforgery,
/// SQLite and the old ProductService / ProductReservationService. Image storage is the recording fake of
/// <see cref="EventsApiFactory"/>: nothing reaches R2; the shop sends no notifications. Every test scopes its own
/// products with a unique tag, since the class shares one database.
/// </summary>
public class ShopApiTests : IClassFixture<EventsApiFactory>
{
    private static readonly byte[] Webp = { (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P', 1, 2 };
    private readonly EventsApiFactory _factory;

    public ShopApiTests(EventsApiFactory factory)
    {
        _factory = factory;
        _factory.Storage.Setup(s => s.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), "products", It.IsAny<string>()))
            .ReturnsAsync(() => $"https://pub-test.r2.dev/images/test/products/{Guid.NewGuid():N}.webp");
    }

    [Fact]
    public async Task Visitors_GetNothing_AndThePageIsReact()
    {
        var id = await AddAsync($"{Tag()} Visita", "Pin");
        var anonymous = Anonymous();
        await WithTokenAsync(anonymous);
        foreach (var path in new[] { "/api/shop", $"/api/shop/products/{id}", $"/api/shop/products/{id}/reservations" })
        {
            (await anonymous.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, path);
        }

        (await anonymous.PostAsJsonAsync($"/api/shop/products/{id}/reservations", new { hasSizes = false })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync("/api/shop/products", Input("x", "Pin"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var page = await anonymous.GetAsync("/shop");
        page.StatusCode.Should().Be(HttpStatusCode.Redirect);
        page.Headers.Location!.ToString().Should().Be("/login?returnUrl=%2Fshop");

        var (member, _) = await SignInAsync("Member");
        (await member.GetStringAsync("/shop")).Should().Contain("id=\"root\"").And.NotContain("blazor.web.js");

        var web = typeof(RTUB.App).Assembly;
        web.GetType("RTUB.Pages.Inventory.Shop").Should().BeNull("the Blazor shop page was retired");
        web.GetTypes()
            .SelectMany(t => t.GetCustomAttributes(typeof(RouteAttribute), false).Cast<RouteAttribute>())
            .Select(r => r.Template)
            .Should().NotContain(new[] { "/shop", "/inventory", "/leaderboard", "/members/manage", "/member/events", "/member/gallery", "/member/roles", "/hierarchy" });
    }

    [Fact]
    public async Task Products_AreByTypeThenName_ThisYearByDefault_WithSearchAndType()
    {
        var tag = Tag();
        await AddAsync($"{tag} Zeta", "Album", stock: 3);
        await AddAsync($"{tag} Alfa", "Roupa", stock: 0, isPublic: false);
        await AddAsync($"{tag} Beta", "Album", stock: 2, isPublic: false);
        var old = await AddAsync($"{tag} Antigo", "Album", stock: 1);
        await SetCreatedAsync(old, DateTime.UtcNow.AddYears(-2));
        var (member, _) = await SignInAsync("Member");

        var shop = await Json(member, $"/api/shop?q={tag}");
        shop.GetProperty("fiscalYear").GetString().Should().Be(FiscalYearHelper.GetCurrentFiscalYearString(), "the old page opened on the current year");
        Names(shop).Should().Equal($"{tag} Beta", $"{tag} Zeta", $"{tag} Alfa");
        shop.GetProperty("canCreate").GetBoolean().Should().BeFalse();
        shop.GetProperty("canManage").GetBoolean().Should().BeFalse();
        shop.GetProperty("sizes").EnumerateArray().Select(s => s.GetString()).Should().Equal("XS", "S", "M", "L", "XL", "XXL", "XXXL");

        var products = shop.GetProperty("products").EnumerateArray().ToList();
        products[0].GetProperty("canReserve").GetBoolean().Should().BeTrue("members-only and in stock");
        products[1].GetProperty("canReserve").GetBoolean().Should().BeFalse("public products are not reserved here");
        products[2].GetProperty("canReserve").GetBoolean().Should().BeFalse("out of stock");
        products[0].EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("id", "name", "type", "price", "stock", "isPublic", "imageUrl",
            "myReservation", "canReserve");

        Names(await Json(member, $"/api/shop?fiscalYear=&q={tag}")).Should().Contain($"{tag} Antigo", "Todos os anos");
        Names(await Json(member, $"/api/shop?fiscalYear=&q={tag}&type=Roupa")).Should().Equal($"{tag} Alfa");
        Names(await Json(member, $"/api/shop?q={tag.ToUpperInvariant()}%20b")).Should().Equal($"{tag} Beta");
        (await Json(member, "/api/shop?fiscalYear=")).GetProperty("types").EnumerateArray().Select(t => t.GetString()).Should().Contain(new[] { "Album", "Roupa" });
        (await member.GetAsync("/api/shop?fiscalYear=1999-2000")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reservations_FollowTheOldRules_AndNeverChangeTheStock()
    {
        var tag = Tag();
        var tee = await AddAsync($"{tag} T-shirt", "Roupa", stock: 2, isPublic: false);
        var pin = await AddAsync($"{tag} Pin", "Pin", stock: 5);
        var gone = await AddAsync($"{tag} Esgotado", "Pin", stock: 0, isPublic: false);
        var (member, me) = await SignInAsync("Member");
        await WithTokenAsync(member);

        (await Errors(await member.PostAsJsonAsync($"/api/shop/products/{pin}/reservations", new { hasSizes = false }))).Keys.Should().Equal("product");
        (await Errors(await member.PostAsJsonAsync($"/api/shop/products/{gone}/reservations", new { hasSizes = false }))).Keys.Should().Equal("product");
        (await Errors(await member.PostAsJsonAsync($"/api/shop/products/{tee}/reservations", new { hasSizes = true }))).Keys.Should().Equal("size");
        (await Errors(await member.PostAsJsonAsync($"/api/shop/products/{tee}/reservations", new { hasSizes = true, size = "XXXXL" }))).Keys.Should().Equal("size");
        (await Errors(await member.PostAsJsonAsync($"/api/shop/products/{tee}/reservations", new { hasSizes = false, displayName = new string('d', 201) })))
            .Keys.Should().Equal("displayName");
        (await member.PostAsJsonAsync("/api/shop/products/999999/reservations", new { hasSizes = false })).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var created = await Json(await member.PostAsJsonAsync($"/api/shop/products/{tee}/reservations", new { hasSizes = true, size = "M", displayName = " Zé " }));
        (created.GetProperty("username").GetString(), created.GetProperty("size").GetString(), created.GetProperty("displayName").GetString())
            .Should().Be((me.UserName, "M", "Zé"));
        created.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("id", "username", "displayName", "size", "createdAt");
        (await Errors(await member.PostAsJsonAsync($"/api/shop/products/{tee}/reservations", new { hasSizes = false })))["product"]
            .Single().Should().Be("Já existe uma reserva para este produto");
        (await ProductAsync(tee))!.Stock.Should().Be(2, "reserving never changed the stock");

        var card = (await Json(member, $"/api/shop?q={tag}")).GetProperty("products").EnumerateArray().Single(p => p.GetProperty("id").GetInt32() == tee);
        card.GetProperty("myReservation").GetProperty("id").GetInt32().Should().Be(created.GetProperty("id").GetInt32());
        card.GetProperty("canReserve").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Cancel_IsTheMembersOwn_AndAdminAndOwnerSeeAndDeleteEveryReservation()
    {
        var tee = await AddAsync($"{Tag()} Casaco", "Roupa", stock: 4, isPublic: false);
        var (alice, _) = await SignInAsync("Member");
        await WithTokenAsync(alice);
        var (bob, _) = await SignInAsync("Mod");
        await WithTokenAsync(bob);
        var (admin, _) = await SignInAsync("Admin");
        await WithTokenAsync(admin);
        var (owner, _) = await SignInAsync("Owner");
        await WithTokenAsync(owner);

        var aliceReservation = (await Json(await alice.PostAsJsonAsync($"/api/shop/products/{tee}/reservations", new { hasSizes = false }))).GetProperty("id").GetInt32();
        var bobReservation = (await Json(await bob.PostAsJsonAsync($"/api/shop/products/{tee}/reservations", new { hasSizes = false }))).GetProperty("id").GetInt32();

        (await bob.GetAsync($"/api/shop/products/{tee}/reservations")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "the reservations list was Admin only");
        (await bob.DeleteAsync($"/api/shop/reservations/{aliceReservation}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Json(admin, $"/api/shop/products/{tee}/reservations")).GetArrayLength().Should().Be(2);
        (await Json(owner, $"/api/shop/products/{tee}/reservations")).GetRawText().Should().NotContain("userId").And.NotContain("@test.com");

        (await alice.DeleteAsync($"/api/shop/reservations/{aliceReservation}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await alice.DeleteAsync($"/api/shop/reservations/{aliceReservation}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.DeleteAsync($"/api/shop/reservations/{bobReservation}")).StatusCode.Should().Be(HttpStatusCode.NoContent, "the Owner inherits Admin");
        (await Json(admin, $"/api/shop/products/{tee}/reservations")).GetArrayLength().Should().Be(0);
    }

    [Theory]
    [InlineData("Member", false, false)]
    [InlineData("Mod", true, false)]
    [InlineData("Admin", true, true)]
    [InlineData("Owner", true, true)]
    public async Task Products_ModAdds_AdminAndOwnerEditAndDelete(string role, bool creates, bool manages)
    {
        var id = await AddAsync($"{Tag()} Papel", "Pin", stock: 1);
        var (client, _) = await SignInAsync(role);
        await WithTokenAsync(client);

        var shop = await Json(client, "/api/shop?q=zzz-none");
        (shop.GetProperty("canCreate").GetBoolean(), shop.GetProperty("canManage").GetBoolean()).Should().Be((creates, manages));
        (await client.PostAsJsonAsync("/api/shop/products", Input($"{Tag()} Novo", "Pin"))).StatusCode
            .Should().Be(creates ? HttpStatusCode.Created : HttpStatusCode.Forbidden, role);
        (await client.PutAsJsonAsync($"/api/shop/products/{id}", Input("Renomeado", "Pin"))).StatusCode
            .Should().Be(manages ? HttpStatusCode.OK : HttpStatusCode.Forbidden, role);
        (await client.DeleteAsync($"/api/shop/products/{id}")).StatusCode.Should().Be(manages ? HttpStatusCode.NoContent : HttpStatusCode.Forbidden, role);
    }

    [Fact]
    public async Task ProductForm_Validates_AvailabilityFollowsStock_AndDeleteTakesImageAndReservations()
    {
        _factory.Storage.Invocations.Clear();
        var (admin, _) = await SignInAsync("Admin");
        await WithTokenAsync(admin);

        (await Errors(await admin.PostAsJsonAsync("/api/shop/products", new { price = 0, stock = -1 }))).Keys
            .Should().BeEquivalentTo("name", "type", "price", "stock");
        (await Errors(await admin.PostAsJsonAsync("/api/shop/products", Input(new string('n', 201), new string('t', 51)) with { Description = new string('d', 1001) })))
            .Keys.Should().BeEquivalentTo("name", "type", "description");

        var created = await admin.PostAsJsonAsync("/api/shop/products", Input($"{Tag()} Sweat", "Roupa") with { Stock = 0, IsPublic = false, Price = 12.5m });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        var saved = (await ProductAsync(id))!;
        (saved.Price, saved.Stock, saved.IsAvailable, saved.IsPublic).Should().Be((12.5m, 0, false, false), "availability follows the stock, as the old save");

        (await admin.PutAsJsonAsync($"/api/shop/products/{id}", Input(saved.Name, "Roupa") with { Stock = 3, IsPublic = false, Price = 15m })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ProductAsync(id))!.IsAvailable.Should().BeTrue();

        var image = (await Json(await admin.PostAsync($"/api/shop/products/{id}/image", Multipart(Webp, "image/webp")))).GetProperty("imageUrl").GetString()!;
        (await Errors(await admin.PostAsync($"/api/shop/products/{id}/image", Multipart("GIF89a-not-allowed"u8.ToArray(), "image/gif")))).Keys.Should().Equal("image");
        var replaced = (await Json(await admin.PostAsync($"/api/shop/products/{id}/image", Multipart(Webp, "image/webp")))).GetProperty("imageUrl").GetString()!;
        _factory.Storage.Verify(s => s.DeleteImageAsync(image), Times.Once, "a new image deletes the previous one");

        var (member, _) = await SignInAsync("Member");
        await WithTokenAsync(member);
        await member.PostAsJsonAsync($"/api/shop/products/{id}/reservations", new { hasSizes = false });
        (await admin.DeleteAsync($"/api/shop/products/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        _factory.Storage.Verify(s => s.DeleteImageAsync(replaced), Times.Once);
        (await ProductAsync(id)).Should().BeNull();
        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().ProductReservations.CountAsync(r => r.ProductId == id))
            .Should().Be(0, "reservations go with the product (cascade), as before");
    }

    [Fact]
    public async Task AMod_GivesTheProductTheyAddItsFirstImage_ButCannotChangeImages()
    {
        var (mod, _) = await SignInAsync("Mod");
        await WithTokenAsync(mod);
        var id = (await (await mod.PostAsJsonAsync("/api/shop/products", Input($"{Tag()} Pin Mod", "Pin"))).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        (await mod.PostAsync($"/api/shop/products/{id}/image", Multipart(Webp, "image/webp"))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await mod.PostAsync($"/api/shop/products/{id}/image", Multipart(Webp, "image/webp"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Writes_NeedTheAntiforgeryHeader()
    {
        var id = await AddAsync($"{Tag()} Token", "Roupa", stock: 2, isPublic: false);
        var (admin, _) = await SignInAsync("Admin");

        (await admin.PostAsJsonAsync("/api/shop/products", Input("sem token", "Pin"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PutAsJsonAsync($"/api/shop/products/{id}", Input("sem token", "Pin"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.DeleteAsync($"/api/shop/products/{id}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync($"/api/shop/products/{id}/reservations", new { hasSizes = false })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.DeleteAsync("/api/shop/reservations/1")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await ProductAsync(id))!.Name.Should().EndWith("Token");
    }

    [Fact]
    public void ReactFiles_SayNothingAboutMigration_AndTheMemberMenuLinksTheShop()
    {
        var src = Path.Combine(RepoRoot(), "src", "RTUB.Web", "portal", "src");
        File.ReadAllText(Path.Combine(src, "Shop.tsx")).Should().NotContainAny("migra", "Migra");
        File.ReadAllText(Path.Combine(src, "content.ts")).Should().Contain("shop: '/shop'");
        File.ReadAllText(Path.Combine(src, "MemberShell.tsx")).Should().Contain("href: portal.shop,", "the member menu links it (030)");
        File.ReadAllText(Path.Combine(RepoRoot(), "src", "RTUB.Web", "Shared", "MainLayout.razor")).Should().Contain("href=\"/shop\"");
    }

    // ---------- helpers ----------

    private static string Tag() => "shp" + Guid.NewGuid().ToString("N")[..8];

    private sealed record InputBody(string? Name, string? Type, decimal Price, int Stock, bool IsPublic, string? Description);

    private static InputBody Input(string name, string type) => new(name, type, 9.99m, 1, true, null);

    private static IEnumerable<string?> Names(JsonElement body) =>
        body.GetProperty("products").EnumerateArray().Select(p => p.GetProperty("name").GetString());

    private static MultipartFormDataContent Multipart(byte[] bytes, string type)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(type);
        return new MultipartFormDataContent { { content, "image", "product-image.webp" } };
    }

    private static async Task<JsonElement> Json(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.StatusCode.Should().Be(HttpStatusCode.OK, path);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<Dictionary<string, string[]>> Errors(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("errors").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.EnumerateArray().Select(v => v.GetString()!).ToArray());
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

    private HttpClient Anonymous()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, "10.90.0.1");
        return client;
    }

    private static async Task WithTokenAsync(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/public/antiforgery-token");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src", "RTUB.Web")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find the repository root");
    }
}
