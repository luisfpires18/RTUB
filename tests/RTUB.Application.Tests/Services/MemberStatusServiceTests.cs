using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// The member status orchestration (task 044B) against a database: the daily reconciliation, ApplicationUser.IsRetired vs
/// the MemberStatus projection, audit, notifications, idempotence and per-member failure handling. One database per test
/// and a fixed clock, so counts and dates are exact.
/// </summary>
public sealed partial class MemberStatusServiceTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);

    private readonly DatabaseFixture _db = new();
    private readonly Mock<IPushNotificationService> _push = new();
    private readonly Mock<IPushNotificationFactory> _pushFactory = new();
    private readonly MemberStatusUpdateOptions _options = new() { Enabled = true, PushNotificationsEnabled = true };
    private int _contextsUntilFailure = int.MaxValue;

    public MemberStatusServiceTests()
    {
        _pushFactory.Setup(f => f.CreateMemberReactivatedNotification(It.IsAny<string>())).Returns(new SendPushNotificationDto());
        _pushFactory.Setup(f => f.CreateMemberRetirementWarningNotification(It.IsAny<string>())).Returns(new SendPushNotificationDto());
    }

    private MemberStatusService Service()
    {
        var factory = new Mock<IDbContextFactory<ApplicationDbContext>>();
        factory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => --_contextsUntilFailure == 0 ? throw new InvalidOperationException("broken member") : _db.CreateContext());
        return new MemberStatusService(factory.Object, _push.Object, _pushFactory.Object, NullLogger<MemberStatusService>.Instance,
            Options.Create(_options), new FixedClock(Now));
    }

    // ---------- helpers ----------

    private Task<string> AddMemberAsync(MemberCategory category, bool retired, params MemberCategory[] more) =>
        AddMemberAsync(category, retired, (2015, 10), more);

    private async Task<string> AddMemberAsync(MemberCategory category, bool retired, (int Year, int Month) leitaoSince, params MemberCategory[] more)
    {
        var id = Guid.NewGuid().ToString();
        await using var db = _db.CreateContext();
        db.Users.Add(new ApplicationUser
        {
            Id = id, UserName = "u" + id[..8], Email = id + "@test.com", FirstName = "F", LastName = "L", Nickname = "N" + id[..4],
            PhoneNumber = "912345678", Categories = new List<MemberCategory> { category }.Concat(more).ToList(),
            YearLeitao = leitaoSince.Year, MonthLeitao = leitaoSince.Month, IsRetired = retired,
        });
        await db.SaveChangesAsync();
        return id;
    }

    private async Task RehearsalAsync(string userId, DateTime date, bool attended = true, bool canceled = false)
    {
        await using var db = _db.CreateContext();
        var rehearsal = Rehearsal.Create(date, "Sala");
        rehearsal.Date = date;
        rehearsal.IsCanceled = canceled;
        db.Rehearsals.Add(rehearsal);
        await db.SaveChangesAsync();
        var attendance = RehearsalAttendance.Create(rehearsal.Id, userId);
        attendance.MarkAttendance(attended);
        db.RehearsalAttendances.Add(attendance);
        await db.SaveChangesAsync();
    }

    private async Task EventAsync(string userId, DateTime date, bool willAttend = true, bool cancelled = false, DateTime? end = null)
    {
        await using var db = _db.CreateContext();
        var ev = Event.Create("Atuação", date, "Bragança", EventType.Atuacao);
        ev.EndDate = end;
        ev.IsCancelled = cancelled;
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        var enrollment = Enrollment.Create(userId, ev.Id);
        enrollment.WillAttend = willAttend;
        db.Enrollments.Add(enrollment);
        await db.SaveChangesAsync();
    }

    private async Task<ApplicationUser> UserAsync(string id)
    {
        await using var db = _db.CreateContext();
        return await db.Users.AsNoTracking().SingleAsync(u => u.Id == id);
    }

    /// <summary>A member the reconciliation has seen before (a MemberStatus row from yesterday's run).</summary>
    private async Task SeenAsync(string id, bool retired, int progressMonths)
    {
        await using var db = _db.CreateContext();
        db.MemberStatuses.Add(new MemberStatus
        {
            UserId = id, IsRetired = retired, HasAnyActivity = true, ProgressMonths = progressMonths,
            ProgressTotalMonths = retired ? 3 : 6, LastUpdatedAt = Now.AddDays(-1),
        });
        await db.SaveChangesAsync();
    }

    private async Task<MemberStatus> ProjectionAsync(string id)
    {
        await using var db = _db.CreateContext();
        return await db.MemberStatuses.AsNoTracking().SingleAsync(s => s.UserId == id);
    }

    private async Task<List<AuditLog>> TransitionsAsync(string id)
    {
        await using var db = _db.CreateContext();
        return await db.AuditLogs.AsNoTracking().Where(a => a.EntityType == MemberStatusService.AuditEntityType && a.UserId == id).ToListAsync();
    }

    public void Dispose() => _db.Dispose();

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);
    }
}
