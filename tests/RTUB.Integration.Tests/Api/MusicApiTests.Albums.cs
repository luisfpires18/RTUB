// MusicApiTests: Albums tests.
using System.Net;
using System.Text.Json;
using FluentAssertions;
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

public partial class MusicApiTests
{
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
            "id", "title", "trackNumber", "lyricAuthor", "musicAuthor", "adaptation", "hasAudio", "playCount", "links", "videoCount", "hasPlayAlong");
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
}
