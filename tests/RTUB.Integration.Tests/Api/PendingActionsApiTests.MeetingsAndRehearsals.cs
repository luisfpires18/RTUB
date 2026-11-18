// PendingActionsApiTests tests: meeting atas, attendance and repertoire panels.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Integration.Tests.Api;
public partial class PendingActionsApiTests
{
    // ---------- Reuniões, Presenças por aprovar, Repertório (075) ----------

    [Fact]
    public async Task APresident_IsAskedForEachPastMeetingWithoutAta_UntilADraftIsSaved()
    {
        Assert.SkipWhen(PortugalTime.Today(TimeProvider.System) < new DateTime(CurrentFiscalYearStart(), 9, 4),
            "the first days of a fiscal year have no past meeting in it yet");
        var (mesa, _) = await SignInAsync([MemberCategory.Tuno], retired: false, positions: [Position.PresidenteMesaAssembleia]);
        var (other, _) = await SignInAsync([MemberCategory.Tuno], retired: false);
        await AreaHttp.WithTokenAsync(mesa);
        var ag = await AddMeetingAsync(MeetingType.AssembleiaGeralOrdinaria, -3);
        var cv = await AddMeetingAsync(MeetingType.ConselhoVeteranos, -3);
        var upcoming = await AddMeetingAsync(MeetingType.AssembleiaGeralExtraordinaria, 5);
        var lastYear = await AddMeetingAsync(MeetingType.AssembleiaGeralOrdinaria, -400);

        var keys = await AllKeysAsync(mesa, "meetings", null);
        keys.Should().Contain($"ata:{ag}").And.NotContain([$"ata:{cv}", $"ata:{upcoming}", $"ata:{lastYear}"],
            "another body's, not yet held, or an earlier mandate's");
        keys.Should().Contain("register:AssembleiaGeralOrdinaria").And.NotContain("register:ConselhoVeteranos");
        var row = await RowAsync(mesa, "meetings", null, $"ata:{ag}");
        row.GetProperty("required").GetBoolean().Should().BeTrue();
        row.GetProperty("actions")[0].GetProperty("href").GetString().Should().Be($"/meetings?meeting={ag}&ata=1");
        (await other.GetAsync("/api/pending-actions/panels/meetings")).StatusCode.Should().Be(HttpStatusCode.NotFound, "no presidency");
        (await mesa.PostAsJsonAsync("/api/pending-actions/dismissals", new { kind = "meetings", sourceKey = $"ata:{ag}" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "a missing ata is required");

        // "Agora não" on the optional prompt: gone for this month.
        (await mesa.PostAsJsonAsync("/api/pending-actions/dismissals", new { kind = "meetings", sourceKey = "register:AssembleiaGeralOrdinaria" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AllKeysAsync(mesa, "meetings", null)).Should().NotContain("register:AssembleiaGeralOrdinaria");

        // A saved draft (the module's own editor) resolves it; publishing is separate.
        (await mesa.PutAsJsonAsync($"/api/meetings/{ag}/ata", MeetingsKit.NewAta(assembly: true))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await AllKeysAsync(mesa, "meetings", null)).Should().NotContain($"ata:{ag}");
    }

    [Fact]
    public async Task TheEnsaiador_ApprovesAndRefusesPendingPresencas_WithoutAdmin()
    {
        var (ensaiador, _) = await SignInAsync([MemberCategory.Tuno], retired: false, positions: [Position.Ensaiador]);
        var (member, me) = await SignInAsync([MemberCategory.Tuno], retired: false);
        var (otherMember, other) = await SignInAsync([MemberCategory.Tuno], retired: false);
        await AreaHttp.WithTokenAsync(ensaiador);
        await AreaHttp.WithTokenAsync(member);
        var past = await AddRehearsalAsync(-10 - Interlocked.Increment(ref _approvalDay));
        var pastToo = await AddRehearsalAsync(-10 - Interlocked.Increment(ref _approvalDay));
        var future = await AddRehearsalAsync(NextDay());
        var approveMe = await AddPendingAsync(past, me.Id);
        var refuseOther = await AddPendingAsync(pastToo, other.Id);
        var early = await AddPendingAsync(future, me.Id);

        var keys = await AllKeysAsync(ensaiador, "approvals", null);
        keys.Should().Contain([approveMe.ToString(), refuseOther.ToString()]).And.NotContain(early.ToString(), "not approvable yet");
        (await member.GetAsync("/api/pending-actions/panels/approvals")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await member.PostAsync($"/api/rehearsals/{past}/attendances/{approveMe}/approve", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "an arbitrary member cannot approve");
        (await ensaiador.PostAsync($"/api/rehearsals/{future}/attendances/{early}/approve", null)).StatusCode.Should().Be(HttpStatusCode.Conflict,
            "a future presença cannot be approved early");

        (await Command(ensaiador, "rehearsal.approval", approveMe.ToString(), "approve")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Command(ensaiador, "rehearsal.approval", refuseOther.ToString(), "refuse")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Command(ensaiador, "rehearsal.approval", refuseOther.ToString(), "approve")).StatusCode.Should().Be(HttpStatusCode.NotFound, "decided");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.RehearsalAttendances.AsNoTracking().SingleAsync(a => a.Id == approveMe)).Attended.Should().BeTrue();
            var refused = await db.RehearsalAttendances.AsNoTracking().SingleAsync(a => a.Id == refuseOther);
            refused.WillAttend.Should().BeFalse("recorded as not attended, not deleted");
            refused.Attended.Should().BeFalse();
            (await db.AuditLogs.AnyAsync(l => l.EntityType == nameof(RehearsalAttendance) && l.EntityId == refuseOther)).Should().BeTrue("audited");
        }

        // The refused member is not asked "Fui / Não fui" again for it: the presença row stays.
        (await AllKeysAsync(otherMember, "rehearsals", "past")).Should().NotContain(pastToo.ToString());
        // The module page: the Ensaiador approves there too, without Admin.
        var detail = await ensaiador.GetFromJsonAsync<JsonElement>($"/api/rehearsals/{future}");
        detail.GetProperty("attendance").GetProperty("going").EnumerateArray().Single(a => a.GetProperty("id").GetInt32() == early)
            .GetProperty("canApprove").GetBoolean().Should().BeFalse("not approvable yet");
    }

    /// <summary>Security review of PR #276: a position holder never approves or refuses their own presença.</summary>
    [Fact]
    public async Task TheEnsaiador_CannotDecideTheirOwnPresenca()
    {
        var (ensaiador, self) = await SignInAsync([MemberCategory.Tuno], retired: false, positions: [Position.Ensaiador]);
        await AreaHttp.WithTokenAsync(ensaiador);
        var past = await AddRehearsalAsync(-40 - Interlocked.Increment(ref _approvalDay));
        var own = await AddPendingAsync(past, self.Id);

        (await AllKeysAsync(ensaiador, "approvals", null)).Should().NotContain(own.ToString());
        (await Command(ensaiador, "rehearsal.approval", own.ToString(), "approve")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ensaiador.PostAsync($"/api/rehearsals/{past}/attendances/{own}/approve", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ensaiador.PostAsync($"/api/rehearsals/{past}/attendances/{own}/refuse", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var detail = await ensaiador.GetFromJsonAsync<JsonElement>($"/api/rehearsals/{past}");
        detail.GetProperty("attendance").GetProperty("going").EnumerateArray().Single(a => a.GetProperty("id").GetInt32() == own)
            .GetProperty("canApprove").GetBoolean().Should().BeFalse();
        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().RehearsalAttendances.AsNoTracking().SingleAsync(x => x.Id == own))
            .Attended.Should().BeFalse("still pending for someone else to decide");
    }

    [Fact]
    public async Task TheEnsaiador_PreparesUpcomingRepertoires_AndNaoENecessarioIsTheirOwn()
    {
        var (ensaiador, ensaiadorUser) = await SignInAsync([MemberCategory.Tuno], retired: false, positions: [Position.Ensaiador]);
        var (magister, _) = await SignInAsync([MemberCategory.Tuno], retired: false, positions: [Position.Magister]);
        var (member, _) = await SignInAsync([MemberCategory.Tuno], retired: false);
        await AreaHttp.WithTokenAsync(ensaiador);
        var upcoming = await AddEventAsync(NextDay());
        var other = await AddEventAsync(NextDay());
        var past = await AddEventAsync(-2);

        (await AllKeysAsync(ensaiador, "repertoire", null)).Should().Contain([upcoming.ToString(), other.ToString()]).And.NotContain(past.ToString());
        (await ensaiador.GetAsync($"/api/events/{upcoming}/repertoire")).StatusCode.Should().Be(HttpStatusCode.OK, "the repertoire editor, no Admin");
        (await ensaiador.GetAsync($"/api/events/{past}/repertoire")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "upcoming events only");
        (await member.GetAsync($"/api/events/{upcoming}/repertoire")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ensaiador.GetFromJsonAsync<JsonElement>($"/api/events/{upcoming}")).GetProperty("canManageRepertoire").GetBoolean().Should().BeTrue();
        (await ensaiador.GetFromJsonAsync<JsonElement>($"/api/events/{upcoming}")).GetProperty("canManage").GetBoolean().Should().BeFalse(
            "no other event management");

        (await ensaiador.PostAsJsonAsync("/api/pending-actions/dismissals", new { kind = "repertoire.event", sourceKey = other.ToString() }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AllKeysAsync(ensaiador, "repertoire", null)).Should().NotContain(other.ToString());
        (await AllKeysAsync(magister, "repertoire", null)).Should().Contain(other.ToString(), "a dismissal is personal");

        await AddRepertoireAsync(upcoming);
        (await AllKeysAsync(magister, "repertoire", null)).Should().NotContain(upcoming.ToString(), "a saved repertoire resolves it for everyone");
        await SetPositionsAsync(ensaiadorUser.Id, []);
        (await ensaiador.GetAsync("/api/pending-actions/panels/repertoire")).StatusCode.Should().Be(HttpStatusCode.NotFound, "mandate ended");
        (await ensaiador.GetAsync($"/api/events/{other}/repertoire")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<int> AddMeetingAsync(MeetingType type, int days)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var m = new Meeting
        {
            Type = type, Title = $"Reunião {Guid.NewGuid():N}"[..20], Date = DateTime.Today.AddDays(days).AddHours(21),
            Location = "Sede", Statement = "Ordem de trabalhos",
        };
        db.Meetings.Add(m);
        await db.SaveChangesAsync();
        return m.Id;
    }

    private async Task<int> AddPendingAsync(int rehearsalId, string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var a = RehearsalAttendance.Create(rehearsalId, userId);
        db.RehearsalAttendances.Add(a);
        await db.SaveChangesAsync();
        return a.Id;
    }

    private async Task AddRepertoireAsync(int eventId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var album = Album.Create($"Álbum {Guid.NewGuid():N}"[..20], 2020);
        db.Albums.Add(album);
        await db.SaveChangesAsync();
        var song = Song.Create($"Canção {Guid.NewGuid():N}"[..20], album.Id);
        db.Songs.Add(song);
        await db.SaveChangesAsync();
        db.EventRepertoires.Add(EventRepertoire.Create(eventId, song.Id, 1, DateTime.Today.AddDays(5)));
        await db.SaveChangesAsync();
    }
}
