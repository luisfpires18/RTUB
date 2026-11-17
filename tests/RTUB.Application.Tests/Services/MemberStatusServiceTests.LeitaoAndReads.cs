// MemberStatusService: leitao and reads.
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public sealed partial class MemberStatusServiceTests
{
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
}
