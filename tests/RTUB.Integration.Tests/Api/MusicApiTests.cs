using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// /api/music (React track 006) through the real host: real Identity roles, the real login, real
/// antiforgery, the real database. Storage is faked - no test reaches Cloudflare R2 or uploads a file.
/// </summary>
public class MusicApiTests : IClassFixture<MusicApiFactory>
{
    private readonly MusicApiFactory _factory;

    public MusicApiTests(MusicApiFactory factory)
    {
        _factory = factory;
    }

    // ---------- albums: visibility ----------

    [Fact]
    public async Task AlbumList_ForAVisitor_HasOnlyAlbumsThatAreNeitherPrivateNorExclusive()
    {
        var seed = await SeedAsync();
        var client = await AnonymousAsync();

        var list = await client.GetFromJsonAsync<JsonElement>("/api/music/albums");

        var ids = AlbumIds(list);
        ids.Should().Contain(seed.PublicAlbum);
        ids.Should().NotContain(new[] { seed.PrivateAlbum, seed.ExclusiveAlbum, seed.ExclusivePublicAlbum },
            "an exclusive album stays hidden from visitors even when it is not flagged private");
        var can = list.GetProperty("permissions");
        foreach (var flag in new[] { "isMember", "canManage", "canDelete", "canManageExclusive", "canSeeStatistics", "canSeeDetailedStatistics" })
        {
            can.GetProperty(flag).GetBoolean().Should().BeFalse(flag);
        }
    }

    [Fact]
    public async Task AlbumList_ForAMember_AddsPrivateAlbums_AndOnlyTheExclusiveAlbumsTheyAreListedOn()
    {
        var (listed, listedUser) = await SignInAsync("Member");
        var (other, _) = await SignInAsync("Member");
        var seed = await SeedAsync(accessFor: listedUser.Id);

        var listedIds = AlbumIds(await listed.GetFromJsonAsync<JsonElement>("/api/music/albums"));
        var otherIds = AlbumIds(await other.GetFromJsonAsync<JsonElement>("/api/music/albums"));

        listedIds.Should().Contain(new[] { seed.PublicAlbum, seed.PrivateAlbum, seed.ExclusiveAlbum, seed.StaleAccessAlbum });
        otherIds.Should().Contain(new[] { seed.PublicAlbum, seed.PrivateAlbum, seed.StaleAccessAlbum },
            "access rows of an album that is no longer exclusive change nothing");
        otherIds.Should().NotContain(new[] { seed.ExclusiveAlbum, seed.ExclusivePublicAlbum });
    }

    [Fact]
    public async Task AlbumList_ForTheOwner_IncludesEveryExclusiveAlbum()
    {
        var (owner, _) = await SignInAsync("Owner");
        var seed = await SeedAsync();

        var ids = AlbumIds(await owner.GetFromJsonAsync<JsonElement>("/api/music/albums"));

        ids.Should().Contain(new[] { seed.ExclusiveAlbum, seed.ExclusivePublicAlbum });
    }

    [Fact]
    public async Task AlbumList_IsOrderedByYear_WithUndatedAlbumsLast_ThenById()
    {
        var (admin, _) = await SignInAsync("Admin");
        await SeedAsync();

        var years = (await admin.GetFromJsonAsync<JsonElement>("/api/music/albums")).GetProperty("albums").EnumerateArray()
            .Select(a => (Year: a.GetProperty("year").ValueKind == JsonValueKind.Null ? (int?)null : a.GetProperty("year").GetInt32(), Id: a.GetProperty("id").GetInt32()))
            .ToList();

        var keys = years.Select(a => (Undated: a.Year == null, Year: a.Year ?? 0, a.Id)).ToList();
        keys.Should().Equal(keys.OrderBy(k => k.Undated).ThenBy(k => k.Year).ThenBy(k => k.Id));
    }

    // ---------- album detail ----------

    [Fact]
    public async Task AlbumDetail_GivesSongsInTrackOrder_WithOnlyBrowserSafeFields()
    {
        var seed = await SeedAsync();
        var client = await AnonymousAsync();

        var body = await client.GetStringAsync($"/api/music/albums/{seed.PublicAlbum}");
        using var json = JsonDocument.Parse(body);
        var songs = json.RootElement.GetProperty("songs").EnumerateArray().ToList();

        songs.Select(s => s.GetProperty("title").GetString()).Should().Equal("Abertura", "Serenata", MissingFileSong, "Sem faixa");
        songs[0].EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            "id", "title", "trackNumber", "lyricAuthor", "musicAuthor", "adaptation", "hasAudio", "playCount", "links", "videoCount");
        songs.Should().OnlyContain(s => s.GetProperty("videoCount").ValueKind == JsonValueKind.Null, "videos are for members only");
        songs[0].GetProperty("links").EnumerateArray().Select(l => l.GetProperty("url").GetString())
            .Should().Equal("https://open.spotify.com/track/abertura", "https://www.youtube.com/watch?v=abertura");
        body.Should().NotContain("javascript:", "stored links that are not http(s) never reach the browser");
        body.Should().NotContain("Letra guardada", "lyrics are fetched on demand, not with the album");
        body.Should().NotContain("albums/", "no storage object key is exposed");
    }

    [Fact]
    public async Task AlbumDetail_RefusesWhatTheCallerMayNotSee()
    {
        var seed = await SeedAsync();
        var visitor = await AnonymousAsync();
        var (member, _) = await SignInAsync("Member");

        (await visitor.GetAsync($"/api/music/albums/{seed.PrivateAlbum}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await visitor.GetAsync($"/api/music/albums/{seed.ExclusivePublicAlbum}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await member.GetAsync($"/api/music/albums/{seed.ExclusiveAlbum}")).StatusCode.Should().Be(HttpStatusCode.NotFound,
            "a member not on the list cannot tell the album exists");
        (await member.GetAsync("/api/music/albums/999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await member.GetAsync($"/api/music/albums/{seed.PrivateAlbum}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------- lyrics ----------

    [Fact]
    public async Task Lyrics_PreferTheSongbookPdf_FallBackToText_AndCanBeEmpty()
    {
        var seed = await SeedAsync();
        var client = await AnonymousAsync();

        var pdf = await client.GetFromJsonAsync<JsonElement>($"/api/music/songs/{seed.SongWithPdf}/lyrics");
        var text = await client.GetFromJsonAsync<JsonElement>($"/api/music/songs/{seed.SongWithText}/lyrics");
        var none = await client.GetFromJsonAsync<JsonElement>($"/api/music/songs/{seed.SongWithout}/lyrics");

        pdf.GetProperty("pdfUrl").GetString().Should().StartWith("https://test-account-id.r2.cloudflarestorage.com/");
        text.GetProperty("pdfUrl").ValueKind.Should().Be(JsonValueKind.Null);
        text.GetProperty("text").GetString().Should().Be("Letra guardada");
        none.GetProperty("pdfUrl").ValueKind.Should().Be(JsonValueKind.Null);
        none.GetProperty("text").ValueKind.Should().Be(JsonValueKind.Null);
        (await client.GetAsync($"/api/music/songs/{seed.PrivateSong}/lyrics")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------- plays and cooldown ----------

    [Fact]
    public async Task Play_CountsOnce_ThenTheCooldownStopsRepeats_WithoutStoppingPlayback()
    {
        var seed = await SeedAsync();
        var (member, user) = await SignInAsync("Member");
        await WithTokenAsync(member);

        var first = await member.PostAsync($"/api/music/songs/{seed.SongWithPdf}/plays", null);
        var second = await member.PostAsync($"/api/music/songs/{seed.SongWithPdf}/plays", null);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var a = await first.Content.ReadFromJsonAsync<JsonElement>();
        var b = await second.Content.ReadFromJsonAsync<JsonElement>();
        a.GetProperty("counted").GetBoolean().Should().BeTrue();
        a.GetProperty("audioUrl").GetString().Should().StartWith("https://test-account-id.r2.cloudflarestorage.com/");
        b.GetProperty("counted").GetBoolean().Should().BeFalse("a repeat inside the song's cooldown is not counted");
        b.GetProperty("audioUrl").GetString().Should().NotBeNullOrEmpty("the cooldown never stops the song from playing");
        b.GetProperty("playCount").GetInt32().Should().Be(a.GetProperty("playCount").GetInt32());
        (await PlaysAsync(seed.SongWithPdf, user.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Play_ByAVisitor_IsCountedWithoutAUser_AndCooledDownByAddress()
    {
        var seed = await SeedAsync();
        var visitor = await AnonymousAsync("10.61.0.1");
        await WithTokenAsync(visitor);

        (await (await visitor.PostAsync($"/api/music/songs/{seed.SongWithText}/plays", null)).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("counted").GetBoolean().Should().BeTrue();
        (await (await visitor.PostAsync($"/api/music/songs/{seed.SongWithText}/plays", null)).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("counted").GetBoolean().Should().BeFalse();

        (await PlaysAsync(seed.SongWithText, null)).Should().Be(1);
    }

    [Fact]
    public async Task Play_RefusesSongsWithoutAudio_PrivateSongsForVisitors_AndRequestsWithoutToken()
    {
        var seed = await SeedAsync();
        var visitor = await AnonymousAsync("10.61.0.2");

        (await visitor.PostAsync($"/api/music/songs/{seed.SongWithPdf}/plays", null)).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "every Music write needs the antiforgery token");

        await WithTokenAsync(visitor);
        var noAudio = await visitor.PostAsync($"/api/music/songs/{seed.SongWithout}/plays", null);
        noAudio.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await noAudio.Content.ReadAsStringAsync()).Should().Contain("music:audio-unavailable");

        var missingFile = await visitor.PostAsync($"/api/music/songs/{seed.SongMissingFile}/plays", null);
        missingFile.StatusCode.Should().Be(HttpStatusCode.NotFound, "flagged as having audio, but the file is not in storage");

        (await visitor.PostAsync($"/api/music/songs/{seed.PrivateSong}/plays", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await PlaysAsync(seed.SongWithout, null)).Should().Be(0);
        (await PlaysAsync(seed.SongMissingFile, null)).Should().Be(0);
    }

    // ---------- statistics ----------

    [Fact]
    public async Task Statistics_AreForMembers_OverAlbumsTheyCanSee_WithPerMemberDetailForAdminsOnly()
    {
        var seed = await SeedAsync();
        await AddPlaysAsync(seed.SongWithPdf, 3);
        await AddPlaysAsync(seed.ExclusiveSong, 2);
        var visitor = await AnonymousAsync();
        var (member, _) = await SignInAsync("Member");
        var (admin, _) = await SignInAsync("Admin");

        (await visitor.GetAsync("/api/music/statistics")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var forMember = await member.GetFromJsonAsync<JsonElement>("/api/music/statistics");
        var memberSongs = forMember.GetProperty("songs").EnumerateArray().Select(s => s.GetProperty("songId").GetInt32()).ToList();
        memberSongs.Should().Contain(seed.SongWithPdf);
        memberSongs.Should().NotContain(seed.ExclusiveSong, "an exclusive album's songs stay hidden from members not on its list");
        forMember.GetProperty("members").ValueKind.Should().Be(JsonValueKind.Null);

        var forAdmin = await admin.GetFromJsonAsync<JsonElement>("/api/music/statistics");
        forAdmin.GetProperty("members").ValueKind.Should().Be(JsonValueKind.Array);
        forAdmin.GetRawText().Should().NotContain("\"userId\"").And.NotContain("@test.com", "no ids or contact details");
    }

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

    // ---------- helpers ----------

    private sealed record Seed(
        int PublicAlbum, int PrivateAlbum, int ExclusiveAlbum, int ExclusivePublicAlbum, int StaleAccessAlbum,
        int SongWithPdf, int SongWithText, int SongWithout, int SongMissingFile, int PrivateSong, int ExclusiveSong);

    /// <summary>A fresh, uniquely named set of albums for one test (the database is shared by the class).</summary>
    private async Task<Seed> SeedAsync(string? accessFor = null)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Album NewAlbum(string title, int? year, bool isPrivate = false, bool exclusive = false) =>
            Album.Create($"{title} {tag}", year, null, isPrivate, exclusive);

        var publicAlbum = NewAlbum("Público", 1995);
        var privateAlbum = NewAlbum("Privado", null, isPrivate: true);
        var exclusive = NewAlbum("Exclusivo", null, isPrivate: true, exclusive: true);
        var exclusivePublic = NewAlbum("Exclusivo aberto", 2000, exclusive: true);
        var stale = NewAlbum("Antigo exclusivo", null, isPrivate: true);
        db.Albums.AddRange(publicAlbum, privateAlbum, exclusive, exclusivePublic, stale);
        await db.SaveChangesAsync();

        var withPdf = Song.Create("Abertura", publicAlbum.Id, 1, "Letrista", "Compositor", spotifyUrl: "https://open.spotify.com/track/abertura", hasMusic: true);
        withPdf.YouTubeUrls.Add(SongYouTubeUrl.Create(0, "https://www.youtube.com/watch?v=abertura"));
        withPdf.YouTubeUrls.Add(SongYouTubeUrl.Create(0, "javascript:alert(1)"));
        var withText = Song.Create("Serenata", publicAlbum.Id, 2, hasMusic: true);
        withText.SetLyrics("Letra guardada");
        var without = Song.Create("Sem faixa", publicAlbum.Id, null);
        var missing = Song.Create(MissingFileSong, publicAlbum.Id, 3, hasMusic: true);
        var privateSong = Song.Create("Privada", privateAlbum.Id, 1, hasMusic: true);
        var exclusiveSong = Song.Create("Exclusiva", exclusive.Id, 1, hasMusic: true);
        db.Songs.AddRange(without, withText, withPdf, missing, privateSong, exclusiveSong);
        await db.SaveChangesAsync();

        if (accessFor is not null)
        {
            db.AlbumAccesses.Add(AlbumAccess.Create(exclusive.Id, accessFor));
        }

        // Left over from when the album was exclusive: must not matter any more.
        var someone = await db.Users.Select(u => u.Id).FirstAsync();
        db.AlbumAccesses.Add(AlbumAccess.Create(stale.Id, someone));
        await db.SaveChangesAsync();

        _factory.Storage.Pdfs[(publicAlbum.Title, withPdf.Title)] = "https://test-account-id.r2.cloudflarestorage.com/test-bucket/lyrics/abertura.pdf?X-Amz-Signature=x";
        _factory.Storage.MissingAudio.Add(missing.Title);

        return new Seed(publicAlbum.Id, privateAlbum.Id, exclusive.Id, exclusivePublic.Id, stale.Id,
            withPdf.Id, withText.Id, without.Id, missing.Id, privateSong.Id, exclusiveSong.Id);
    }

    private async Task AddPlaysAsync(int songId, int count)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.Select(u => u.Id).FirstAsync();
        for (var i = 0; i < count; i++)
        {
            db.SongPlayCounts.Add(new SongPlayCount { SongId = songId, UserId = user, PlayedAt = DateTime.UtcNow });
        }

        await db.SaveChangesAsync();
    }

    private async Task<int> PlaysAsync(int songId, string? userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.SongPlayCounts.CountAsync(p => p.SongId == songId && p.UserId == userId);
    }

    private async Task<Album> AlbumAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Albums.AsNoTracking().FirstAsync(a => a.Id == id);
    }

    private async Task<bool> AlbumExistsAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Albums.AnyAsync(a => a.Id == id);
    }

    private async Task<List<string>> AccessAsync(int albumId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().AlbumAccesses
            .Where(a => a.AlbumId == albumId).Select(a => a.UserId).ToListAsync();
    }

    private static List<int> AlbumIds(JsonElement list) =>
        list.GetProperty("albums").EnumerateArray().Select(a => a.GetProperty("id").GetInt32()).ToList();

    private const string MissingFileSong = "Ficheiro em falta";

    private static int _ip;

    private Task<(HttpClient Client, ApplicationUser User)> SignInAsync(string role)
    {
        var n = Interlocked.Increment(ref _ip);
        return CookieTestSession.SignInAsync(_factory, $"music-{role.ToLowerInvariant()}-{Guid.NewGuid():N}"[..30],
            $"10.62.{n / 250}.{n % 250 + 1}", role);
    }

    private Task<HttpClient> AnonymousAsync(string ip = "10.63.0.1")
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, ip);
        return Task.FromResult(client);
    }

    private static async Task WithTokenAsync(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/public/antiforgery-token");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }

    private static MultipartFormDataContent AlbumForm(string title, string year = "", bool isPrivate = false, bool exclusive = false,
        string[]? access = null, HttpContent? cover = null, string coverType = "image/webp")
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(title), "title" },
            { new StringContent(year), "year" },
            { new StringContent(string.Empty), "description" },
            { new StringContent(isPrivate ? "true" : "false"), "isPrivate" },
            { new StringContent(exclusive ? "true" : "false"), "isExclusive" },
        };
        foreach (var id in access ?? Array.Empty<string>())
        {
            form.Add(new StringContent(id), "accessUserIds");
        }

        if (cover is not null)
        {
            cover.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(coverType);
            form.Add(cover, "cover", "album-cover.webp");
        }

        return form;
    }

    /// <summary>Not a real image - storage is faked, the server only checks type and size.</summary>
    private static ByteArrayContent Webp() => new(new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4 });

    private static MultipartFormDataContent VideoForm(string title, string type, string fileName = "clip.mp4")
    {
        var file = new ByteArrayContent(new byte[] { 0, 0, 0, 24, 0x66, 0x74, 0x79, 0x70 });
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(type);
        return new MultipartFormDataContent { { file, "file", fileName }, { new StringContent(title), "title" } };
    }

    private static object SongBody(string title, string? spotify = null, int? track = null, string[]? youTube = null, string? lyrics = null) => new
    {
        title,
        trackNumber = track,
        lyricAuthor = "",
        musicAuthor = "",
        adaptation = "",
        spotifyUrl = spotify ?? "",
        hasAudio = true,
        youTubeUrls = youTube ?? Array.Empty<string>(),
        lyrics = lyrics ?? "",
    };
}

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

public sealed class FakeMusicStorage : IAudioStorageService, ILyricStorageService, IImageStorageService, ISongVideoStorageService
{
    private const string Signed = "https://test-account-id.r2.cloudflarestorage.com/test-bucket";

    public ConcurrentDictionary<(string Album, string Song), string> Pdfs { get; } = new();
    public ConcurrentBag<string> MissingAudio { get; } = new();
    public ConcurrentBag<string> UploadedImages { get; } = new();
    public ConcurrentBag<string> DeletedImages { get; } = new();
    public ConcurrentBag<string> DeletedVideos { get; } = new();

    public Task<string?> GetAudioUrlAsync(string albumTitle, int? trackNumber, string songTitle) =>
        Task.FromResult(MissingAudio.Contains(songTitle) ? null : $"{Signed}/audio/{Guid.NewGuid():N}.mp3?X-Amz-Signature=x");

    public Task<bool> AudioFileExistsAsync(string albumTitle, int? trackNumber, string songTitle) => Task.FromResult(!MissingAudio.Contains(songTitle));

    public Task<string?> GetLyricPdfUrlAsync(string albumTitle, string songTitle) =>
        Task.FromResult(Pdfs.TryGetValue((albumTitle, songTitle), out var url) ? url : null);

    public Task<bool> LyricPdfExistsAsync(string albumTitle, string songTitle) => Task.FromResult(Pdfs.ContainsKey((albumTitle, songTitle)));

    /// <summary>An album with this in its title gets a storage failure on cover upload.</summary>
    public const string FailingTitle = "Falha de armazenamento";

    public Task<string> UploadImageAsync(Stream fileStream, string fileName, string contentType, string entityType, string entityId)
    {
        if (entityId.StartsWith("falha_de_armazenamento", StringComparison.Ordinal))
        {
            throw new HttpRequestException("storage unreachable (test)");
        }

        var url = $"https://pub-test.r2.dev/images/{entityType}/{entityId}-{Guid.NewGuid():N}.webp";
        UploadedImages.Add(url);
        return Task.FromResult(url);
    }

    public Task DeleteImageAsync(string imageUrl)
    {
        DeletedImages.Add(imageUrl);
        return Task.CompletedTask;
    }

    public Task<bool> ImageExistsAsync(string imageUrl) => Task.FromResult(UploadedImages.Contains(imageUrl));

    public Task<string> UploadVideoAsync(Stream fileStream, string fileName, string contentType, int songId) =>
        Task.FromResult($"https://pub-test.r2.dev/videos/{songId}/{Guid.NewGuid():N}.mp4");

    public Task DeleteVideoAsync(string videoUrl)
    {
        DeletedVideos.Add(videoUrl);
        return Task.CompletedTask;
    }

    public Task<bool> VideoExistsAsync(string videoUrl) => Task.FromResult(true);
}
