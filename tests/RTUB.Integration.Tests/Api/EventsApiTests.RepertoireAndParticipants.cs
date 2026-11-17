// EventsApiTests tests: repertoire, participants and stats.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using Xunit;

namespace RTUB.Integration.Tests.Api;
public partial class EventsApiTests
{
    // ---------- repertoire (Admin/Owner, 012D) ----------

    [Fact]
    public async Task RepertoireWrites_AreRefused_ForVisitorsMembersAndMods_AndWithoutTheAntiforgeryHeader()
    {
        var day = DateTime.Today.AddDays(320);
        var id = await AddEventAsync("Arraial com repertório protegido", day);
        var song = await AddSongAsync("Intocável");
        var item = await AddRepertoireItemAsync(id, song, day, 1);
        var other = await AddSongAsync("Nova para o arraial");
        var date = day.ToString("yyyy-MM-dd");
        var visitor = Anonymous();
        await WithTokenAsync(visitor);
        var (member, _) = await SignInAsync();
        await WithTokenAsync(member);
        var (mod, _) = await SignInAsync("Mod");
        await WithTokenAsync(mod);
        var (adminWithoutToken, _) = await SignInAsync("Admin");

        foreach (var (client, expected) in new[] { (visitor, HttpStatusCode.Unauthorized), (member, HttpStatusCode.Forbidden), (mod, HttpStatusCode.Forbidden) })
        {
            (await client.GetAsync($"/api/events/{id}/repertoire")).StatusCode.Should().Be(expected);
            (await client.GetAsync($"/api/events/{id}/repertoire/songs?q=")).StatusCode.Should().Be(expected);
            (await client.PostAsJsonAsync($"/api/events/{id}/repertoire", new { songId = other, date })).StatusCode.Should().Be(expected);
            (await client.PostAsJsonAsync($"/api/events/{id}/repertoire/reorder", new { date, itemIds = new[] { item } })).StatusCode.Should().Be(expected);
            (await client.DeleteAsync($"/api/events/{id}/repertoire/{item}")).StatusCode.Should().Be(expected);
            (await client.DeleteAsync($"/api/events/{id}/repertoire/days/{date}")).StatusCode.Should().Be(expected);
        }

        (await adminWithoutToken.PostAsJsonAsync($"/api/events/{id}/repertoire", new { songId = other, date })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await adminWithoutToken.DeleteAsync($"/api/events/{id}/repertoire/{item}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await adminWithoutToken.DeleteAsync($"/api/events/{id}/repertoire/days/{date}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var page = await member.GetFromJsonAsync<JsonElement>($"/api/events/{id}");
        page.GetProperty("member").GetProperty("repertoire")[0].GetProperty("songs").EnumerateArray().Select(s => s.GetString())
            .Should().Equal("Intocável");
        (await Anonymous().GetFromJsonAsync<JsonElement>($"/api/events/{id}")).GetProperty("member").ValueKind
            .Should().Be(JsonValueKind.Null, "visitors never saw the repertoire");
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Owner")]
    public async Task AdminOrOwner_ManageTheRepertoire_AndMembersReadIt(string role)
    {
        var day = DateTime.Today.AddDays(role == "Admin" ? 321 : 322);
        var id = await AddEventAsync($"Arraial cantado ({role})", day);
        var first = await AddSongAsync($"Abertura {role}");
        var second = await AddSongAsync($"Fecho {role}");
        var date = day.ToString("yyyy-MM-dd");
        var (client, _) = await SignInAsync(role);
        await WithTokenAsync(client);
        var (member, _) = await SignInAsync();

        var found = await client.GetFromJsonAsync<JsonElement>($"/api/events/{id}/repertoire/songs?q={Uri.EscapeDataString("abertura " + role)}");
        found.EnumerateArray().Select(s => s.GetProperty("id").GetInt32()).Should().Contain(first);

        (await client.PostAsJsonAsync($"/api/events/{id}/repertoire", new { songId = first, date })).StatusCode.Should().Be(HttpStatusCode.OK);
        var added = await client.PostAsJsonAsync($"/api/events/{id}/repertoire", new { songId = second, date });
        var items = (await added.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("days")[0].GetProperty("items");
        items.EnumerateArray().Select(i => i.GetProperty("title").GetString()).Should().Equal($"Abertura {role}", $"Fecho {role}");

        var duplicate = await client.PostAsJsonAsync($"/api/events/{id}/repertoire", new { songId = first, date });
        duplicate.StatusCode.Should().Be(HttpStatusCode.BadRequest, "a song goes once per event");
        (await duplicate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("songId", out _).Should().BeTrue();

        var ids = items.EnumerateArray().Select(i => i.GetProperty("id").GetInt32()).ToArray();
        (await client.PostAsJsonAsync($"/api/events/{id}/repertoire/reorder", new { date, itemIds = new[] { ids[1], ids[0] } }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await member.GetFromJsonAsync<JsonElement>($"/api/events/{id}")).GetProperty("member").GetProperty("repertoire")[0].GetProperty("songs")
            .EnumerateArray().Select(s => s.GetString()).Should().Equal($"Fecho {role}", $"Abertura {role}");

        (await client.DeleteAsync($"/api/events/{id}/repertoire/{ids[1]}")).StatusCode.Should().Be(HttpStatusCode.OK);
        var cleared = await client.DeleteAsync($"/api/events/{id}/repertoire/days/{date}");
        (await cleared.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("days")[0].GetProperty("items").GetArrayLength().Should().Be(0);
        (await member.GetFromJsonAsync<JsonElement>($"/api/events/{id}")).GetProperty("member").GetProperty("repertoire").GetArrayLength()
            .Should().Be(0, "the last song gone, members see no repertoire");
    }

    // ---------- other members' answers (Admin/Owner, 012E) ----------

    [Fact]
    public async Task ParticipantManagement_IsRefused_ForVisitorsMembersAndMods_AndWithoutTheAntiforgeryHeader()
    {
        var id = await AddEventAsync("Serenata com inscrições protegidas", DateTime.Today.AddDays(330));
        var (someone, someoneUser) = await SignInAsync();
        await AddEnrollmentAsync(id, someoneUser.Id, willAttend: true);
        var answer = (await EnrollmentAsync(id, someoneUser.Id))!.Id;
        var (target, targetUser) = await SignInAsync();
        var visitor = Anonymous();
        await WithTokenAsync(visitor);
        var (member, _) = await SignInAsync();
        await WithTokenAsync(member);
        var (mod, _) = await SignInAsync("Mod");
        await WithTokenAsync(mod);
        var (adminWithoutToken, _) = await SignInAsync("Admin");

        foreach (var (client, expected) in new[] { (visitor, HttpStatusCode.Unauthorized), (member, HttpStatusCode.Forbidden), (mod, HttpStatusCode.Forbidden) })
        {
            (await client.GetAsync($"/api/events/{id}/enrollments")).StatusCode.Should().Be(expected);
            (await client.GetAsync($"/api/events/{id}/enrollments/members?q=events")).StatusCode.Should().Be(expected);
            (await client.PostAsJsonAsync($"/api/events/{id}/enrollments", new { userId = targetUser.Id })).StatusCode.Should().Be(expected);
            (await client.DeleteAsync($"/api/events/{id}/enrollments/{answer}")).StatusCode.Should().Be(expected);
        }

        (await adminWithoutToken.PostAsJsonAsync($"/api/events/{id}/enrollments", new { userId = targetUser.Id })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await adminWithoutToken.DeleteAsync($"/api/events/{id}/enrollments/{answer}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await EnrollmentAsync(id, targetUser.Id)).Should().BeNull();
        (await EnrollmentAsync(id, someoneUser.Id)).Should().NotBeNull();
        var page = await member.GetAsync($"/api/events/{id}");
        (await page.Content.ReadAsStringAsync()).Should().NotContain(someoneUser.Id, "members see who goes, never user ids");
        (await Anonymous().GetFromJsonAsync<JsonElement>($"/api/events/{id}")).GetProperty("member").ValueKind
            .Should().Be(JsonValueKind.Null, "visitors never see who goes");
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Owner")]
    public async Task AdminOrOwner_AddAndRemoveOthersAnswers_AndTheMembersOwnAnswerStillWorks(string role)
    {
        var id = await AddEventAsync($"Serenata gerida ({role})", DateTime.Today.AddDays(role == "Admin" ? 331 : 332));
        var (client, _) = await SignInAsync(role);
        await WithTokenAsync(client);
        var (member, memberUser) = await SignInAsync();
        await WithTokenAsync(member);
        var (_, addedUser) = await SignInAsync();
        _factory.Push.Invocations.Clear();

        var found = await client.GetFromJsonAsync<JsonElement>($"/api/events/{id}/enrollments/members?q={Uri.EscapeDataString(addedUser.Nickname!)}");
        var option = found.EnumerateArray().Single();
        option.GetProperty("id").GetString().Should().Be(addedUser.Id);
        option.GetRawText().Should().NotContain("@", "no email in the picker");

        var added = await client.PostAsJsonAsync($"/api/events/{id}/enrollments", new { userId = addedUser.Id });
        added.StatusCode.Should().Be(HttpStatusCode.OK);
        (await EnrollmentAsync(id, addedUser.Id))!.WillAttend.Should().BeTrue();
        _factory.Push.Invocations.Should().BeEmpty("adding someone by hand notifies nobody, as before");

        (await client.PostAsJsonAsync($"/api/events/{id}/enrollments", new { userId = addedUser.Id })).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "one answer per member");

        // The member still answers for themselves in the usual modal.
        (await member.PutAsJsonAsync($"/api/events/{id}/enrollment", Answer(false))).StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await client.GetFromJsonAsync<JsonElement>($"/api/events/{id}/enrollments");
        list.GetProperty("notGoing").GetArrayLength().Should().Be(1);
        list.GetProperty("going").GetArrayLength().Should().Be(1);

        var mine = list.GetProperty("notGoing")[0].GetProperty("id").GetInt32();
        (await client.DeleteAsync($"/api/events/{id}/enrollments/{mine}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await EnrollmentAsync(id, memberUser.Id)).Should().BeNull();
        (await member.GetFromJsonAsync<JsonElement>($"/api/events/{id}/enrollment")).GetProperty("status").ValueKind
            .Should().Be(JsonValueKind.Null, "the member sees no answer of theirs any more");
    }

    // ---------- statistics (members, 012F) ----------

    [Fact]
    public async Task Stats_AreForMembersOnly_AndCountTheOldWay()
    {
        var (member, user) = await SignInAsync();
        var today = DateTime.Today;
        var went = await AddEventAsync("Estatística passada", today.AddDays(-3000));
        var cancelled = await AddEventAsync("Estatística cancelada", today.AddDays(-2999));
        var refused = await AddEventAsync("Estatística recusada", today.AddDays(-2998));
        var next = await AddEventAsync("Estatística futura", today.AddDays(3000));
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Events.SingleAsync(e => e.Id == cancelled)).Cancel("motivo");
            await db.SaveChangesAsync();
        }

        await AddEnrollmentAsync(went, user.Id, willAttend: true);
        await AddEnrollmentAsync(refused, user.Id, willAttend: false);
        await AddEnrollmentAsync(next, user.Id, willAttend: true);
        string Day(int offset) => today.AddDays(offset).ToString("yyyy-MM-dd");

        (await Anonymous().GetAsync("/api/events/stats")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var response = await member.GetAsync($"/api/events/stats?from={Day(-3001)}&to={Day(-2997)}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain(user.Id).And.NotContainAny(new[] { "userId", "email", "notes" });
        var past = JsonDocument.Parse(raw).RootElement;
        past.GetProperty("pastEvents").GetInt32().Should().Be(2, "past, not cancelled events of the range, answered or not");
        var row = past.GetProperty("members").EnumerateArray().Single();
        row.GetProperty("went").GetInt32().Should().Be(1, "only \"vou\" answers count");
        row.GetProperty("going").GetInt32().Should().Be(0);

        var future = await member.GetFromJsonAsync<JsonElement>($"/api/events/stats?from={Day(2999)}&to={Day(3001)}");
        future.GetProperty("pastEvents").GetInt32().Should().Be(0);
        var ahead = future.GetProperty("members").EnumerateArray().Single();
        ahead.GetProperty("went").GetInt32().Should().Be(0);
        ahead.GetProperty("going").GetInt32().Should().Be(1);

        (await member.GetAsync($"/api/events/stats?from={Day(1)}&to={Day(0)}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var season = await member.GetFromJsonAsync<JsonElement>("/api/events/stats");
        season.GetProperty("from").GetString().Should().EndWith("-09-01", "the default is the current season");
        season.GetProperty("to").GetString().Should().EndWith("-08-31");
    }

    private async Task<int> AddSongAsync(string title)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var album = await db.Albums.FirstOrDefaultAsync(a => a.Title == "Cancioneiro de teste");
        if (album is null)
        {
            album = Album.Create("Cancioneiro de teste", 2020);
            db.Albums.Add(album);
            await db.SaveChangesAsync();
        }

        var song = Song.Create(title, album.Id);
        db.Songs.Add(song);
        await db.SaveChangesAsync();
        return song.Id;
    }

    private async Task<int> AddRepertoireItemAsync(int eventId, int songId, DateTime day, int order)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var r = EventRepertoire.Create(eventId, songId, order, day);
        db.EventRepertoires.Add(r);
        await db.SaveChangesAsync();
        return r.Id;
    }
}
