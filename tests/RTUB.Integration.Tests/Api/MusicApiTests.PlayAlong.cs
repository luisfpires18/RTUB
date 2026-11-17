// MusicApiTests: PlayAlong tests.
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
    // ---------- helpers ----------

    // ---------- tocar junto (094) ----------

    [Fact]
    public async Task PlayAlong_IsUploadedByTheOwner_ReadByMembers_AndHiddenFromVisitors()
    {
        var seed = await SeedAsync();
        var (owner, _) = await SignInAsync("Owner");
        var (member, _) = await SignInAsync("Member");
        var visitor = await AnonymousAsync();
        await WithTokenAsync(owner);
        var url = $"/api/music/songs/{seed.SongWithPdf}/playalong";

        (await member.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.NotFound, "the song has no chart yet");
        (await owner.PutAsJsonAsync(url, Chart())).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await owner.PutAsJsonAsync(url, Chart(bpm: 90))).StatusCode.Should().Be(HttpStatusCode.NoContent, "a second upload replaces the chart");

        var read = await member.GetFromJsonAsync<JsonElement>(url);
        read.GetProperty("songId").GetInt32().Should().Be(seed.SongWithPdf);
        read.GetProperty("title").GetString().Should().Be("Abertura");
        read.GetProperty("hasAudio").GetBoolean().Should().BeTrue();
        var chart = read.GetProperty("chart");
        chart.GetProperty("bpm").GetDouble().Should().Be(90);
        chart.GetProperty("sections")[0].GetProperty("lines")[0].GetProperty("chords")[0].GetProperty("name").GetString().Should().Be("F");
        chart.TryGetProperty("extra", out _).Should().BeFalse("only the known fields are stored");
        chart.GetProperty("picking").GetBoolean().Should().BeFalse("dedilhado is off unless the chart asks for it");

        (await visitor.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "play-along is for members");
        bool Flag(JsonElement album) => album.GetProperty("songs").EnumerateArray()
            .Single(s => s.GetProperty("id").GetInt32() == seed.SongWithPdf).GetProperty("hasPlayAlong").GetBoolean();
        Flag(await member.GetFromJsonAsync<JsonElement>($"/api/music/albums/{seed.PublicAlbum}")).Should().BeTrue();
        Flag(await visitor.GetFromJsonAsync<JsonElement>($"/api/music/albums/{seed.PublicAlbum}")).Should().BeFalse();
        (await owner.GetFromJsonAsync<JsonElement>($"/api/music/songs/{seed.SongWithPdf}/edit")).GetProperty("hasPlayAlong").GetBoolean().Should().BeTrue();

        (await owner.DeleteAsync(url)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await member.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PlayAlong_Writes_AreOwnerOnly_NeedAToken_AndAValidChart()
    {
        var seed = await SeedAsync();
        var (owner, _) = await SignInAsync("Owner");
        var (member, _) = await SignInAsync("Member");
        var url = $"/api/music/songs/{seed.SongWithPdf}/playalong";

        (await owner.PutAsJsonAsync(url, Chart())).StatusCode.Should().Be(HttpStatusCode.BadRequest, "no antiforgery token");
        await WithTokenAsync(owner);
        await WithTokenAsync(member);

        (await member.PutAsJsonAsync(url, Chart())).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.DeleteAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await owner.PutAsJsonAsync(url, Chart(version: 2))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.PutAsJsonAsync(url, Chart(chord: "<script>"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.PutAsJsonAsync(url, new { version = 1 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        foreach (var role in new[] { "Mod", "Admin" })
        {
            var (manager, _) = await SignInAsync(role);
            await WithTokenAsync(manager);
            (await manager.PutAsJsonAsync(url, Chart())).StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{role} manages songs, not charts");
            (await manager.DeleteAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        (await owner.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.NotFound, "nothing invalid was stored");
    }

    [Fact]
    public async Task LearnList_ShowsMembersTheSongsWithAChart_InTheAlbumsTheySee()
    {
        var (listed, listedUser) = await SignInAsync("Member");
        var (other, _) = await SignInAsync("Member");
        var (owner, _) = await SignInAsync("Owner");
        var visitor = await AnonymousAsync();
        var seed = await SeedAsync(accessFor: listedUser.Id);
        await WithTokenAsync(owner);

        (await owner.PutAsJsonAsync($"/api/music/songs/{seed.SongWithPdf}/playalong", Chart())).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await owner.PutAsJsonAsync($"/api/music/songs/{seed.ExclusiveSong}/playalong", Chart())).StatusCode.Should().Be(HttpStatusCode.NoContent);

        int[] Ids(JsonElement list) => list.GetProperty("songs").EnumerateArray().Select(s => s.GetProperty("songId").GetInt32()).ToArray();
        Ids(await listed.GetFromJsonAsync<JsonElement>("/api/music/playalongs")).Should().Contain(new[] { seed.SongWithPdf, seed.ExclusiveSong });
        var seen = await other.GetFromJsonAsync<JsonElement>("/api/music/playalongs");
        Ids(seen).Should().Contain(seed.SongWithPdf).And.NotContain(seed.ExclusiveSong, "an exclusive album stays with the members on its list");
        seen.GetProperty("canManage").GetBoolean().Should().BeFalse();
        (await owner.GetFromJsonAsync<JsonElement>("/api/music/playalongs")).GetProperty("canManage").GetBoolean().Should().BeTrue();
        var mine = seen.GetProperty("songs").EnumerateArray().Single(s => s.GetProperty("songId").GetInt32() == seed.SongWithPdf);
        mine.GetProperty("title").GetString().Should().Be("Abertura");
        mine.GetProperty("albumId").GetInt32().Should().Be(seed.PublicAlbum);

        (await visitor.GetAsync("/api/music/playalongs")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var page = await visitor.GetAsync("/music/learn");
        page.StatusCode.Should().Be(HttpStatusCode.Redirect);
        page.Headers.Location!.ToString().Should().Be("/login?returnUrl=" + Uri.EscapeDataString("/music/learn"));
        (await other.GetStringAsync("/music/learn")).Should().Contain("id=\"root\"");
    }

    [Fact]
    public async Task LearnList_CountsViews_AndKeepsEachMembersStars()
    {
        var seed = await SeedAsync();
        var (member, _) = await SignInAsync("Member");
        var (other, _) = await SignInAsync("Member");
        var (owner, _) = await SignInAsync("Owner");
        await WithTokenAsync(owner);
        await WithTokenAsync(member);
        await WithTokenAsync(other);
        (await owner.PutAsJsonAsync($"/api/music/songs/{seed.SongWithPdf}/playalong", Chart())).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var song = $"/api/music/songs/{seed.SongWithPdf}/playalong";

        async Task<JsonElement> Mine(HttpClient c) => (await c.GetFromJsonAsync<JsonElement>("/api/music/playalongs"))
            .GetProperty("songs").EnumerateArray().Single(s => s.GetProperty("songId").GetInt32() == seed.SongWithPdf);

        (await member.PostAsync(song + "/view", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await other.PostAsync(song + "/view", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await member.PutAsync(song + "/star", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await member.PutAsync(song + "/star", null)).StatusCode.Should().Be(HttpStatusCode.NoContent, "starring twice is harmless");

        var mineA = await Mine(member);
        mineA.GetProperty("views").GetInt32().Should().Be(2);
        mineA.GetProperty("starred").GetBoolean().Should().BeTrue();
        (await Mine(other)).GetProperty("starred").GetBoolean().Should().BeFalse("a star is the member's own");

        (await member.DeleteAsync(song + "/star")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Mine(member)).GetProperty("starred").GetBoolean().Should().BeFalse();
        (await member.PostAsync($"/api/music/songs/{seed.SongWithText}/playalong/view", null)).StatusCode.Should().Be(HttpStatusCode.NotFound, "no chart, no view");
    }

    [Fact]
    public async Task LearnNotice_IsSentByTheOwnerOnly_ForASongWithAChart()
    {
        var seed = await SeedAsync();
        var (owner, _) = await SignInAsync("Owner");
        var (admin, _) = await SignInAsync("Admin");
        await WithTokenAsync(owner);
        await WithTokenAsync(admin);
        var url = $"/api/music/songs/{seed.SongWithPdf}/playalong";

        (await owner.DeleteAsync(url)).StatusCode.Should().BeOneOf(HttpStatusCode.NoContent, HttpStatusCode.NotFound);
        (await owner.PostAsJsonAsync(url + "/notify", new { message = "" })).StatusCode.Should().Be(HttpStatusCode.NotFound, "the song has no chart");
        (await owner.PutAsJsonAsync(url, Chart())).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await admin.PostAsJsonAsync(url + "/notify", new { message = "" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await owner.PostAsJsonAsync(url + "/notify", new { message = new string('a', 301) })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.PostAsJsonAsync(url + "/notify", new { message = "" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await owner.PostAsJsonAsync(url + "/notify", new { message = "Experimentem!" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PlayAlong_OfAnExclusiveAlbum_IsOnlyForMembersOnItsList()
    {
        var (listed, listedUser) = await SignInAsync("Member");
        var (other, _) = await SignInAsync("Member");
        var (owner, _) = await SignInAsync("Owner");
        var seed = await SeedAsync(accessFor: listedUser.Id);
        await WithTokenAsync(owner);
        var url = $"/api/music/songs/{seed.ExclusiveSong}/playalong";
        (await owner.PutAsJsonAsync(url, Chart())).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await listed.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await other.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
