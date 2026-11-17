// NaipesApiTests: Uploads tests.
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using RTUB.Application.DTOs;
using Xunit;

namespace RTUB.Integration.Tests.Api;

public partial class NaipesApiTests
{
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
            n.Title == "Novo vídeo em Guitarra" && n.Body.Contains("Rasgado") && n.Url == "/naipes?instrument=Guitarra")), Times.Once);

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
}
