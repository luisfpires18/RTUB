// MemberStatusService: transitions and notifications.
using FluentAssertions;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public sealed partial class MemberStatusServiceTests
{
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
                ProgressMonths = 6, ProgressTotalMonths = 6, LastUpdatedAt = Now.AddMinutes(-5),
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
        await SeenAsync(id, retired: false, progressMonths: 2);
        var service = Service();

        await service.ReconcileAllMemberStatusesAsync(notify: true);
        await service.ReconcileAllMemberStatusesAsync(notify: true);

        (await ProjectionAsync(id)).ProgressMonths.Should().Be(1);
        _push.Verify(p => p.SendToUserAsync(id, It.IsAny<SendPushNotificationDto>()), Times.Once);
        _push.Verify(p => p.BroadcastAsync(It.IsAny<SendPushNotificationDto>()), Times.Never, "only the member losing the status hears it");
    }

    // ---------- Pushes come only from the scheduled run (task 058) ----------

    [Fact]
    public async Task Triggers_AndTheOwnerSync_NeverNotify_EvenWhenTheyReactivate()
    {
        var id = await AddMemberAsync(MemberCategory.Tuno, retired: true);
        await RehearsalAsync(id, new DateTime(2026, 8, 5));
        await RehearsalAsync(id, new DateTime(2026, 9, 5));
        await RehearsalAsync(id, new DateTime(2026, 10, 5));
        await SeenAsync(id, retired: true, progressMonths: 2);
        var service = Service();

        (await service.ReconcileMemberStatusAsync(id)).IsRetired.Should().BeFalse("a presença approval still reactivates at once");
        await service.ReconcileAllMemberStatusesAsync();
        await service.ReconcileAllMemberStatusesAsync(notify: true);

        _push.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FirstEvaluation_NewMemberOrFirstPopulation_NeverNotifies()
    {
        var returning = await AddMemberAsync(MemberCategory.Tuno, retired: true);
        await RehearsalAsync(returning, new DateTime(2026, 8, 5));
        await RehearsalAsync(returning, new DateTime(2026, 9, 5));
        await RehearsalAsync(returning, new DateTime(2026, 10, 5));
        var lastMonth = await AddMemberAsync(MemberCategory.Tuno, retired: false);
        await RehearsalAsync(lastMonth, new DateTime(2026, 5, 20));

        var run = await Service().ReconcileAllMemberStatusesAsync(notify: true);

        run.Activated.Should().Be(1);
        (await ProjectionAsync(lastMonth)).ProgressMonths.Should().Be(1);
        _push.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RetirementWarning_StillSent_WhenATriggerStoredTheLastMonthEarlierThatDay()
    {
        // Threshold 13 Dec 00:00: the countdown became 1 at 13 Oct 00:00, half a day before the run at 12:00.
        var id = await AddMemberAsync(MemberCategory.Tuno, retired: false);
        await RehearsalAsync(id, new DateTime(2026, 6, 13));
        await SeenAsync(id, retired: false, progressMonths: 2);
        var service = Service();

        (await service.ReconcileMemberStatusAsync(id)).ProgressMonths.Should().Be(1);
        await service.ReconcileAllMemberStatusesAsync(notify: true);

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
        _push.VerifyNoOtherCalls(); // a trigger never notifies (task 058)
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

        await Service().ReconcileAllMemberStatusesAsync(notify: true);

        (await ProjectionAsync(id)).ProgressMonths.Should().Be(1);
        _push.Verify(p => p.SendToUserAsync(id, It.IsAny<SendPushNotificationDto>()), warned ? Times.Once() : Times.Never());
    }
}
