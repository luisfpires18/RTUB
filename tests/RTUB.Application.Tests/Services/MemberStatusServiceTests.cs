using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
public sealed class MemberStatusServiceTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);

    private readonly DatabaseFixture _db = new();
    private readonly Mock<IPushNotificationService> _push = new();
    private readonly Mock<IPushNotificationFactory> _pushFactory = new();
    private readonly MemberStatusUpdateOptions _options = new() { Enabled = true, PushNotificationsEnabled = true };
    private int _contextsUntilFailure = int.MaxValue;

    public MemberStatusServiceTests()
    {
        _pushFactory.Setup(f => f.CreateMemberReactivatedNotification(It.IsAny<string>(), It.IsAny<string>())).Returns(new SendPushNotificationDto());
        _pushFactory.Setup(f => f.CreateMemberRetirementWarningNotification(It.IsAny<string>(), It.IsAny<string>())).Returns(new SendPushNotificationDto());
    }

    private MemberStatusService Service()
    {
        var factory = new Mock<IDbContextFactory<ApplicationDbContext>>();
        factory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => --_contextsUntilFailure == 0 ? throw new InvalidOperationException("broken member") : _db.CreateContext());
        return new MemberStatusService(factory.Object, _push.Object, _pushFactory.Object, NullLogger<MemberStatusService>.Instance,
            Options.Create(_options), new FixedClock(Now));
    }

    // ---------- Live bug regressions ----------

    [Fact]
    public async Task LiveBug_ActiveMemberPastTheThreshold_IsReformedByTheBackgroundServicePath()
    {
        var id = await AddMemberAsync(MemberCategory.Tuno, retired: false);
        await RehearsalAsync(id, new DateTime(2026, 3, 1));

        var services = new ServiceCollection().AddScoped<IMemberStatusService>(_ => Service()).BuildServiceProvider();
        var worker = new MemberStatusUpdateBackgroundService(NullLogger<MemberStatusUpdateBackgroundService>.Instance, services,
            Options.Create(_options), new FixedClock(Now));

        var run = await worker.RunUpdateAsync();

        run!.Reformed.Should().Be(1);
        run.Failed.Should().Be(0);
        (await UserAsync(id)).IsRetired.Should().BeTrue();
        (await ProjectionAsync(id)).IsRetired.Should().BeTrue();
    }

    [Fact]
    public async Task ReactivationBug_ReformedMemberWithThreeConsecutiveMonths_IsActivatedByTheDailyReconciliation()
    {
        // The old ReconcileAllMemberStatusesAsync selected only !IsRetired users, so this member was never looked at.
        var id = await AddMemberAsync(MemberCategory.Tuno, retired: true);
        await RehearsalAsync(id, new DateTime(2026, 8, 5));
        await RehearsalAsync(id, new DateTime(2026, 9, 5));
        await RehearsalAsync(id, new DateTime(2026, 10, 5));

        var run = await Service().ReconcileAllMemberStatusesAsync();

        run.Activated.Should().Be(1);
        (await UserAsync(id)).IsRetired.Should().BeFalse();
        (await ProjectionAsync(id)).IsRetired.Should().BeFalse();
    }

    // ---------- Daily job ----------

    [Fact]
    public async Task DailyJob_EvaluatesActiveAndReformedMembersAndLeitoes_AndExcludesOnlyHonorariosAndNoCategory()
    {
        var toReform = await AddMemberAsync(MemberCategory.Caloiro, retired: false);
        await RehearsalAsync(toReform, new DateTime(2025, 12, 1));
        var toActivate = await AddMemberAsync(MemberCategory.Veterano, retired: true);
        await EventAsync(toActivate, new DateTime(2026, 8, 20));
        await EventAsync(toActivate, new DateTime(2026, 9, 20));
        await RehearsalAsync(toActivate, new DateTime(2026, 10, 2));
        var stillActive = await AddMemberAsync(MemberCategory.Tuno, retired: false);
        await RehearsalAsync(stillActive, new DateTime(2026, 9, 1));
        var stillReformed = await AddMemberAsync(MemberCategory.Tunossauro, retired: true);
        var newLeitao = await AddMemberAsync(MemberCategory.Leitao, retired: false, leitaoSince: (2026, 9));
        var oldLeitao = await AddMemberAsync(MemberCategory.Leitao, retired: false, leitaoSince: (2024, 2));
        var honorarioOnly = await AddMemberAsync(MemberCategory.TunoHonorario, retired: false);
        var noCategory = await AddMemberAsync(MemberCategory.Tuno, retired: false);
        await using (var db = _db.CreateContext())
        {
            (await db.Users.SingleAsync(u => u.Id == noCategory)).Categories = new List<MemberCategory>();
            await db.SaveChangesAsync();
        }

        var run = await Service().ReconcileAllMemberStatusesAsync();

        run.Should().Be(new MemberStatusReconciliationResult(Evaluated: 6, Activated: 1, Reformed: 2, Unchanged: 3, Excluded: 2, Failed: 0));
        (await UserAsync(toReform)).IsRetired.Should().BeTrue();
        (await UserAsync(toActivate)).IsRetired.Should().BeFalse();
        (await UserAsync(stillActive)).IsRetired.Should().BeFalse();
        (await UserAsync(stillReformed)).IsRetired.Should().BeTrue();
        (await UserAsync(newLeitao)).IsRetired.Should().BeFalse("six months from September 2026 have not passed");
        (await UserAsync(oldLeitao)).IsRetired.Should().BeTrue("a Leitão is reformed after six months without activity too");
        (await UserAsync(honorarioOnly)).IsRetired.Should().BeFalse("only Tuno Honorário: out of the automatic rule");
        (await UserAsync(noCategory)).IsRetired.Should().BeFalse("no category: out of the automatic rule");
    }

    [Fact]
    public async Task DailyJob_OneMemberFails_TheOthersAreStillEvaluated_AndTheFailureIsCounted()
    {
        var ids = new[]
        {
            await AddMemberAsync(MemberCategory.Tuno, retired: false),
            await AddMemberAsync(MemberCategory.Tuno, retired: false),
            await AddMemberAsync(MemberCategory.Tuno, retired: false),
        };
        // Context 1 loads the batch; context 2 is the first member's - it throws.
        _contextsUntilFailure = 2;

        var run = await Service().ReconcileAllMemberStatusesAsync();

        run.Failed.Should().Be(1);
        run.Reformed.Should().Be(2);
        run.Evaluated.Should().Be(2);
        var flags = new List<bool>();
        foreach (var id in ids)
        {
            flags.Add((await UserAsync(id)).IsRetired);
        }

        flags.Count(f => f).Should().Be(2);
    }

    [Fact]
    public async Task DailyJob_SecondIdenticalRun_ChangesNothing_AuditsAndNotifiesOnce()
    {
        var id = await AddMemberAsync(MemberCategory.Tuno, retired: true);
        await RehearsalAsync(id, new DateTime(2026, 8, 5));
        await RehearsalAsync(id, new DateTime(2026, 9, 5));
        await RehearsalAsync(id, new DateTime(2026, 10, 5));
        var service = Service();

        var first = await service.ReconcileAllMemberStatusesAsync();
        var updatedAt = (await ProjectionAsync(id)).LastUpdatedAt;
        var runs = new List<MemberStatusReconciliationResult>();
        for (var n = 0; n < 4; n++)
        {
            runs.Add(await service.ReconcileAllMemberStatusesAsync());
        }

        first.Activated.Should().Be(1);
        runs.Should().AllSatisfy(r => r.Changed.Should().Be(0));
        _push.Verify(p => p.BroadcastAsync(It.IsAny<SendPushNotificationDto>()), Times.Once);
        (await TransitionsAsync(id)).Should().ContainSingle().Which.Action.Should().Be("Activated");
        (await ProjectionAsync(id)).LastUpdatedAt.Should().Be(updatedAt, "an unchanged projection is not rewritten");
    }

    [Fact]
    public async Task Disabled_RunsNothing()
    {
        _options.Enabled = false;
        var id = await AddMemberAsync(MemberCategory.Tuno, retired: false);

        (await Service().ReconcileAllMemberStatusesAsync()).Should().Be(MemberStatusReconciliationResult.Empty);
        (await UserAsync(id)).IsRetired.Should().BeFalse();
    }

    // ---------- Activity sources ----------

    [Fact]
    public async Task RehearsalOnlyStreak_Activates()
    {
        var id = await AddMemberAsync(MemberCategory.Tuno, retired: true);
        foreach (var month in new[] { 7, 8, 9 })
        {
            await RehearsalAsync(id, new DateTime(2026, month, 3));
        }

        (await Service().ReconcileMemberStatusAsync(id)).IsRetired.Should().BeFalse();
    }

    [Fact]
    public async Task PerformanceOnlyStreak_Activates()
    {
        var id = await AddMemberAsync(MemberCategory.Tuno, retired: true);
        foreach (var month in new[] { 7, 8, 9 })
        {
            await EventAsync(id, new DateTime(2026, month, 3));
        }

        (await Service().ReconcileMemberStatusAsync(id)).IsRetired.Should().BeFalse();
    }

    [Fact]
    public async Task MixedStreak_Activates()
    {
        var id = await AddMemberAsync(MemberCategory.Tuno, retired: true);
        await RehearsalAsync(id, new DateTime(2026, 8, 3));
        await EventAsync(id, new DateTime(2026, 9, 3));
        await RehearsalAsync(id, new DateTime(2026, 10, 3));
        await EventAsync(id, new DateTime(2026, 10, 4));

        (await Service().ReconcileMemberStatusAsync(id)).IsRetired.Should().BeFalse();
    }

    [Fact]
    public async Task NonQualifyingParticipation_NeverCounts()
    {
        var id = await AddMemberAsync(MemberCategory.Tuno, retired: true);
        await RehearsalAsync(id, new DateTime(2026, 8, 3), attended: false);
        await RehearsalAsync(id, new DateTime(2026, 9, 3), canceled: true);
        await EventAsync(id, new DateTime(2026, 9, 10), willAttend: false);
        await EventAsync(id, new DateTime(2026, 10, 1), cancelled: true);
        await EventAsync(id, new DateTime(2026, 10, 12), end: new DateTime(2026, 10, 14));
        await RehearsalAsync(id, new DateTime(2026, 10, 20));

        var status = await Service().GetMemberStatusAsync(id);

        status.HasAnyActivity.Should().BeFalse("unconfirmed, cancelled, not-going, unfinished and future activities do not count");
        status.IsRetired.Should().BeTrue();
    }

    // ---------- Projection consistency ----------

    [Fact]
    public async Task StaleProjection_CannotPreventRecalculation_AndEndsEqualToTheUserFlag()
    {
        var id = await AddMemberAsync(MemberCategory.Tuno, retired: false);
        await RehearsalAsync(id, new DateTime(2026, 1, 10));
        await using (var db = _db.CreateContext())
        {
            db.MemberStatuses.Add(new MemberStatus
            {
                UserId = id, IsRetired = false, HasAnyActivity = true, LastActivityDate = Now.AddDays(-1),
                ProgressMonths = 6, ProgressTotalMonths = 6, OverrideRetired = true, LastUpdatedAt = Now.AddMinutes(-5),
            });
            await db.SaveChangesAsync();
        }

        var status = await Service().ReconcileMemberStatusAsync(id);

        status.IsRetired.Should().BeTrue("the stale projection and the retired override flag are not business truth");
        var projection = await ProjectionAsync(id);
        projection.IsRetired.Should().Be((await UserAsync(id)).IsRetired).And.BeTrue();
        projection.LastActivityDate.Should().Be(new DateTime(2026, 1, 10));
        projection.ProgressTotalMonths.Should().Be(3);
    }

    [Fact]
    public async Task Transition_IsAuditedWithTheReasonAndDates()
    {
        var id = await AddMemberAsync(MemberCategory.Tuno, retired: false);
        await RehearsalAsync(id, new DateTime(2026, 4, 13, 11, 0, 0));

        await Service().ReconcileAllMemberStatusesAsync();

        var audit = (await TransitionsAsync(id)).Should().ContainSingle().Subject;
        audit.Action.Should().Be("Reformed");
        audit.Changes.Should().Contain("SixMonthsWithoutActivity").And.Contain("2026-04-13").And.Contain("2026-10-13");
    }

    [Fact]
    public async Task RetirementWarning_IsSentOnceWhenTheLastMonthStarts()
    {
        var id = await AddMemberAsync(MemberCategory.Tuno, retired: false);
        await RehearsalAsync(id, new DateTime(2026, 5, 20));
        var service = Service();

        (await service.ReconcileMemberStatusAsync(id)).ProgressMonths.Should().Be(1);
        await service.ReconcileAllMemberStatusesAsync();
        await service.ReconcileAllMemberStatusesAsync();

        _push.Verify(p => p.SendToUserAsync(id, It.IsAny<SendPushNotificationDto>()), Times.Once);
    }

    [Fact]
    public async Task Hotfix_FinalPartialMonth_ShowsOneMonthUntilRetirement()
    {
        // Calhau: last activity 05 May, threshold 05 Nov; on 13 Oct less than one full month is left.
        var id = await AddMemberAsync(MemberCategory.Tuno, retired: false);
        await RehearsalAsync(id, new DateTime(2026, 5, 5));

        var status = await Service().ReconcileMemberStatusAsync(id);

        status.IsRetired.Should().BeFalse();
        status.ProgressMonths.Should().Be(1);
        status.ProgressDescription.Should().Be("1 mês até reforma");
        _push.Verify(p => p.SendToUserAsync(id, It.IsAny<SendPushNotificationDto>()), Times.Once);
    }

    [Theory]
    [InlineData(1, false, true)]
    [InlineData(1, true, false)] // activity this month: no countdown
    [InlineData(5, true, false)]
    [InlineData(3, false, true)]
    public void Hotfix_DirectoryCountdown_IsShownForTheFinalMonth_AndStillSuppressedByActivityThisMonth(
        int months, bool activityThisMonth, bool shown)
    {
        var status = new MemberStatusResult
        {
            IsRetired = false, ProgressMonths = months, ProgressTotalMonths = MemberActivityStatusEvaluator.InactivityMonths,
            ProgressDescription = months == 1 ? "1 mês até reforma" : $"{months} meses até reforma",
            HasActivityInCurrentMonth = activityThisMonth,
        };

        MemberDirectoryService.ShowProgress(status).Should().Be(shown);
    }

    [Fact]
    public async Task Hotfix_MultipleMonths_KeepThePluralDescription()
    {
        var id = await AddMemberAsync(MemberCategory.Tuno, retired: false);
        await RehearsalAsync(id, new DateTime(2026, 7, 20));

        (await Service().ReconcileMemberStatusAsync(id)).ProgressDescription.Should().Be("3 meses até reforma");
    }

    [Theory]
    [InlineData(0, false)] // stored by the old countdown in its final partial month: already warned when it passed 1
    [InlineData(1, false)]
    [InlineData(2, true)]
    public async Task Hotfix_RetirementWarning_IsNotRepeatedForAMemberAlreadyInTheFinalMonth(int storedMonths, bool warned)
    {
        var id = await AddMemberAsync(MemberCategory.Tuno, retired: false);
        await RehearsalAsync(id, new DateTime(2026, 5, 5));
        await using (var db = _db.CreateContext())
        {
            db.MemberStatuses.Add(new MemberStatus
            {
                UserId = id, IsRetired = false, HasAnyActivity = true, LastActivityDate = new DateTime(2026, 5, 5),
                ProgressMonths = storedMonths, ProgressTotalMonths = 6, LastUpdatedAt = Now.AddDays(-1),
            });
            await db.SaveChangesAsync();
        }

        var service = Service();
        (await service.ReconcileMemberStatusAsync(id)).ProgressMonths.Should().Be(1);
        await service.ReconcileAllMemberStatusesAsync();

        _push.Verify(p => p.SendToUserAsync(id, It.IsAny<SendPushNotificationDto>()), warned ? Times.Once() : Times.Never());
    }

    [Fact]
    public async Task UnknownUser_Throws_AndTheBatchMapsItToNull()
    {
        var service = Service();

        await service.Invoking(s => s.GetMemberStatusAsync("nobody")).Should().ThrowAsync<InvalidOperationException>();
        (await service.GetMemberStatusesBatchAsync(new[] { "nobody" }))["nobody"].Should().BeNull();
    }

    // ---------- Tuno Honorário with a membership category ----------

    [Fact]
    public async Task TunoHonorario_WithAMembershipCategory_IsReconciledLikeAnyMember()
    {
        var tuno = await AddMemberAsync(MemberCategory.Tuno, retired: false, MemberCategory.TunoHonorario);
        await RehearsalAsync(tuno, new DateTime(2025, 12, 1));
        var veterano = await AddMemberAsync(MemberCategory.Veterano, retired: true, MemberCategory.TunoHonorario);
        foreach (var month in new[] { 8, 9, 10 })
        {
            await RehearsalAsync(veterano, new DateTime(2026, month, 5));
        }

        var leitao = await AddMemberAsync(MemberCategory.Leitao, retired: false, (2024, 2), MemberCategory.TunoHonorario);
        var honorarioOnly = await AddMemberAsync(MemberCategory.TunoHonorario, retired: false);

        var run = await Service().ReconcileAllMemberStatusesAsync();

        run.Should().Be(new MemberStatusReconciliationResult(Evaluated: 3, Activated: 1, Reformed: 2, Unchanged: 0, Excluded: 1, Failed: 0));
        (await UserAsync(tuno)).IsRetired.Should().BeTrue("Tuno + Tuno Honorário: six months without activity");
        (await UserAsync(veterano)).IsRetired.Should().BeFalse("Veterano + Tuno Honorário: three consecutive months");
        (await UserAsync(leitao)).IsRetired.Should().BeTrue("Leitão + Tuno Honorário: six months from the Leitão date");
        (await UserAsync(honorarioOnly)).IsRetired.Should().BeFalse();
        (await ProjectionAsync(veterano)).IsRetired.Should().BeFalse();
    }

    // ---------- Leitões ----------

    [Fact]
    public async Task NewLeitao_NoActivity_BeforeTheThreshold_StaysActive()
    {
        // Leitão since May 2026: six months run to 1 Nov 2026; now is 13 Oct.
        var id = await AddMemberAsync(MemberCategory.Leitao, retired: false, leitaoSince: (2026, 5));

        var status = await Service().ReconcileMemberStatusAsync(id);

        status.IsRetired.Should().BeFalse();
        (await UserAsync(id)).IsRetired.Should().BeFalse();
    }

    [Fact]
    public async Task Leitao_NoActivity_PastTheThreshold_IsReformed()
    {
        // Leitão since April 2026: the threshold was 1 Oct 2026.
        var id = await AddMemberAsync(MemberCategory.Leitao, retired: false, leitaoSince: (2026, 4));

        await Service().ReconcileAllMemberStatusesAsync();

        (await UserAsync(id)).IsRetired.Should().BeTrue();
        (await TransitionsAsync(id)).Should().ContainSingle().Which.Changes.Should().Contain("SixMonthsWithoutActivity").And.Contain("2026-10-01");
    }

    [Fact]
    public async Task Leitao_ActivityAfterTheStart_MovesTheThreshold()
    {
        var id = await AddMemberAsync(MemberCategory.Leitao, retired: false, leitaoSince: (2026, 1));
        await RehearsalAsync(id, new DateTime(2026, 6, 10));

        (await Service().ReconcileMemberStatusAsync(id)).IsRetired.Should().BeFalse("six months after 10 June is 10 December");
    }

    // ---------- Reads never write ----------

    [Fact]
    public async Task Reads_NeverTransition_WriteTheProjection_AuditOrNotify()
    {
        // Active but well past the threshold, and a Reformado with three consecutive months: both would change on reconcile.
        var active = await AddMemberAsync(MemberCategory.Tuno, retired: false);
        await RehearsalAsync(active, new DateTime(2025, 1, 10));
        var reformed = await AddMemberAsync(MemberCategory.Tuno, retired: true);
        foreach (var month in new[] { 8, 9, 10 })
        {
            await RehearsalAsync(reformed, new DateTime(2026, month, 5));
        }

        int auditsBefore;
        await using (var before = _db.CreateContext())
        {
            auditsBefore = await before.AuditLogs.CountAsync();
        }

        var service = Service();
        for (var n = 0; n < 3; n++)
        {
            (await service.GetMemberStatusAsync(active)).IsRetired.Should().BeFalse("a read shows the persisted flag");
            (await service.GetMemberStatusesBatchAsync(new[] { active, reformed }))[reformed]!.IsRetired.Should().BeTrue();
        }

        (await UserAsync(active)).IsRetired.Should().BeFalse();
        (await UserAsync(reformed)).IsRetired.Should().BeTrue();
        await using var db = _db.CreateContext();
        (await db.MemberStatuses.CountAsync()).Should().Be(0);
        (await db.AuditLogs.CountAsync()).Should().Be(auditsBefore, "a read writes no audit entry");
        _push.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Read_ShowsComputedProgress_ForThePersistedState()
    {
        var active = await AddMemberAsync(MemberCategory.Tuno, retired: false);
        await RehearsalAsync(active, new DateTime(2025, 1, 10));
        var reformed = await AddMemberAsync(MemberCategory.Tuno, retired: true);
        await RehearsalAsync(reformed, new DateTime(2026, 9, 5));

        var statuses = await Service().GetMemberStatusesBatchAsync(new[] { active, reformed });

        statuses[active]!.ProgressDescription.Should().Be("Próximo da reforma", "past the threshold, until the next reconciliation");
        statuses[reformed]!.ProgressDescription.Should().Be("1/3 meses de atividade consecutiva");
    }

    [Fact]
    public async Task Reconcile_AfterReads_StillTransitions()
    {
        var id = await AddMemberAsync(MemberCategory.Tuno, retired: false);
        await RehearsalAsync(id, new DateTime(2025, 1, 10));
        var service = Service();
        await service.GetMemberStatusAsync(id);

        var run = await service.ReconcileAllMemberStatusesAsync();

        run.Reformed.Should().Be(1);
        (await UserAsync(id)).IsRetired.Should().BeTrue();
        (await ProjectionAsync(id)).IsRetired.Should().BeTrue();
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
