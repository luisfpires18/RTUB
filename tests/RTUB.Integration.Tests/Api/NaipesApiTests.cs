using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Moq;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// /api/naipes (task 033, was the Blazor /naipes and /naipes/config) through the real host: real login, antiforgery,
/// SQLite and the old NaipeService. Media storage and push are recording fakes, so nothing reaches R2 or a device.
/// Each test works on its own instrument, so the shared database never couples two tests.
/// </summary>
public partial class NaipesApiTests : IClassFixture<NaipesApiFactory>
{
    private static readonly byte[] Mp4 = Bytes(0, 0, 0, 0x18, 'f', 't', 'y', 'p', 'i', 's', 'o', 'm', 0, 0, 2, 0);
    private static readonly byte[] WebM = Bytes(0x1A, 0x45, 0xDF, 0xA3, 0x9F, 0x42, 0x86, 0x81, 1, 0x42, 0xF7, 0x81);
    private static readonly byte[] Png = Bytes(0x89, 'P', 'N', 'G', 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0, 0, 0, 0);
    private static readonly byte[] Jpeg = Bytes(0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 'J', 'F', 'I', 'F', 0, 1, 1, 0);
    private static readonly byte[] Svg = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>");
    private static readonly byte[] Html = Encoding.UTF8.GetBytes("<!doctype html><html><body><script>alert(1)</script></body></html>");

    private readonly NaipesApiFactory _factory;

    public NaipesApiTests(NaipesApiFactory factory)
    {
        _factory = factory;
    }

    // ---------- visitors ----------

    [Fact]
    public async Task Visitors_GetNothing_AndThePagesSendThemToSignIn()
    {
        var anonymous = AreaHttp.Anonymous(_factory, "10.56.0.1");
        await AreaHttp.WithTokenAsync(anonymous);

        foreach (var path in new[] { "/api/naipes", "/api/naipes?instrument=Guitarra", "/api/naipes/1/comments", "/api/naipes/config" })
        {
            var refused = await anonymous.GetAsync(path);
            refused.StatusCode.Should().Be(HttpStatusCode.Unauthorized, path);
            refused.Headers.CacheControl!.NoStore.Should().BeTrue(path);
        }

        (await anonymous.PostAsync("/api/naipes", Upload(Mp4, "aula.mp4", "video/mp4", "Guitarra", video: true))).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PutAsJsonAsync("/api/naipes/1", new { title = "x", description = "", sortOrder = 1 })).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.DeleteAsync("/api/naipes/1")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsync("/api/naipes/1/plays", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync("/api/naipes/1/comments", new { text = "olá" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.DeleteAsync("/api/naipes/1/comments/1")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PutAsJsonAsync("/api/naipes/config/1", new { isVisible = true, sortOrder = 1 })).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsync("/api/naipes/config/1/picture", Picture(Png, "image/png"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.DeleteAsync("/api/naipes/config/1/picture")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        foreach (var page in new[] { "/naipes", "/naipes/config" })
        {
            var response = await anonymous.GetAsync(page);
            response.StatusCode.Should().Be(HttpStatusCode.Redirect, page);
            response.Headers.Location!.ToString().Should().Be("/login?returnUrl=" + Uri.EscapeDataString(page));
        }
    }

    [Fact]
    public async Task Members_GetTheReactPages()
    {
        var (member, _) = await SignInAsync();

        foreach (var page in new[] { "/naipes", "/naipes/config" })
        {
            (await member.GetStringAsync(page)).Should().Contain("id=\"root\"", page).And.NotContain("blazor.web.js", page);
        }
    }

    [Fact]
    public async Task Writes_NeedTheAntiforgeryToken()
    {
        var (member, _) = await SignInAsync();

        (await member.PostAsync("/api/naipes", Upload(Mp4, "aula.mp4", "video/mp4", "Pandeireta", video: true))).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
        (await member.PostAsJsonAsync("/api/naipes/1/comments", new { text = "olá" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _factory.Media.Verify(m => m.UploadVideoAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), "Pandeireta"), Times.Never);
    }

    // ---------- reading ----------

    [Fact]
    public async Task Members_ReadTheBoard_WithEveryVisibleInstrument_AndNothingUntilOneIsPicked()
    {
        var (member, _) = await SignInAsync();

        var board = await AreaHttp.Json(member, "/api/naipes");

        board.GetProperty("instrument").ValueKind.Should().Be(JsonValueKind.Null);
        board.GetProperty("items").GetArrayLength().Should().Be(0, "nothing shows until an instrument is picked, as before");
        board.GetProperty("canConfigure").GetBoolean().Should().BeFalse("a plain member does not manage the settings");
        board.GetProperty("instruments").EnumerateArray().Select(i => i.GetProperty("value").GetString()).Should().Contain("Guitarra");
        board.GetProperty("instrumentOptions").GetArrayLength().Should().Be(Enum.GetValues<RTUB.Core.Enums.InstrumentType>().Length);

        (await AreaHttp.Errors(await member.GetAsync("/api/naipes?instrument=Harpa"))).Should().ContainKey("instrument");
        (await AreaHttp.Errors(await member.GetAsync("/api/naipes?instrument=3"))).Should().ContainKey("instrument");
    }

    [Fact]
    public async Task TheBoard_SortsByOrderThenAge_AndSearchesTitleAndDescription()
    {
        var (member, _) = await SignInAsync();
        await AreaHttp.WithTokenAsync(member);

        var late = await CreateAsync(member, "Bandolim", title: "Escala de ré", sortOrder: "3");
        var first = await CreateAsync(member, "Bandolim", title: "Primeira aula", description: "Postura e palheta", sortOrder: "1");
        var between = await CreateAsync(member, "Bandolim", title: "Arpejos", sortOrder: "1,5");

        var board = await AreaHttp.Json(member, "/api/naipes?instrument=Bandolim");
        board.GetProperty("instrument").GetString().Should().Be("Bandolim");
        board.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()).Should().Equal(new[] { first, between, late });
        board.GetProperty("totalForInstrument").GetInt32().Should().Be(3);
        board.GetProperty("items")[1].GetProperty("sortOrder").GetDecimal().Should().Be(1.5m, "a comma is read as the decimal point");

        var found = await AreaHttp.Json(member, "/api/naipes?instrument=Bandolim&q=PALHETA");
        found.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()).Should().Equal(new[] { first },
            "the search is case-insensitive and reads the description too");
        found.GetProperty("totalForInstrument").GetInt32().Should().Be(3, "the total is the instrument's, before the search");

        (await AreaHttp.Json(member, "/api/naipes?instrument=Bandolim&q=nada-disto")).GetProperty("items").GetArrayLength().Should().Be(0);
        (await AreaHttp.Json(member, "/api/naipes?instrument=Estandarte")).GetProperty("items").GetArrayLength()
            .Should().Be(0, "each instrument shows only its own content");
    }

    // ---------- helpers ----------

    private static int _ip;

    private Task<(HttpClient Client, RTUB.Core.Entities.ApplicationUser User)> SignInAsync(string? role = null)
    {
        var n = Interlocked.Increment(ref _ip);
        return CookieTestSession.SignInAsync(_factory, $"nps{Guid.NewGuid():N}"[..20], $"10.57.{n / 250}.{n % 250 + 1}", role);
    }

    private static async Task<int> CreateAsync(HttpClient client, string instrument, string title = "", string description = "",
        string sortOrder = "1", bool video = true)
    {
        var content = video
            ? Upload(Mp4, "aula.mp4", "video/mp4", instrument, video: true, title, description, sortOrder)
            : Upload(Png, "pauta.png", "image/png", instrument, video: false, title, description, sortOrder);
        var response = await client.PostAsync("/api/naipes", content);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    private static async Task<JsonElement> SettingAsync(HttpClient admin, string instrument) =>
        (await AreaHttp.Json(admin, "/api/naipes/config")).EnumerateArray().Single(s => s.GetProperty("instrument").GetString() == instrument);

    private static MultipartFormDataContent Upload(byte[] bytes, string fileName, string contentType, string instrument, bool video,
        string title = "", string description = "", string sortOrder = "1")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        return new MultipartFormDataContent
        {
            { file, "file", fileName },
            { new StringContent(instrument), "instrument" },
            { new StringContent(video ? "video" : "image"), "kind" },
            { new StringContent(title), "title" },
            { new StringContent(description), "description" },
            { new StringContent(sortOrder), "sortOrder" },
        };
    }

    private static MultipartFormDataContent Picture(byte[] bytes, string contentType)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        return new MultipartFormDataContent { { file, "picture", "naipe.png" } };
    }

    private static byte[] Bytes(params int[] values) => values.Select(v => (byte)v).ToArray();
}
