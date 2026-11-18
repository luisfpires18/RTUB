using System.Security.Claims;
using FluentAssertions;
using Moq;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// "As minhas ações pendentes" (tasks 071 / 072): who gets which panel, what counts as pending in Atuações / Ensaios
/// (unanswered, not cancelled; every upcoming one; past ones of the current fiscal year from the admission month),
/// independent paging with the last page clamped, the dismissal rules (optional only, server-decided, per member and
/// period, idempotent) and the in-page answers (routed to the domain services, create-only, validated). The domain
/// services are fakes that write the row they would write; their own rules and the real SQLite unique indexes are
/// covered by RehearsalAttendanceAdminWorkflowTests and PendingActionsApiTests.
/// Each test gets its own database and a frozen clock: Friday 6 Nov 2026, fiscal year 2026-2027.
/// </summary>
public partial class PendingActionsServiceTests
{
    private static readonly DateTime Today = new(2026, 11, 6);
    private readonly TimeProvider _clock = new TestClock(new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero));
    private readonly DatabaseFixture _db = new();

    private readonly Mock<IEnrollmentService> _enrollments = new();
    private readonly Mock<IRehearsalAttendanceService> _attendance = new();
    private readonly Mock<IMemberStatusService> _status = new();

    public PendingActionsServiceTests()
    {
        _enrollments.Setup(s => s.CreateEnrollmentAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<InstrumentType?>(), It.IsAny<string?>(),
                It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns((string userId, int eventId, InstrumentType? instrument, string? notes, bool willAttend, string? others, bool _, CancellationToken _) =>
            {
                using var db = _db.CreateContext();
                var x = Enrollment.Create(userId, eventId);
                (x.Instrument, x.Notes, x.WillAttend, x.OtherInstruments) = (instrument, notes, willAttend, others);
                db.Enrollments.Add(x);
                db.SaveChanges();
                return Task.FromResult(x);
            });
        _attendance.Setup(s => s.AddFirstAttendanceAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<InstrumentType?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>()))
            .Returns((int rehearsalId, string userId, bool willAttend, InstrumentType? instrument, string? notes, string? others, bool _) =>
            {
                using var db = _db.CreateContext();
                var a = RehearsalAttendance.Create(rehearsalId, userId, instrument);
                (a.WillAttend, a.Notes, a.OtherInstruments) = (willAttend, notes, others);
                db.RehearsalAttendances.Add(a);
                db.SaveChanges();
                return Task.FromResult(a);
            });
    }

    private PendingActionsService Service(params IPendingActionSource[] extra) =>
        new(new Contexts(_db), new IPendingActionSource[]
        {
            new EventResponsePendingActionSource(_enrollments.Object, _status.Object),
            new RehearsalResponsePendingActionSource(_attendance.Object),
        }.Concat(extra), _clock);

    // ---------- Secretaria (074) ----------

    private static DocumentationDto Year(string fiscalYear, params string[] folders) => new(
        [], fiscalYear, folders.Select(f => new DocumentFolderDto(f, f, [])).ToList(), [], 0, true);

    private Mock<IDocumentationService> Documentation(string[] previous, string[] current)
    {
        var docs = new Mock<IDocumentationService>();
        docs.Setup(d => d.GetAsync("2025-2026", It.IsAny<ClaimsPrincipal>())).ReturnsAsync(EventResult<DocumentationDto>.Ok(Year("2025-2026", previous)));
        docs.Setup(d => d.GetAsync(null, It.IsAny<ClaimsPrincipal>())).ReturnsAsync(() => EventResult<DocumentationDto>.Ok(Year("2026-2027", current)));
        docs.Setup(d => d.CreateFolderAsync(It.IsAny<DocumentFolderInput>(), It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((DocumentFolderInput i, ClaimsPrincipal _) => EventResult<DocumentFolderDto>.Ok(new DocumentFolderDto(i.Name!, i.Name!, [])));
        return docs;
    }

    private static SecretaryPendingActionSource SecretarySource(IDocumentationService docs) => new(docs);

    // ---------- helpers ----------

    private static string Key(int id) => id.ToString();

    private void AddInstrument(string userId, InstrumentType type, bool primary)
    {
        using var db = _db.CreateContext();
        db.MemberInstruments.Add(MemberInstrument.Create(userId, type, primary));
        db.SaveChanges();
    }

    private string AddUser(List<MemberCategory> categories, bool retired, bool expelled = false, (int Year, int Month)? leitao = null,
        List<Position>? positions = null)
    {
        using var db = _db.CreateContext();
        var id = Guid.NewGuid().ToString();
        db.Users.Add(new ApplicationUser
        {
            Id = id, UserName = id, Email = $"{id}@test.pt", FirstName = "Ana", LastName = "Silva", Nickname = "Ana",
            Categories = categories, IsRetired = retired, IsExpelled = expelled,
            YearLeitao = leitao?.Year, MonthLeitao = leitao?.Month, Positions = positions ?? [],
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

    private void AddMedia(string uploader, int? eventId, MediaType type)
    {
        using var db = _db.CreateContext();
        db.GalleryMedia.Add(GalleryMedia.Create(uploader, "Foto", type, "https://example.test/x", 2026, eventId: eventId));
        db.SaveChanges();
    }

    private void AddNews(string author, DateTime createdUtc)
    {
        using var db = _db.CreateContext();
        db.NewsPosts.Add(new NewsPost { Body = "Texto", AuthorId = author, CreatedAt = createdUtc });
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

    private sealed class Contexts(DatabaseFixture db) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => db.CreateContext();
    }
}
