// MemberStatusService: reconciliation.
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Enums;
using RTUB.Tests;

namespace RTUB.Application.Tests.Services;

public sealed partial class MemberStatusServiceTests
{
    // ---------- Live bug regressions ----------

    [Fact]
    public async Task LiveBug_ActiveMemberPastTheThreshold_IsReformedByTheBackgroundServicePath()
    {
        var id = await AddMemberAsync(MemberCategory.Tuno, retired: false);
        await RehearsalAsync(id, new DateTime(2026, 3, 1));

        var services = new ServiceCollection().AddScoped<IMemberStatusService>(_ => Service()).BuildServiceProvider();
        var worker = new MemberStatusUpdateBackgroundService(NullLogger<MemberStatusUpdateBackgroundService>.Instance, services,
            Options.Create(_options), new TestClock(new DateTimeOffset(Now, TimeSpan.Zero)));

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
        await SeenAsync(id, retired: true, progressMonths: 2);
        var service = Service();

        var first = await service.ReconcileAllMemberStatusesAsync(notify: true);
        var updatedAt = (await ProjectionAsync(id)).LastUpdatedAt;
        var runs = new List<MemberStatusReconciliationResult>();
        for (var n = 0; n < 4; n++)
        {
            runs.Add(await service.ReconcileAllMemberStatusesAsync(notify: true));
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
}
