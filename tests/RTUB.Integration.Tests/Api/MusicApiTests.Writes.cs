// MusicApiTests: Writes tests.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;

namespace RTUB.Integration.Tests.Api;

public partial class MusicApiTests
{
    // ---------- album management ----------

    [Fact]
    public async Task AlbumWrites_AreRefusedToVisitorsAndMembers()
    {
        var seed = await SeedAsync();
        var visitor = await AnonymousAsync("10.61.0.3");
        var (member, _) = await SignInAsync("Member");
        await WithTokenAsync(visitor);
        await WithTokenAsync(member);

        (await visitor.PostAsync("/api/music/albums", AlbumForm("Pirata"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.PostAsync("/api/music/albums", AlbumForm("Pirata"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.PutAsync($"/api/music/albums/{seed.PublicAlbum}", AlbumForm("Pirata"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.DeleteAsync($"/api/music/albums/{seed.PublicAlbum}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.GetAsync($"/api/music/albums/{seed.PublicAlbum}/edit")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.GetAsync("/api/music/members")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Mod_CreatesAndEditsAlbums_ButCannotDeleteThem_OrMakeThemExclusive()
    {
        var (mod, _) = await SignInAsync("Mod");
        await WithTokenAsync(mod);

        var created = await mod.PostAsync("/api/music/albums", AlbumForm($"Novo {Guid.NewGuid():N}", year: "2020", exclusive: true));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        (await AlbumAsync(id)).IsExclusive.Should().BeFalse("only the Owner makes an album exclusive");

        (await mod.PutAsync($"/api/music/albums/{id}", AlbumForm("Renomeado", isPrivate: true))).StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = await AlbumAsync(id);
        saved.Title.Should().Be("Renomeado");
        saved.IsPrivate.Should().BeTrue();

        (await mod.DeleteAsync($"/api/music/albums/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ModEditingAnExclusiveAlbumTheyAreOn_KeepsItExclusive()
    {
        var (mod, modUser) = await SignInAsync("Mod");
        var seed = await SeedAsync(accessFor: modUser.Id);
        await WithTokenAsync(mod);

        (await mod.PutAsync($"/api/music/albums/{seed.ExclusiveAlbum}", AlbumForm("Exclusivo editado"))).StatusCode.Should().Be(HttpStatusCode.OK);

        var album = await AlbumAsync(seed.ExclusiveAlbum);
        album.IsExclusive.Should().BeTrue("the retired page silently cleared this when a Mod saved");
        album.Title.Should().Be("Exclusivo editado");
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Owner")]
    public async Task AdminAndOwner_DeleteAlbums_AndTheCoverGoesWithThem(string role)
    {
        var (client, _) = await SignInAsync(role);
        await WithTokenAsync(client);
        var created = await client.PostAsync("/api/music/albums", AlbumForm($"Apagar {Guid.NewGuid():N}", cover: Webp()));
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        var cover = (await AlbumAsync(id)).ImageUrl;

        (await client.DeleteAsync($"/api/music/albums/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await AlbumExistsAsync(id)).Should().BeFalse();
        _factory.Storage.DeletedImages.Should().Contain(cover!);
    }

    [Fact]
    public async Task Owner_MakesAnAlbumExclusive_WithAnAccessListOfRealMembers()
    {
        var (owner, _) = await SignInAsync("Owner");
        var (_, listed) = await SignInAsync("Member");
        await WithTokenAsync(owner);

        var created = await owner.PostAsync("/api/music/albums",
            AlbumForm($"Só nosso {Guid.NewGuid():N}", exclusive: true, access: new[] { listed.Id, "not-a-user" }));
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        (await AlbumAsync(id)).IsExclusive.Should().BeTrue();
        (await AccessAsync(id)).Should().Equal(listed.Id);
        var edit = await owner.GetFromJsonAsync<JsonElement>($"/api/music/albums/{id}/edit");
        edit.GetProperty("accessMembers").EnumerateArray().Select(m => m.GetProperty("id").GetString()).Should().Equal(listed.Id);
    }

    [Fact]
    public async Task Cover_IsUploadedThroughStorage_AndValidated()
    {
        var (mod, _) = await SignInAsync("Mod");
        await WithTokenAsync(mod);

        var ok = await mod.PostAsync("/api/music/albums", AlbumForm($"Com capa {Guid.NewGuid():N}", cover: Webp()));
        ok.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ok.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("coverUrl").GetString().Should().StartWith("https://pub-test.r2.dev/");

        var uploads = _factory.Storage.UploadedImages.Count;
        var bad = await mod.PostAsync("/api/music/albums", AlbumForm("Capa errada", cover: new ByteArrayContent(new byte[] { 1, 2, 3 }), coverType: "application/pdf"));
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await bad.Content.ReadAsStringAsync()).Should().Contain("\"cover\"");
        _factory.Storage.UploadedImages.Count.Should().Be(uploads, "an invalid cover is never uploaded");

        var noTitle = await mod.PostAsync("/api/music/albums", AlbumForm(" ", year: "1800"));
        var errors = (await noTitle.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        errors.TryGetProperty("title", out _).Should().BeTrue();
        errors.TryGetProperty("year", out _).Should().BeTrue();
    }

    [Fact]
    public async Task AStorageFailure_AnswersAsAGenericProblem_AndCreatesNothing()
    {
        var (mod, _) = await SignInAsync("Mod");
        await WithTokenAsync(mod);
        var title = $"{FakeMusicStorage.FailingTitle} {Guid.NewGuid():N}";

        var response = await mod.PostAsync("/api/music/albums", AlbumForm(title, cover: Webp()));

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("Exception").And.NotContain("at RTUB.", "no stack trace leaves the server");
        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Albums.AnyAsync(a => a.Title == title))
            .Should().BeFalse("the cover is uploaded before the album is created");
    }

    // ---------- song management ----------

    [Fact]
    public async Task Mod_CreatesAndEditsSongs_ButCannotDeleteThem()
    {
        var seed = await SeedAsync();
        var (mod, _) = await SignInAsync("Mod");
        await WithTokenAsync(mod);

        var created = await mod.PostAsJsonAsync($"/api/music/albums/{seed.PublicAlbum}/songs", SongBody("Nova", youTube: new[] { "https://youtu.be/x", "" }));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        (await mod.PutAsJsonAsync($"/api/music/songs/{id}", SongBody("Nova editada", youTube: new[] { "https://youtu.be/y" }, lyrics: "Texto"))).StatusCode
            .Should().Be(HttpStatusCode.OK);
        var edit = await mod.GetFromJsonAsync<JsonElement>($"/api/music/songs/{id}/edit");
        edit.GetProperty("title").GetString().Should().Be("Nova editada");
        edit.GetProperty("youTubeUrls").EnumerateArray().Select(u => u.GetString()).Should().Equal("https://youtu.be/y");
        edit.GetProperty("lyrics").GetString().Should().Be("Texto");

        (await mod.DeleteAsync($"/api/music/songs/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SongWrites_ValidateInput_AndRefuseMembers()
    {
        var seed = await SeedAsync();
        var (mod, _) = await SignInAsync("Mod");
        var (member, _) = await SignInAsync("Member");
        var (admin, _) = await SignInAsync("Admin");
        await WithTokenAsync(mod);
        await WithTokenAsync(member);
        await WithTokenAsync(admin);

        var bad = await mod.PostAsJsonAsync($"/api/music/albums/{seed.PublicAlbum}/songs",
            SongBody("", spotify: "javascript:alert(1)", track: 1000));
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = (await bad.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        errors.TryGetProperty("title", out _).Should().BeTrue();
        errors.TryGetProperty("spotifyUrl", out _).Should().BeTrue();
        errors.TryGetProperty("trackNumber", out _).Should().BeTrue();

        (await member.PostAsJsonAsync($"/api/music/albums/{seed.PublicAlbum}/songs", SongBody("Intrusa"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await mod.PostAsJsonAsync("/api/music/albums/999999/songs", SongBody("Perdida"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.DeleteAsync($"/api/music/songs/{seed.SongWithout}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ---------- videos ----------

    [Fact]
    public async Task Videos_AreForMembers_WhoUploadAndDeleteTheirOwn_ButNotOthers()
    {
        var seed = await SeedAsync();
        var visitor = await AnonymousAsync();
        var (uploader, _) = await SignInAsync("Member");
        var (other, _) = await SignInAsync("Member");
        await WithTokenAsync(uploader);
        await WithTokenAsync(other);

        (await visitor.GetAsync($"/api/music/songs/{seed.SongWithPdf}/videos")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var upload = await uploader.PostAsync($"/api/music/songs/{seed.SongWithPdf}/videos", VideoForm("Ensaio", "video/mp4"));
        upload.StatusCode.Should().Be(HttpStatusCode.Created);
        var videoId = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var seenByOther = await other.GetFromJsonAsync<JsonElement>($"/api/music/songs/{seed.SongWithPdf}/videos");
        var mine = seenByOther.EnumerateArray().Single(v => v.GetProperty("id").GetInt32() == videoId);
        mine.GetProperty("canDelete").GetBoolean().Should().BeFalse();
        mine.GetProperty("url").GetString().Should().StartWith("https://pub-test.r2.dev/");
        mine.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("id", "title", "url", "mimeType", "canDelete");

        (await other.DeleteAsync($"/api/music/videos/{videoId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await uploader.DeleteAsync($"/api/music/videos/{videoId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        _factory.Storage.DeletedVideos.Should().ContainSingle(u => u.Contains($"/{seed.SongWithPdf}/"));

        (await uploader.PostAsync($"/api/music/songs/{seed.SongWithPdf}/videos", VideoForm("Não é vídeo", "application/pdf", "doc.pdf"))).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
    }
}
