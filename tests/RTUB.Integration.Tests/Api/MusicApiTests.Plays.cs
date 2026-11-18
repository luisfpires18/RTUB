// MusicApiTests: Plays tests.
using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace RTUB.Integration.Tests.Api;

public partial class MusicApiTests
{
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
}
