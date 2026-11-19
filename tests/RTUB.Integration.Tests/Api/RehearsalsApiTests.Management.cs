// RehearsalsApiTests: Management tests.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Moq;
using RTUB.Application.DTOs;

namespace RTUB.Integration.Tests.Api;

public partial class RehearsalsApiTests
{
    // ---------- Admin/Owner ----------

    [Theory]
    [InlineData(null)]
    [InlineData("Mod")]
    public async Task MembersAndMods_CannotManageRehearsals(string? role)
    {
        var id = await AddRehearsalAsync(NextDay(past: true));
        var (client, _) = await SignInAsync(role);
        var (_, other) = await SignInAsync();
        var row = await AddAttendanceAsync(id, other.Id, willAttend: true, attended: false);
        await AreaHttp.WithTokenAsync(client);

        // Create and edit are Mod and above (see AMod_CreatesAndEditsRehearsals_ButCannotDoTheRest).
        if (role is null)
        {
            (await client.PostAsJsonAsync("/api/rehearsals", Input(DateText(NextDay(past: false))))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await client.PutAsJsonAsync($"/api/rehearsals/{id}", Input(null))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        (await client.PostAsJsonAsync($"/api/rehearsals/{id}/cancel", new { reason = "x" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsync($"/api/rehearsals/{id}/attendances/{row}/approve", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.DeleteAsync($"/api/rehearsals/{id}/attendances/{row}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync($"/api/rehearsals/{id}/notice")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.DeleteAsync($"/api/rehearsals/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await RehearsalAsync(id)).Should().NotBeNull();
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Owner")]
    public async Task AdminOrOwner_CreatesEditsCancelsReactivatesAndDeletes(string role)
    {
        var (admin, _) = await SignInAsync(role);
        await AreaHttp.WithTokenAsync(admin);
        var date = DateText(NextDay(past: false));

        var created = await admin.PostAsJsonAsync("/api/rehearsals", new { date, location = "Centro Académico", theme = "Fados", description = "Repertório novo", notes = "Trazer pautas" });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        var saved = await RehearsalAsync(id);
        saved!.Description.Should().Be("Repertório novo", "the description typed when creating is kept now");
        saved.Notes.Should().Be("Trazer pautas");
        saved.StartTime.Should().Be(new TimeSpan(21, 30, 0));

        (await admin.PostAsJsonAsync("/api/rehearsals", new { date, location = "Outro", theme = "", description = "", notes = "" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "one rehearsal per date");
        (await admin.PostAsJsonAsync("/api/rehearsals", new { date = DateText(NextDay(past: false)), location = " ", theme = "", description = "", notes = "" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await admin.PutAsJsonAsync($"/api/rehearsals/{id}", new { date = (string?)null, location = "Nómada", theme = "", description = "", notes = "" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await RehearsalAsync(id))!.Location.Should().Be("Nómada");

        var (_, member) = await SignInAsync();
        await AddAttendanceAsync(id, member.Id, willAttend: true, attended: false);
        (await admin.PostAsJsonAsync($"/api/rehearsals/{id}/cancel", new { reason = " " })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync($"/api/rehearsals/{id}/cancel", new { reason = "Feriado" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await RehearsalAsync(id))!.IsCanceled.Should().BeTrue();
        (await AttendanceAsync(id, member.Id)).Should().BeNull("cancelling removes the presenças, as before");

        (await admin.PostAsync($"/api/rehearsals/{id}/reactivate", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await RehearsalAsync(id))!.IsCanceled.Should().BeFalse();

        (await admin.DeleteAsync($"/api/rehearsals/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await RehearsalAsync(id)).Should().BeNull();
    }

    [Fact]
    public async Task AMod_CreatesAndEditsRehearsals_ButCannotDoTheRest()
    {
        var (mod, _) = await SignInAsync("Mod");
        await AreaHttp.WithTokenAsync(mod);

        var agenda = await mod.GetFromJsonAsync<JsonElement>("/api/rehearsals");
        (agenda.GetProperty("canEdit").GetBoolean(), agenda.GetProperty("canManage").GetBoolean()).Should().Be((true, false));

        var created = await mod.PostAsJsonAsync("/api/rehearsals",
            new { date = DateText(NextDay(past: false)), location = "Centro Académico", theme = "", description = "", notes = "" });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        (await mod.PutAsJsonAsync($"/api/rehearsals/{id}", new { date = (string?)null, location = "Nómada", theme = "", description = "", notes = "" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await RehearsalAsync(id))!.Location.Should().Be("Nómada");

        var monday = DateTime.Today.AddDays(7000 + ((8 - (int)DateTime.Today.AddDays(7000).DayOfWeek) % 7));
        var range = await mod.PostAsJsonAsync("/api/rehearsals/range",
            new { from = DateText(monday), to = DateText(monday.AddDays(6)), location = "Centro Académico", theme = "", description = "" });
        range.StatusCode.Should().Be(HttpStatusCode.OK);
        (await range.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("created").GetInt32().Should().Be(2);

        (await mod.PostAsJsonAsync($"/api/rehearsals/{id}/cancel", new { reason = "x" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await mod.GetAsync($"/api/rehearsals/{id}/notice")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await mod.DeleteAsync($"/api/rehearsals/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await RehearsalAsync(id)).Should().NotBeNull();
    }

    [Fact]
    public async Task ARange_CreatesTuesdaysAndThursdays_SkipsTakenDates_AndIsLimited()
    {
        var (admin, _) = await SignInAsync("Admin");
        await AreaHttp.WithTokenAsync(admin);
        var monday = DateTime.Today.AddDays(5000 + ((8 - (int)DateTime.Today.AddDays(5000).DayOfWeek) % 7));
        await AddRehearsalAtAsync(monday.AddDays(1)); // that Tuesday is taken

        var response = await admin.PostAsJsonAsync("/api/rehearsals/range",
            new { from = DateText(monday), to = DateText(monday.AddDays(13)), location = "Centro Académico", theme = "", description = "" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("created").GetInt32().Should().Be(3, "two weeks hold 2 Tuesdays and 2 Thursdays, one taken");
        result.GetProperty("skipped").GetInt32().Should().Be(1);

        (await admin.PostAsJsonAsync("/api/rehearsals/range", new { from = DateText(monday), to = DateText(monday.AddDays(91)), location = "X", theme = "", description = "" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync("/api/rehearsals/range", new { from = DateText(DateTime.Today.AddDays(-1)), to = DateText(DateTime.Today), location = "X", theme = "", description = "" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Owner")]
    public async Task OnceApprovable_AdminOrOwner_ConfirmsRemovesAndAddsPresencas(string role)
    {
        var past = await AddRehearsalAsync(NextDay(past: true));
        var future = await AddRehearsalAsync(NextDay(past: false));
        var (admin, _) = await SignInAsync(role);
        var (_, pending) = await SignInAsync();
        var (_, extra) = await SignInAsync();
        var (_, other) = await SignInAsync();
        var row = await AddAttendanceAsync(past, pending.Id, willAttend: true, attended: false);
        var futureRow = await AddAttendanceAsync(future, pending.Id, willAttend: true, attended: false);
        var otherRow = await AddAttendanceAsync(past, other.Id, willAttend: false, attended: false);
        await AreaHttp.WithTokenAsync(admin);

        (await admin.PostAsync($"/api/rehearsals/{future}/attendances/{futureRow}/approve", null)).StatusCode.Should().Be(HttpStatusCode.Conflict,
            "a future rehearsal cannot be approved yet");
        (await admin.PostAsync($"/api/rehearsals/{future}/attendances/{row}/approve", null)).StatusCode.Should().Be(HttpStatusCode.NotFound,
            "a presença only counts under its own rehearsal");
        (await admin.PostAsync($"/api/rehearsals/{past}/attendances/{row}/approve", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AttendanceAsync(past, pending.Id))!.Attended.Should().BeTrue();
        _factory.Push.Verify(p => p.SendToUserAsync(pending.Id, It.IsAny<SendPushNotificationDto>()), Times.Once, "the member hears it was confirmed, as before");

        var found = await admin.GetFromJsonAsync<JsonElement>($"/api/rehearsals/{past}/attendances/members?q={extra.UserName}");
        found.EnumerateArray().Should().ContainSingle(m => m.GetProperty("id").GetString() == extra.Id);
        (await admin.PostAsJsonAsync($"/api/rehearsals/{past}/attendances", new { userId = extra.Id })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var added = await AttendanceAsync(past, extra.Id);
        added!.Attended.Should().BeTrue("someone added afterwards was there");
        (await admin.PostAsJsonAsync($"/api/rehearsals/{past}/attendances", new { userId = extra.Id })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync($"/api/rehearsals/{future}/attendances", new { userId = extra.Id })).StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await admin.DeleteAsync($"/api/rehearsals/{past}/attendances/{otherRow}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AttendanceAsync(past, other.Id)).Should().BeNull();
        (await admin.DeleteAsync($"/api/rehearsals/{future}/attendances/{futureRow}")).StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "before it can be approved, only the member removes their presença");
    }
}
