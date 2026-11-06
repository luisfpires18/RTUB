using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// "As minhas ações pendentes" (task 071): who gets which panel, what counts as pending in Atuações / Ensaios
/// (unanswered, not cancelled; every upcoming one; past ones of the current fiscal year), independent paging with the
/// last page clamped, and the dismissal rules (optional only, server-decided, per member and period, idempotent).
/// Each test gets its own database and a frozen clock: Friday 6 Nov 2026, fiscal year 2026-2027.
/// </summary>
public class PendingActionsServiceTests
{
    private static readonly DateTime Today = new(2026, 11, 6);
    private readonly TimeProvider _clock = new FixedClock(new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero));
    private readonly DatabaseFixture _db = new();

    private PendingActionsService Service(params IPendingActionSource[] extra) =>
        new(new Contexts(_db), new IPendingActionSource[] { new EventResponsePendingActionSource(), new RehearsalResponsePendingActionSource() }.Concat(extra), _clock);

    // ---------- access ----------

    [Fact]
    public async Task ActiveLeitao_GetsBothParticipationPanels_AndTheHeader()
    {
        var me = AddUser([MemberCategory.Leitao], retired: false);

        var page = (await Service().GetPageAsync(SignedIn(me))).Value!;

        page.Status.Should().Be("active");
        page.Today.Should().Be("2026-11-06");
        page.FiscalYear.Should().Be("2026-2027");
        page.Panels.Select(p => p.Key).Should().Equal("events", "rehearsals");
        page.Panels.Should().OnlyContain(p => p.Category == "participation" && p.PageSize == PendingActionsService.PageSize);
    }

    [Fact]
    public async Task Reformado_AndAccountsWithoutCategory_GetThePage_WithoutParticipationPanels()
    {
        AddEvent("Serenata", Today.AddDays(3));
        var retired = AddUser([MemberCategory.Tuno], retired: true);
        var none = AddUser([], retired: false);

        var r = (await Service().GetPageAsync(SignedIn(retired))).Value!;
        var n = (await Service().GetPageAsync(SignedIn(none))).Value!;

        r.Status.Should().Be("retired");
        r.Panels.Should().BeEmpty("status alone grants nothing, and Reformados answer in the agenda");
        n.Status.Should().Be("none");
        n.Panels.Should().BeEmpty();
        (await Service().GetPanelAsync(SignedIn(retired), "events", null, 1)).Status.Should().Be(EventResultStatus.NotFound);
    }

    [Fact]
    public async Task Visitors_ExpelledAndUnknownAccounts_AreRefused()
    {
        var expelled = AddUser([MemberCategory.Tuno], retired: false, expelled: true);

        (await Service().GetPageAsync(new ClaimsPrincipal(new ClaimsIdentity()))).Status.Should().Be(EventResultStatus.SignInRequired);
        (await Service().GetPageAsync(SignedIn(expelled))).Status.Should().Be(EventResultStatus.SignInRequired);
        (await Service().GetPageAsync(SignedIn("ghost"))).Status.Should().Be(EventResultStatus.SignInRequired);
        (await Service().CountPendingAsync("ghost")).Should().Be(0);
    }

    // ---------- what is pending ----------

    [Fact]
    public async Task Events_UpcomingIsEveryUnansweredOne_PastIsThisFiscalYearOnly()
    {
        var me = AddUser([MemberCategory.Tuno], retired: false);
        var soon = AddEvent("Amanhã", Today.AddDays(1));
        var far = AddEvent("Daqui a meio ano", Today.AddDays(200));
        var going = AddEvent("Já disse que vou", Today.AddDays(2));
        var notGoing = AddEvent("Já disse que não", Today.AddDays(4));
        AddEvent("Cancelada", Today.AddDays(5), cancelled: true);
        var today = AddEvent("Hoje", Today.AddHours(21));
        var september = AddEvent("Setembro", new DateTime(2026, 9, 1, 21, 0, 0));
        AddEvent("Ano passado", new DateTime(2026, 8, 31, 21, 0, 0));
        AddEnrollment(going, me, true);
        AddEnrollment(notGoing, me, false);

        var upcoming = (await Service().GetPanelAsync(SignedIn(me), "events", "upcoming", 1)).Value!;
        var past = (await Service().GetPanelAsync(SignedIn(me), "events", "past", 1)).Value!;

        upcoming.Total.Should().Be(3);
        upcoming.Rows.Select(r => r.SourceKey).Should().Equal(Key(today), Key(soon), Key(far));
        upcoming.Rows[0].Actions.Single().Href.Should().Be($"/events/{today}?respond=1");
        upcoming.Rows.Should().OnlyContain(r => !r.Required && !r.Dismissible);
        past.Total.Should().Be(1, "the fiscal year starts on 1 September");
        past.Rows.Single().SourceKey.Should().Be(Key(september));
        past.Filters.Select(f => (f.Key, f.Count)).Should().Equal(("upcoming", 3), ("past", 1));
    }

    [Fact]
    public async Task Rehearsals_SameRules_AndAnotherMembersAnswerDoesNotResolveMine()
    {
        var me = AddUser([MemberCategory.Caloiro], retired: false);
        var other = AddUser([MemberCategory.Caloiro], retired: false);
        var next = AddRehearsal(Today.AddDays(3));
        var answered = AddRehearsal(Today.AddDays(5));
        AddRehearsal(Today.AddDays(6), cancelled: true);
        var lastWeek = AddRehearsal(Today.AddDays(-7));
        AddRehearsal(new DateTime(2026, 6, 1));
        AddAttendance(answered, me);
        AddAttendance(next, other);

        var upcoming = (await Service().GetPanelAsync(SignedIn(me), "rehearsals", "upcoming", 1)).Value!;
        var past = (await Service().GetPanelAsync(SignedIn(me), "rehearsals", "past", 1)).Value!;

        upcoming.Rows.Select(r => r.SourceKey).Should().Equal(Key(next));
        upcoming.Rows.Single().Actions.Single().Href.Should().Be($"/rehearsals/{next}");
        past.Rows.Select(r => r.SourceKey).Should().Equal(Key(lastWeek));
        (await Service().CountPendingAsync(me)).Should().Be(2);
        (await Service().GetPanelAsync(SignedIn(other), "rehearsals", "upcoming", 1)).Value!.Rows.Select(r => r.SourceKey)
            .Should().Equal([Key(answered)], "each member's own answers only");
    }

    // ---------- paging ----------

    [Fact]
    public async Task Panels_PageThreeRowsAtATime_IndependentlyAndClamped()
    {
        var me = AddUser([MemberCategory.Tuno], retired: false);
        var ids = Enumerable.Range(1, 7).Select(i => AddEvent($"E{i}", Today.AddDays(i))).ToList();
        AddRehearsal(Today.AddDays(1));
        var service = Service();

        var page1 = (await service.GetPanelAsync(SignedIn(me), "events", "upcoming", 1)).Value!;
        var page3 = (await service.GetPanelAsync(SignedIn(me), "events", "upcoming", 3)).Value!;
        var beyond = (await service.GetPanelAsync(SignedIn(me), "events", "upcoming", 99)).Value!;
        var rehearsals = (await service.GetPanelAsync(SignedIn(me), "rehearsals", "upcoming", 1)).Value!;

        page1.Rows.Select(r => r.SourceKey).Should().Equal(ids.Take(3).Select(Key));
        page1.Total.Should().Be(7);
        page3.Rows.Select(r => r.SourceKey).Should().Equal(Key(ids[6]));
        beyond.Page.Should().Be(3, "a page past the end answers the last one");
        rehearsals.Total.Should().Be(1, "the other panel pages on its own");

        // The only row of the last page is answered elsewhere: asking for page 3 again answers page 2, with the new count.
        AddEnrollment(ids[6], me, true);
        var after = (await service.GetPanelAsync(SignedIn(me), "events", "upcoming", 3)).Value!;
        after.Page.Should().Be(2);
        after.Total.Should().Be(6);
        after.Rows.Should().HaveCount(3);
    }

    [Fact]
    public async Task UnknownFilter_IsTheDefault_AndUnknownPanel_Is404()
    {
        var me = AddUser([MemberCategory.Tuno], retired: false);
        AddEvent("Próxima", Today.AddDays(1));

        var panel = (await Service().GetPanelAsync(SignedIn(me), "events", "everything", 0)).Value!;

        panel.Filter.Should().Be("upcoming");
        panel.Page.Should().Be(1);
        (await Service().GetPanelAsync(SignedIn(me), "inbox", null, 1)).Status.Should().Be(EventResultStatus.NotFound);
    }

    // ---------- dismissal ----------

    [Fact]
    public async Task Dismissing_AnAnswerOrARequiredAction_IsRefused_AndUnknownOnesAre404()
    {
        var me = AddUser([MemberCategory.Tuno], retired: false);
        var e = AddEvent("Próxima", Today.AddDays(1));
        var service = Service(new FakeSource("required", required: true));

        (await service.DismissAsync(SignedIn(me), new(EventResponsePendingActionSource.Kind, Key(e)))).Status.Should().Be(EventResultStatus.Forbidden);
        (await service.DismissAsync(SignedIn(me), new("fake.required", "1"))).Status.Should().Be(EventResultStatus.Forbidden);
        (await service.DismissAsync(SignedIn(me), new(EventResponsePendingActionSource.Kind, "999"))).Status.Should().Be(EventResultStatus.NotFound);
        (await service.DismissAsync(SignedIn(me), new(null, null))).Status.Should().Be(EventResultStatus.NotFound);
        (await service.DismissAsync(SignedIn(me), new(new string('x', 65), "1"))).Status.Should().Be(EventResultStatus.NotFound);
        Dismissals().Should().BeEmpty();
    }

    [Fact]
    public async Task DismissingAnOptionalAction_HidesItForThatMemberAndPeriodOnly_AndIsIdempotent()
    {
        var me = AddUser([MemberCategory.Tuno], retired: false);
        var other = AddUser([MemberCategory.Tuno], retired: false);
        var optional = new FakeSource("optional", required: false);
        var service = Service(optional);

        (await service.DismissAsync(SignedIn(me), new("fake.optional", "2"))).Status.Should().Be(EventResultStatus.Ok);
        (await service.DismissAsync(SignedIn(me), new("fake.optional", "2"))).Status.Should().Be(EventResultStatus.NotFound,
            "a retry finds nothing pending (the page treats it as done) and stores nothing");

        Dismissals().Should().ContainSingle(d => d.UserId == me && d.SourceKey == "2" && d.Period == "2026-W45");
        var mine = (await service.GetPanelAsync(SignedIn(me), "optional", null, 1)).Value!;
        mine.Rows.Select(r => r.SourceKey).Should().Equal("1", "3");
        (await service.GetPanelAsync(SignedIn(other), "optional", null, 1)).Value!.Total.Should().Be(3, "a dismissal is personal");

        // A new period is a new key: the prompt is back by itself.
        optional.Period = "2026-W46";
        (await service.GetPanelAsync(SignedIn(me), "optional", null, 1)).Value!.Total.Should().Be(3);
    }

    // ---------- live permissions ----------

    [Fact]
    public async Task APanelGatedByRole_FollowsTheDatabase_NotTheCookie()
    {
        var me = AddUser([MemberCategory.Tuno], retired: false);
        var gated = new FakeSource("mods", required: true) { Role = "Mod" };
        var cookie = SignedIn(me); // no role claims at all

        (await Service(gated).GetPageAsync(cookie)).Value!.Panels.Select(p => p.Key).Should().NotContain("mods");
        AddRole(me, "Mod");
        (await Service(gated).GetPageAsync(cookie)).Value!.Panels.Select(p => p.Key).Should().Contain("mods");
        SetRetired(me, true);
        (await Service(gated).GetPageAsync(cookie)).Value!.Panels.Select(p => p.Key).Should().Equal("mods");
    }

    // ---------- helpers ----------

    private static string Key(int id) => id.ToString();

    private static ClaimsPrincipal SignedIn(string userId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "test"));

    private string AddUser(List<MemberCategory> categories, bool retired, bool expelled = false)
    {
        using var db = _db.CreateContext();
        var id = Guid.NewGuid().ToString();
        db.Users.Add(new ApplicationUser
        {
            Id = id, UserName = id, Email = $"{id}@test.pt", FirstName = "Ana", LastName = "Silva", Nickname = "Ana",
            Categories = categories, IsRetired = retired, IsExpelled = expelled,
        });
        db.SaveChanges();
        return id;
    }

    private void AddRole(string userId, string role)
    {
        using var db = _db.CreateContext();
        var r = new IdentityRole(role) { Id = Guid.NewGuid().ToString() };
        db.Roles.Add(r);
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = userId, RoleId = r.Id });
        db.SaveChanges();
    }

    private void SetRetired(string userId, bool retired)
    {
        using var db = _db.CreateContext();
        db.Users.Single(u => u.Id == userId).IsRetired = retired;
        db.SaveChanges();
    }

    private int AddEvent(string name, DateTime date, bool cancelled = false)
    {
        using var db = _db.CreateContext();
        var e = Event.Create(name, date, "Braga", Enum.GetValues<EventType>()[0]);
        e.IsCancelled = cancelled;
        db.Events.Add(e);
        db.SaveChanges();
        return e.Id;
    }

    private void AddEnrollment(int eventId, string userId, bool willAttend)
    {
        using var db = _db.CreateContext();
        var x = Enrollment.Create(userId, eventId);
        x.WillAttend = willAttend;
        db.Enrollments.Add(x);
        db.SaveChanges();
    }

    private int AddRehearsal(DateTime day, bool cancelled = false)
    {
        using var db = _db.CreateContext();
        var r = Rehearsal.Create(day, "Centro Académico");
        if (cancelled)
        {
            r.Cancel("Feriado");
        }

        db.Rehearsals.Add(r);
        db.SaveChanges();
        return r.Id;
    }

    private void AddAttendance(int rehearsalId, string userId)
    {
        using var db = _db.CreateContext();
        db.RehearsalAttendances.Add(RehearsalAttendance.Create(rehearsalId, userId));
        db.SaveChanges();
    }

    private List<PendingActionDismissal> Dismissals()
    {
        using var db = _db.CreateContext();
        return db.PendingActionDismissals.AsNoTracking().ToList();
    }

    /// <summary>
    /// A stand-in for the optional and required panels of later phases (news prompt, treasurer report...): three actions
    /// "1".."3", the member's dismissals of the current <see cref="Period"/> filtered out in the query, like a real source.
    /// </summary>
    private sealed class FakeSource(string panel, bool required) : IPendingActionSource
    {
        public string Period { get; set; } = "2026-W45";
        public string? Role { get; init; }
        public string Panel => panel;
        public string Title => panel;
        public string Category => "responsibilities";
        public IReadOnlyList<(string Key, string Label)> Filters => [];
        private string Kind => $"fake.{panel}";

        public bool AppliesTo(PendingActionMember member) => Role is null || member.Roles.Contains(Role);

        public async Task<int> CountAsync(ApplicationDbContext db, PendingActionMember member, string? filter) => (await Open(db, member)).Count;

        public async Task<IReadOnlyList<PendingActionRowDto>> PageAsync(ApplicationDbContext db, PendingActionMember member, string? filter, int skip, int take) =>
            (await Open(db, member)).Skip(skip).Take(take).Select(Row).ToList();

        public async Task<(PendingActionRowDto Row, string Period)?> FindAsync(ApplicationDbContext db, PendingActionMember member, string kind, string sourceKey) =>
            kind == Kind && (await Open(db, member)).Contains(sourceKey) ? (Row(sourceKey), Period) : null;

        private async Task<List<string>> Open(ApplicationDbContext db, PendingActionMember member)
        {
            var dismissed = await db.PendingActionDismissals
                .Where(d => d.UserId == member.UserId && d.ActionKind == Kind && d.Period == Period)
                .Select(d => d.SourceKey).ToListAsync();
            return new[] { "1", "2", "3" }.Except(dismissed).ToList();
        }

        private PendingActionRowDto Row(string key) =>
            new(Kind, key, $"Ação {key}", null, null, null, required, !required, [new PendingActionButtonDto("Abrir", "/", true)]);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Contexts(DatabaseFixture db) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => db.CreateContext();
    }
}
