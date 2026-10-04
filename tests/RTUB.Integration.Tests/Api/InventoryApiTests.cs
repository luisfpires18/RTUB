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
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// The instruments inventory on /api/inventory (React track 020, was the Blazor /inventory) through the real host: real
/// login, antiforgery, SQLite and the old InstrumentService. Image storage is the recording fake of
/// <see cref="EventsApiFactory"/>: nothing reaches R2. Every test scopes its own instruments with a unique tag, since
/// the class shares one database.
/// </summary>
public class InventoryApiTests : IClassFixture<EventsApiFactory>
{
    private static readonly byte[] Png = { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 1, 2, 3 };
    private static readonly byte[] Webp = { (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P', 1, 2 };
    private readonly EventsApiFactory _factory;

    public InventoryApiTests(EventsApiFactory factory)
    {
        _factory = factory;
        _factory.Storage.Setup(s => s.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), "instruments", It.IsAny<string>()))
            .ReturnsAsync(() => $"https://pub-test.r2.dev/images/test/instruments/{Guid.NewGuid():N}.png");
        _factory.Storage.Setup(s => s.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), "instruments/thumbnails", It.IsAny<string>()))
            .ReturnsAsync(() => $"https://pub-test.r2.dev/images/test/instruments/thumbnails/{Guid.NewGuid():N}.webp");
    }

    [Fact]
    public async Task Visitors_GetNothing_TheRouteIsReact_AndTheShopStaysBlazor()
    {
        var id = await AddAsync("Visita", InstrumentType.Guitarra);
        var anonymous = Anonymous();
        await WithTokenAsync(anonymous);
        (await anonymous.GetAsync("/api/inventory")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync($"/api/inventory/{id}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync("/api/inventory", Input("x", "Guitarra"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.DeleteAsync($"/api/inventory/{id}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var page = await anonymous.GetAsync("/inventory");
        page.StatusCode.Should().Be(HttpStatusCode.Redirect);
        page.Headers.Location!.ToString().Should().Be("/login?returnUrl=%2Finventory");

        var (member, _) = await SignInAsync("Member");
        (await member.GetStringAsync("/inventory")).Should().Contain("id=\"root\"").And.NotContain("blazor.web.js");
        (await member.GetStringAsync("/shop")).Should().Contain("blazor.web.js", "the shop (Loja) is its own module and stays Blazor for now");

        var web = typeof(RTUB.App).Assembly;
        web.GetType("RTUB.Pages.Inventory.Inventory").Should().BeNull("the Blazor instruments page was retired");
        web.GetTypes()
            .SelectMany(t => t.GetCustomAttributes(typeof(RouteAttribute), false).Cast<RouteAttribute>())
            .Select(r => r.Template)
            .Should().NotContain(new[] { "/inventory", "/leaderboard", "/members/manage", "/member/events", "/member/gallery", "/member/roles", "/hierarchy" });
    }

    [Fact]
    public async Task List_IsByName_WithTheOldSearchFiltersAndCounters()
    {
        var tag = Tag();
        await AddAsync($"{tag} Bravo", InstrumentType.Bandolim, InstrumentCondition.Worn, brand: "Yamaha", location: "Sala 1");
        await AddAsync($"{tag} Alfa", InstrumentType.Acordeao, InstrumentCondition.Excellent);
        await AddAsync($"{tag} Charlie", InstrumentType.Bandolim, InstrumentCondition.Lost);
        var (member, _) = await SignInAsync("Member");

        var all = await Json(member, $"/api/inventory?q={tag}");
        Names(all).Should().Equal($"{tag} Alfa", $"{tag} Bravo", $"{tag} Charlie");
        all.GetProperty("canManage").GetBoolean().Should().BeFalse();
        var bravo = all.GetProperty("instruments")[1];
        (bravo.GetProperty("category").GetString(), bravo.GetProperty("categoryLabel").GetString(), bravo.GetProperty("conditionLabel").GetString(),
            bravo.GetProperty("brand").GetString(), bravo.GetProperty("location").GetString()).Should().Be(("Bandolim", "Bandolim", "Velho", "Yamaha", "Sala 1"));
        all.GetProperty("instruments")[0].GetProperty("categoryLabel").GetString().Should().Be("Acordeão");

        Names(await Json(member, "/api/inventory?q=yAmAhA")).Should().Contain($"{tag} Bravo", "the brand is searched, case-insensitive");
        Names(await Json(member, $"/api/inventory?q={tag}&category=Bandolim")).Should().Equal($"{tag} Bravo", $"{tag} Charlie");
        Names(await Json(member, $"/api/inventory?q={tag}&condition=Lost")).Should().Equal($"{tag} Charlie");
        foreach (var bad in new[] { "category=Kazoo", "condition=Broken", "condition=3" })
        {
            (await member.GetAsync($"/api/inventory?{bad}")).StatusCode.Should().Be(HttpStatusCode.BadRequest, bad);
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var counts = await db.Instruments.GroupBy(i => i.Condition).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        var stats = all.GetProperty("stats").EnumerateArray().Select(s => (s.GetProperty("condition").GetString(), s.GetProperty("count").GetInt32())).ToList();
        stats.Should().Equal(counts.OrderBy(c => c.Key).Take(3).Select(c => ((string?)c.Key.ToString(), c.Count)),
            "the old counters: the first three conditions present, in enum order, over every instrument");
        all.GetProperty("total").GetInt32().Should().Be(await db.Instruments.CountAsync());
    }

    [Fact]
    public async Task Details_ShowEveryOldFieldToAnyMember()
    {
        var id = await AddAsync($"{Tag()} Detalhe", InstrumentType.Guitarra, brand: "Alhambra", location: "Armário", serial: "SN-1", notes: "Cordas novas",
            maintenance: new DateTime(2025, 3, 4));
        var (member, _) = await SignInAsync("Member");

        var detail = await Json(member, $"/api/inventory/{id}");

        (detail.GetProperty("serialNumber").GetString(), detail.GetProperty("maintenanceNotes").GetString(), detail.GetProperty("lastMaintenanceDate").GetString())
            .Should().Be(("SN-1", "Cordas novas", "2025-03-04"));
        detail.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("id", "name", "category", "categoryLabel", "condition", "conditionLabel",
            "brand", "serialNumber", "location", "lastMaintenanceDate", "maintenanceNotes", "imageUrl", "thumbnailUrl");
        (await member.GetAsync("/api/inventory/999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("Member", false)]
    [InlineData("Mod", true)]
    [InlineData("Admin", true)]
    [InlineData("Owner", true)]
    public async Task OnlyModAdminAndOwner_Manage(string role, bool manages)
    {
        var id = await AddAsync($"{Tag()} Papel", InstrumentType.Pandeireta);
        var (client, _) = await SignInAsync(role);
        await WithTokenAsync(client);

        (await Json(client, "/api/inventory?q=zzz-none")).GetProperty("canManage").GetBoolean().Should().Be(manages);
        var expected = manages ? HttpStatusCode.Created : HttpStatusCode.Forbidden;
        (await client.PostAsJsonAsync("/api/inventory", Input($"{Tag()} Novo", "Guitarra"))).StatusCode.Should().Be(expected, role);
        (await client.PutAsJsonAsync($"/api/inventory/{id}", Input("Renomeado", "Guitarra"))).StatusCode
            .Should().Be(manages ? HttpStatusCode.OK : HttpStatusCode.Forbidden, role);
        (await client.DeleteAsync($"/api/inventory/{id}")).StatusCode.Should().Be(manages ? HttpStatusCode.NoContent : HttpStatusCode.Forbidden, role);
    }

    [Fact]
    public async Task Create_Validates_AsTheEntity_AndEditNeverChangesTheType()
    {
        var (mod, _) = await SignInAsync("Mod");
        await WithTokenAsync(mod);

        (await Errors(await mod.PostAsJsonAsync("/api/inventory", new { condition = "Nope" }))).Keys.Should().BeEquivalentTo("name", "category", "condition");
        (await Errors(await mod.PostAsJsonAsync("/api/inventory", Input(new string('n', 101), "Guitarra") with
        {
            Brand = new string('b', 101), SerialNumber = new string('s', 101), Location = new string('l', 201), MaintenanceNotes = new string('m', 501),
        }))).Keys.Should().BeEquivalentTo("name", "brand", "serialNumber", "location", "maintenanceNotes");
        (await Errors(await mod.PostAsJsonAsync("/api/inventory", Input("x", "Kazoo")))).Keys.Should().Equal("category");

        var name = $"{Tag()} Criado";
        var created = await mod.PostAsJsonAsync("/api/inventory", Input(name, "Cavaquinho") with { Brand = "", LastMaintenanceDate = new DateOnly(2024, 5, 6) });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        var saved = (await InstrumentAsync(id))!;
        (saved.Name, saved.Category, saved.Condition, saved.Brand, saved.LastMaintenanceDate).Should().Be((name, "Cavaquinho", InstrumentCondition.Good, null, new DateTime(2024, 5, 6)));

        var edit = await mod.PutAsJsonAsync($"/api/inventory/{id}", Input("Outro nome", "Guitarra") with { Condition = "NeedsMaintenance", Location = "Sede" });
        edit.StatusCode.Should().Be(HttpStatusCode.OK);
        var edited = (await InstrumentAsync(id))!;
        (edited.Name, edited.Category, edited.Condition, edited.Location).Should().Be(("Outro nome", "Cavaquinho", InstrumentCondition.NeedsMaintenance, "Sede"),
            "the old edit never changed the type");
        (await mod.PutAsJsonAsync("/api/inventory/999999", Input("x", "Guitarra"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Image_TakesTheOriginalAndItsThumbnail_ReplacesBoth_AndDeleteRemovesTheImageAsBefore()
    {
        _factory.Storage.Invocations.Clear();
        var id = await AddAsync($"{Tag()} Imagem", InstrumentType.Guitarra);
        var (admin, _) = await SignInAsync("Admin");
        await WithTokenAsync(admin);

        (await admin.PostAsync($"/api/inventory/{id}/image", Multipart(("image", Png, "image/png")))).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "the thumbnail must come with the image, as the old form required");
        (await Errors(await admin.PostAsync($"/api/inventory/{id}/image", Multipart(("image", "GIF89a-not-allowed"u8.ToArray(), "image/gif"), ("thumbnail", Webp, "image/webp")))))
            .Keys.Should().Equal("image");

        var first = await Json(await admin.PostAsync($"/api/inventory/{id}/image", Multipart(("image", Png, "image/png"), ("thumbnail", Webp, "image/webp"))));
        var image = first.GetProperty("imageUrl").GetString()!;
        var thumbnail = first.GetProperty("thumbnailUrl").GetString()!;
        image.Should().Contain("/instruments/");
        thumbnail.Should().Contain("/instruments/thumbnails/");

        await Json(await admin.PostAsync($"/api/inventory/{id}/image", Multipart(("image", Png, "image/png"), ("thumbnail", Webp, "image/webp"))));
        _factory.Storage.Verify(s => s.DeleteImageAsync(image), Times.Once, "a new image deletes the previous one");
        _factory.Storage.Verify(s => s.DeleteImageAsync(thumbnail), Times.Once, "and the previous thumbnail");

        var current = (await InstrumentAsync(id))!;
        (await admin.DeleteAsync($"/api/inventory/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        _factory.Storage.Verify(s => s.DeleteImageAsync(current.ImageUrl!), Times.Once);
        _factory.Storage.Verify(s => s.DeleteImageAsync(current.ThumbnailUrl!), Times.Never, "the old delete left the thumbnail (recorded follow-up)");
        (await InstrumentAsync(id)).Should().BeNull("a hard delete, as before");
        (await admin.DeleteAsync($"/api/inventory/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Writes_NeedTheAntiforgeryHeader()
    {
        var id = await AddAsync($"{Tag()} Token", InstrumentType.Guitarra);
        var (admin, _) = await SignInAsync("Admin");

        (await admin.PostAsJsonAsync("/api/inventory", Input("sem token", "Guitarra"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PutAsJsonAsync($"/api/inventory/{id}", Input("sem token", "Guitarra"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.DeleteAsync($"/api/inventory/{id}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsync($"/api/inventory/{id}/image", Multipart(("image", Png, "image/png"), ("thumbnail", Webp, "image/webp")))).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);

        (await InstrumentAsync(id))!.Name.Should().EndWith("Token");
    }

    [Fact]
    public void ReactFiles_SayNothingAboutMigration_AndTheProfileLinksTheInventory()
    {
        var src = Path.Combine(RepoRoot(), "src", "RTUB.Web", "portal", "src");
        File.ReadAllText(Path.Combine(src, "Inventory.tsx")).Should().NotContainAny("migra", "Migra");
        File.ReadAllText(Path.Combine(src, "content.ts")).Should().Contain("inventory: '/inventory'");
        File.ReadAllText(Path.Combine(src, "Profile.tsx")).Should().Contain("href={portal.inventory}");
        File.ReadAllText(Path.Combine(RepoRoot(), "src", "RTUB.Web", "Shared", "MainLayout.razor")).Should().Contain("href=\"/inventory\"").And.Contain("href=\"/shop\"");
    }

    // ---------- helpers ----------

    private static string Tag() => "inv" + Guid.NewGuid().ToString("N")[..8];

    private sealed record InputBody(string? Name, string? Category, string? Condition, string? Brand, string? SerialNumber, string? Location,
        DateOnly? LastMaintenanceDate, string? MaintenanceNotes);

    private static InputBody Input(string name, string category) => new(name, category, "Good", null, null, null, null, null);

    private static IEnumerable<string?> Names(JsonElement body) =>
        body.GetProperty("instruments").EnumerateArray().Select(i => i.GetProperty("name").GetString());

    private static MultipartFormDataContent Multipart(params (string Name, byte[] Bytes, string Type)[] files)
    {
        var form = new MultipartFormDataContent();
        foreach (var (name, bytes, type) in files)
        {
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue(type);
            form.Add(content, name, $"{name}.bin");
        }

        return form;
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

    private async Task<int> AddAsync(string name, InstrumentType type, InstrumentCondition condition = InstrumentCondition.Good, string? brand = null,
        string? location = null, string? serial = null, string? notes = null, DateTime? maintenance = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var instrument = Instrument.Create(type.ToString(), name, condition);
        instrument.Brand = brand;
        instrument.Location = location;
        instrument.SerialNumber = serial;
        instrument.MaintenanceNotes = notes;
        instrument.LastMaintenanceDate = maintenance;
        db.Instruments.Add(instrument);
        await db.SaveChangesAsync();
        return instrument.Id;
    }

    private async Task<Instrument?> InstrumentAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Instruments.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id);
    }

    private static int _ip;

    private Task<(HttpClient Client, ApplicationUser User)> SignInAsync(string? role = null)
    {
        var n = Interlocked.Increment(ref _ip);
        return CookieTestSession.SignInAsync(_factory, $"inv{Guid.NewGuid():N}"[..20], $"10.87.{n / 250}.{n % 250 + 1}", role);
    }

    private HttpClient Anonymous()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, "10.88.0.1");
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
