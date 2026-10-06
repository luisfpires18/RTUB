using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// /api/naipes (task 033, was the Blazor /naipes and /naipes/config) through the real host: real login, antiforgery,
/// SQLite and the old NaipeService. Media storage and push are recording fakes, so nothing reaches R2 or a device.
/// Each test works on its own instrument, so the shared database never couples two tests.
/// </summary>
public class NaipesApiTests : IClassFixture<NaipesApiFactory>
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

    // ---------- adding ----------

    [Fact]
    public async Task AnyMember_AddsAVideoOrAnImage_StoredAsBefore_AndEveryoneIsNotified()
    {
        var (member, user) = await SignInAsync();
        await AreaHttp.WithTokenAsync(member);

        var created = await member.PostAsync("/api/naipes",
            Upload(Mp4, "aula.mp4", "video/mp4", "Guitarra", video: true, title: "  Rasgado  ", description: "Ritmo base", sortOrder: "2"));
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var video = await created.Content.ReadFromJsonAsync<JsonElement>();
        video.GetProperty("title").GetString().Should().Be("Rasgado");
        video.GetProperty("description").GetString().Should().Be("Ritmo base");
        video.GetProperty("isVideo").GetBoolean().Should().BeTrue();
        video.GetProperty("mimeType").GetString().Should().Be("video/mp4");
        video.GetProperty("instrumentLabel").GetString().Should().Be("Guitarra");
        video.GetProperty("createdBy").GetString().Should().Be(user.Nickname);
        video.GetProperty("canEdit").GetBoolean().Should().BeTrue("the author edits what they added");
        video.GetProperty("url").GetString().Should().StartWith("https://pub-test.r2.dev/naipes/");

        _factory.Media.Verify(m => m.UploadVideoAsync(It.IsAny<Stream>(), "aula.mp4", "video/mp4", "Guitarra"), Times.Once);
        _factory.Push.Verify(p => p.BroadcastAsync(It.Is<SendPushNotificationDto>(n =>
            n.Title == "Novo vídeo em Guitarra" && n.Body.Contains("Rasgado") && n.Url!.EndsWith("/naipes"))), Times.Once);

        var image = await AreaHttp.Json(await member.PostAsync("/api/naipes", Upload(Jpeg, "acordes.jpeg", "image/jpeg", "Guitarra", video: false)));
        image.GetProperty("title").GetString().Should().Be("Guitarra_Imagem_1", "an untitled item is named as the old page named it");
        image.GetProperty("isVideo").GetBoolean().Should().BeFalse();
        _factory.Media.Verify(m => m.UploadImageAsync(It.IsAny<Stream>(), "acordes.jpg", "image/jpeg", "Guitarra"), Times.Once);

        var webm = await AreaHttp.Json(await member.PostAsync("/api/naipes", Upload(WebM, "ensaio.webm", "video/webm", "Guitarra", video: true)));
        webm.GetProperty("title").GetString().Should().Be("Guitarra_Video_2");
        webm.GetProperty("mimeType").GetString().Should().Be("video/webm");
    }

    [Fact]
    public async Task TheStoredType_IsTheServers_NeverTheBrowsers()
    {
        var (member, _) = await SignInAsync();
        await AreaHttp.WithTokenAsync(member);

        // A real MP4 sent with a wrong header is stored as MP4; a PNG called .jpg is stored as PNG.
        var video = await AreaHttp.Json(await member.PostAsync("/api/naipes", Upload(Mp4, "aula.mp4", "text/html", "Flauta", video: true)));
        video.GetProperty("mimeType").GetString().Should().Be("video/mp4");
        var image = await AreaHttp.Json(await member.PostAsync("/api/naipes", Upload(Png, "pauta.jpg", "image/jpeg", "Flauta", video: false)));
        image.GetProperty("mimeType").GetString().Should().Be("image/png");
        _factory.Media.Verify(m => m.UploadImageAsync(It.IsAny<Stream>(), "pauta.png", "image/png", "Flauta"), Times.Once);
    }

    [Fact]
    public async Task Uploads_AreValidated()
    {
        var (member, _) = await SignInAsync();
        await AreaHttp.WithTokenAsync(member);

        async Task<Dictionary<string, string[]>> Refused(HttpContent content) => await AreaHttp.Errors(await member.PostAsync("/api/naipes", content));

        (await Refused(Upload(Array.Empty<byte>(), "nada.mp4", "video/mp4", "Baixo", video: true))).Should().ContainKey("file");
        (await Refused(Upload(Mp4, "aula.mp4", "video/mp4", "Harpa", video: true))).Should().ContainKey("instrument");
        (await Refused(Upload(Html, "pagina.mp4", "video/mp4", "Baixo", video: true))).Should().ContainKey("file", "the bytes are not a video");
        (await Refused(Upload(Mp4, "aula.exe", "video/mp4", "Baixo", video: true))).Should().ContainKey("file", "the extension is not a video's");
        (await Refused(Upload(Mp4, "aula.mkv", "video/x-matroska", "Baixo", video: true))).Should().ContainKey("file");
        (await Refused(Upload(Svg, "logo.svg", "image/svg+xml", "Baixo", video: false))).Should().ContainKey("file", "SVG is never stored");
        (await Refused(Upload(Html, "pagina.png", "image/png", "Baixo", video: false))).Should().ContainKey("file", "the bytes are not an image");
        (await Refused(Upload(Mp4, "aula.mp4", "video/mp4", "Baixo", video: false))).Should().ContainKey("file", "a video is not an image");
        (await Refused(Upload(Png, "pauta.png", "image/png", "Baixo", video: true))).Should().ContainKey("file", "an image is not a video");

        var tooBig = new byte[10 * 1024 * 1024 + 1];
        Png.CopyTo(tooBig, 0);
        (await Refused(Upload(tooBig, "pauta.png", "image/png", "Baixo", video: false)))["file"].Single().Should().Contain("10 MB");

        (await Refused(Upload(Mp4, "aula.mp4", "video/mp4", "Baixo", video: true, title: new string('t', 201)))).Should().ContainKey("title");
        (await Refused(Upload(Mp4, "aula.mp4", "video/mp4", "Baixo", video: true, description: new string('d', 1001)))).Should().ContainKey("description");
        foreach (var order in new[] { "", "0", "1000", "um" })
        {
            (await Refused(Upload(Mp4, "aula.mp4", "video/mp4", "Baixo", video: true, sortOrder: order))).Should().ContainKey("sortOrder", order);
        }

        _factory.Media.Verify(m => m.UploadVideoAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), "Baixo"), Times.Never);
        _factory.Media.Verify(m => m.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), "Baixo"), Times.Never);
    }

    // ---------- editing ----------

    [Fact]
    public async Task OnlyTheAuthor_AnAdminOrTheOwner_EditOrDelete()
    {
        var (author, _) = await SignInAsync();
        var (other, _) = await SignInAsync();
        var (admin, _) = await SignInAsync("Admin");
        var (owner, _) = await SignInAsync("Owner");
        foreach (var client in new[] { author, other, admin, owner })
        {
            await AreaHttp.WithTokenAsync(client);
        }

        var id = await CreateAsync(author, "Cavaquinho", title: "Batida");

        var seen = (await AreaHttp.Json(other, "/api/naipes?instrument=Cavaquinho")).GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetInt32() == id);
        seen.GetProperty("canEdit").GetBoolean().Should().BeFalse();
        (await AreaHttp.Json(admin, "/api/naipes?instrument=Cavaquinho")).GetProperty("canConfigure").GetBoolean().Should().BeTrue();
        (await AreaHttp.Json(owner, "/api/naipes?instrument=Cavaquinho")).GetProperty("canConfigure").GetBoolean().Should().BeTrue("Owner inherits Admin");

        (await other.PutAsJsonAsync($"/api/naipes/{id}", new { title = "Roubado", description = "", sortOrder = 1 })).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await other.DeleteAsync($"/api/naipes/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var edited = await AreaHttp.Json(await author.PutAsJsonAsync($"/api/naipes/{id}", new { title = " Batida nova ", description = " Lenta ", sortOrder = 4.5 }));
        edited.GetProperty("title").GetString().Should().Be("Batida nova");
        edited.GetProperty("description").GetString().Should().Be("Lenta");
        edited.GetProperty("sortOrder").GetDecimal().Should().Be(4.5m);

        (await AreaHttp.Errors(await author.PutAsJsonAsync($"/api/naipes/{id}", new { title = " ", description = "", sortOrder = 1 }))).Should().ContainKey("title");
        (await AreaHttp.Errors(await author.PutAsJsonAsync($"/api/naipes/{id}", new { title = "x", description = new string('d', 1001), sortOrder = 1 })))
            .Should().ContainKey("description");
        (await AreaHttp.Errors(await author.PutAsJsonAsync($"/api/naipes/{id}", new { title = "x", description = "", sortOrder = (decimal?)null })))
            .Should().ContainKey("sortOrder");

        (await AreaHttp.Json(await admin.PutAsJsonAsync($"/api/naipes/{id}", new { title = "Pelo Admin", description = "", sortOrder = 1 })))
            .GetProperty("title").GetString().Should().Be("Pelo Admin");
        (await AreaHttp.Json(await owner.PutAsJsonAsync($"/api/naipes/{id}", new { title = "Pelo Owner", description = "", sortOrder = 1 })))
            .GetProperty("title").GetString().Should().Be("Pelo Owner");

        var url = (await AreaHttp.Json(owner, "/api/naipes?instrument=Cavaquinho")).GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("id").GetInt32() == id).GetProperty("url").GetString();
        (await owner.DeleteAsync($"/api/naipes/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        _factory.Media.Verify(m => m.DeleteMediaAsync(url!), Times.Once, "the file leaves the media store, as before");
        (await author.DeleteAsync($"/api/naipes/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await author.PutAsJsonAsync($"/api/naipes/{id}", new { title = "x", description = "", sortOrder = 1 })).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var mine = await CreateAsync(author, "Cavaquinho");
        (await author.DeleteAsync($"/api/naipes/{mine}")).StatusCode.Should().Be(HttpStatusCode.NoContent, "the author deletes their own");
    }

    // ---------- comments and plays ----------

    [Fact]
    public async Task Comments_AnyMemberWrites_TheAuthorOrAManagerDeletes()
    {
        var (writer, _) = await SignInAsync();
        var (other, _) = await SignInAsync();
        var (admin, _) = await SignInAsync("Admin");
        foreach (var client in new[] { writer, other, admin })
        {
            await AreaHttp.WithTokenAsync(client);
        }

        var id = await CreateAsync(other, "Acordeao");

        (await AreaHttp.Errors(await writer.PostAsJsonAsync($"/api/naipes/{id}/comments", new { text = "   " }))).Should().ContainKey("text");
        (await AreaHttp.Errors(await writer.PostAsJsonAsync($"/api/naipes/{id}/comments", new { text = new string('c', 1001) }))).Should().ContainKey("text");
        (await writer.PostAsJsonAsync("/api/naipes/999999/comments", new { text = "olá" })).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var comments = await AreaHttp.Json(await writer.PostAsJsonAsync($"/api/naipes/{id}/comments", new { text = "  Muito útil  " }));
        var comment = comments.EnumerateArray().Single();
        comment.GetProperty("text").GetString().Should().Be("Muito útil");
        comment.GetProperty("authorName").GetString().Should().NotBeNullOrWhiteSpace();
        comment.GetProperty("canDelete").GetBoolean().Should().BeTrue();
        comments.EnumerateArray().Select(c => c.EnumerateObject().Select(p => p.Name)).SelectMany(n => n)
            .Should().NotContain(new[] { "authorId", "email" }, "no account id or email reaches the page");
        var commentId = comment.GetProperty("id").GetInt32();

        var seenByOther = (await AreaHttp.Json(other, $"/api/naipes/{id}/comments")).EnumerateArray().Single();
        seenByOther.GetProperty("canDelete").GetBoolean().Should().BeFalse("the content's author is not the comment's");
        (await other.DeleteAsync($"/api/naipes/{id}/comments/{commentId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await AreaHttp.Json(other, $"/api/naipes?instrument=Acordeao")).GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("id").GetInt32() == id).GetProperty("commentCount").GetInt32().Should().Be(1);

        var other2 = await CreateAsync(other, "Acordeao");
        (await writer.DeleteAsync($"/api/naipes/{other2}/comments/{commentId}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound, "the comment belongs to another item");

        (await AreaHttp.Json(await writer.DeleteAsync($"/api/naipes/{id}/comments/{commentId}"))).GetArrayLength().Should().Be(0);

        var second = (await AreaHttp.Json(await writer.PostAsJsonAsync($"/api/naipes/{id}/comments", new { text = "Outra" }))).EnumerateArray().Single();
        (await AreaHttp.Json(admin, $"/api/naipes/{id}/comments")).EnumerateArray().Single().GetProperty("canDelete").GetBoolean().Should().BeTrue();
        (await AreaHttp.Json(await admin.DeleteAsync($"/api/naipes/{id}/comments/{second.GetProperty("id").GetInt32()}"))).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Plays_CountForVideosOnly()
    {
        var (member, _) = await SignInAsync();
        await AreaHttp.WithTokenAsync(member);
        var video = await CreateAsync(member, "Fagote");
        var image = await CreateAsync(member, "Fagote", video: false);

        (await member.PostAsync($"/api/naipes/{video}/plays", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await member.PostAsync($"/api/naipes/{video}/plays", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AreaHttp.Errors(await member.PostAsync($"/api/naipes/{image}/plays", null))).Should().ContainKey("id");
        (await member.PostAsync("/api/naipes/999999/plays", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var items = (await AreaHttp.Json(member, "/api/naipes?instrument=Fagote")).GetProperty("items").EnumerateArray().ToList();
        items.Single(i => i.GetProperty("id").GetInt32() == video).GetProperty("playCount").GetInt32().Should().Be(2);
        items.Single(i => i.GetProperty("id").GetInt32() == image).GetProperty("playCount").GetInt32().Should().Be(0);
    }

    // ---------- settings (/naipes/config) ----------

    [Fact]
    public async Task Settings_AreForAdminAndOwner_AndMembersAreRefused()
    {
        var (member, _) = await SignInAsync();
        await AreaHttp.WithTokenAsync(member);

        (await member.GetAsync("/api/naipes/config")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.PutAsJsonAsync("/api/naipes/config/1", new { isVisible = false, sortOrder = 1 })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.PostAsync("/api/naipes/config/1/picture", Picture(Png, "image/png"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.DeleteAsync("/api/naipes/config/1/picture")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var (admin, _) = await SignInAsync("Admin");
        var (owner, _) = await SignInAsync("Owner");
        foreach (var client in new[] { admin, owner })
        {
            var settings = (await AreaHttp.Json(client, "/api/naipes/config")).EnumerateArray().ToList();
            settings.Should().HaveCount(Enum.GetValues<RTUB.Core.Enums.InstrumentType>().Length, "every instrument has its setting");
            settings.Select(s => s.GetProperty("sortOrder").GetInt32()).Should().BeInAscendingOrder();
        }
    }

    [Fact]
    public async Task AnAdmin_HidesReordersAndPicturesAnInstrument_AndTheBoardFollows()
    {
        var (admin, _) = await SignInAsync("Admin");
        var (owner, _) = await SignInAsync("Owner");
        var (member, _) = await SignInAsync();
        await AreaHttp.WithTokenAsync(admin);
        await AreaHttp.WithTokenAsync(owner);
        await AreaHttp.WithTokenAsync(member);
        var saxofone = await SettingAsync(admin, "Saxofone");
        var id = saxofone.GetProperty("id").GetInt32();
        var order = saxofone.GetProperty("sortOrder").GetInt32();
        await CreateAsync(member, "Saxofone", title: "Embocadura");

        try
        {
            foreach (var bad in new int?[] { -1, 1000, null })
            {
                (await AreaHttp.Errors(await admin.PutAsJsonAsync($"/api/naipes/config/{id}", new { isVisible = true, sortOrder = bad })))
                    .Should().ContainKey("sortOrder");
            }

            (await admin.PutAsJsonAsync("/api/naipes/config/999999", new { isVisible = true, sortOrder = 1 })).StatusCode.Should().Be(HttpStatusCode.NotFound);

            var hidden = (await AreaHttp.Json(await admin.PutAsJsonAsync($"/api/naipes/config/{id}", new { isVisible = false, sortOrder = 0 })))
                .EnumerateArray().Single(s => s.GetProperty("id").GetInt32() == id);
            hidden.GetProperty("isVisible").GetBoolean().Should().BeFalse();
            hidden.GetProperty("sortOrder").GetInt32().Should().Be(0);

            var board = await AreaHttp.Json(member, "/api/naipes?instrument=Saxofone");
            board.GetProperty("instruments").EnumerateArray().Select(i => i.GetProperty("value").GetString()).Should().NotContain("Saxofone");
            board.GetProperty("instrument").ValueKind.Should().Be(JsonValueKind.Null, "a hidden instrument is off the board");
            board.GetProperty("items").GetArrayLength().Should().Be(0);

            await AreaHttp.Json(await owner.PutAsJsonAsync($"/api/naipes/config/{id}", new { isVisible = true, sortOrder = 0 }));
            var instruments = (await AreaHttp.Json(member, "/api/naipes")).GetProperty("instruments").EnumerateArray()
                .Select(i => i.GetProperty("value").GetString()).ToList();
            instruments.IndexOf("Saxofone").Should().BeLessThan(instruments.IndexOf("Bandolim"), "order 0 comes before order 1");
            (await AreaHttp.Json(member, "/api/naipes?instrument=Saxofone")).GetProperty("items").GetArrayLength().Should().Be(1, "its content was kept");

            // Picture: JPEG, PNG or WebP by type and bytes, at most 5 MB; the old one is deleted.
            (await AreaHttp.Errors(await admin.PostAsync($"/api/naipes/config/{id}/picture", Picture(Svg, "image/svg+xml")))).Should().ContainKey("picture");
            (await AreaHttp.Errors(await admin.PostAsync($"/api/naipes/config/{id}/picture", Picture(Html, "image/png")))).Should().ContainKey("picture");
            var tooBig = new byte[5 * 1024 * 1024 + 1];
            Png.CopyTo(tooBig, 0);
            (await AreaHttp.Errors(await admin.PostAsync($"/api/naipes/config/{id}/picture", Picture(tooBig, "image/png"))))["picture"].Single()
                .Should().Contain("5 MB");

            var first = (await AreaHttp.Json(await admin.PostAsync($"/api/naipes/config/{id}/picture", Picture(Png, "image/png"))))
                .EnumerateArray().Single(s => s.GetProperty("id").GetInt32() == id).GetProperty("pictureUrl").GetString();
            first.Should().StartWith("https://pub-test.r2.dev/naipes/");
            _factory.Media.Verify(m => m.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), "image/png", "typeconfig_Saxofone"), Times.Once);
            (await AreaHttp.Json(member, "/api/naipes")).GetProperty("instruments").EnumerateArray()
                .Single(i => i.GetProperty("value").GetString() == "Saxofone").GetProperty("pictureUrl").GetString().Should().Be(first);

            await AreaHttp.Json(await owner.PostAsync($"/api/naipes/config/{id}/picture", Picture(Jpeg, "image/jpeg")));
            _factory.Media.Verify(m => m.DeleteMediaAsync(first!), Times.Once, "the replaced picture is deleted, as before");

            var removed = (await AreaHttp.Json(await admin.DeleteAsync($"/api/naipes/config/{id}/picture")))
                .EnumerateArray().Single(s => s.GetProperty("id").GetInt32() == id);
            removed.GetProperty("pictureUrl").ValueKind.Should().Be(JsonValueKind.Null);
        }
        finally
        {
            await admin.PutAsJsonAsync($"/api/naipes/config/{id}", new { isVisible = true, sortOrder = order });
        }
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

/// <summary>033: the Blazor Naipes pages and what only they used are gone; the menus link the React pages.</summary>
public class NaipesRetirementTests
{
    [Fact]
    public void BlazorPages_AndWhatOnlyTheyUsed_AreGone()
    {
        var src = Path.Combine(RepoRoot(), "src");
        foreach (var gone in new[]
                 {
                     "RTUB.Web/Pages/Activities/Naipes.razor", "RTUB.Web/Pages/Activities/NaipesConfig.razor",
                     "RTUB.Shared/Components/Cards/NaipeCard.razor", "RTUB.Shared/Components/Naipes/NaipeCommentItem.razor",
                     "RTUB.Shared/Components/Modals/DetailsModal.razor", "RTUB.Shared/Components/Modals/InfoSection.razor",
                     "RTUB.Shared/Components/Profile/ProfileField.razor", "RTUB.Web/wwwroot/css/3-components/naipe-card.css",
                     "RTUB.Web/wwwroot/css/3-components/details-modal.css", "RTUB.Web/wwwroot/css/4-pages/naipes-config.css",
                 })
        {
            File.Exists(Path.Combine(src, gone)).Should().BeFalse("{0} was retired in 033", gone);
        }

        File.ReadAllText(Path.Combine(src, "RTUB.Web", "wwwroot", "css", "site.css")).Should()
            .NotContain("naipe-card.css").And.NotContain("naipes-config.css").And.NotContain("details-modal.css");
        File.ReadAllText(Path.Combine(src, "RTUB.Web", "Shared", "MainLayout.razor")).Should()
            .Contain("href=\"/naipes\" data-enhance-nav=\"false\"", "a full navigation leaves Blazor for the React page");

        var web = typeof(RTUB.App).Assembly;
        web.GetType("RTUB.Pages.Activities.Naipes").Should().BeNull();
        web.GetType("RTUB.Pages.Activities.NaipesConfig").Should().BeNull();
    }

    [Fact]
    public void ReactPages_AreLinked_AndSayNoMigration()
    {
        var portal = Path.Combine(RepoRoot(), "src", "RTUB.Web", "portal", "src");
        File.ReadAllText(Path.Combine(portal, "content.ts")).Should().Contain("naipes: '/naipes'").And.Contain("naipesConfig: '/naipes/config'");
        var shell = File.ReadAllText(Path.Combine(portal, "MemberShell.tsx"));
        shell.Should().Contain("href: portal.naipes,").And.NotContain("blazor.naipes");
        var main = File.ReadAllText(Path.Combine(portal, "main.tsx"));
        main.Should().Contain("'/naipes': lazy(").And.Contain("'/naipes/config': lazy(");
        File.ReadAllText(Path.Combine(portal, "Naipes.tsx")).Should().Contain("href={portal.naipesConfig}");

        foreach (var file in new[] { "Naipes.tsx", "NaipesConfig.tsx", "naipesApi.ts" })
        {
            File.ReadAllText(Path.Combine(portal, file)).Should().NotContainAny(new[] { "migra", "Migra" }, file);
        }
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

/// <summary>The host with media storage and push replaced by recording fakes.</summary>
public sealed class NaipesApiFactory : TestWebApplicationFactory
{
    public Mock<IPushNotificationService> Push { get; } = new();
    public Mock<INaipeMediaStorageService> Media { get; } = new();

    public NaipesApiFactory()
    {
        Media.Setup(m => m.UploadVideoAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((Stream _, string name, string _, string folder) => $"https://pub-test.r2.dev/naipes/test/videos/{folder}/{Guid.NewGuid():N}_{name}");
        Media.Setup(m => m.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((Stream _, string name, string _, string folder) => $"https://pub-test.r2.dev/naipes/test/images/{folder}/{Guid.NewGuid():N}_{name}");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPushNotificationService>();
            services.AddSingleton(Push.Object);
            services.RemoveAll<INaipeMediaStorageService>();
            services.AddSingleton(Media.Object);
        });
    }
}
