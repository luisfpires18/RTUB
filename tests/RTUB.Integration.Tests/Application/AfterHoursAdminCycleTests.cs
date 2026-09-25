using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Application.Data;
using RTUB.Application.Interfaces.AfterHours;
using RTUB.Application.Services.AfterHours;
using RTUB.Core.Entities;
using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;
using RTUB.Core.Helpers.AfterHours;
using Xunit;

namespace RTUB.Integration.Tests.Application;

/// <summary>
/// AH-010 cycle controls and readiness through the Owner-only admin service, on the real SQLite schema. Every test
/// starts from no active cycle and uses its own fiscal years.
/// </summary>
public class AfterHoursAdminCycleTests : IClassFixture<AfterHoursAdminFactory>
{
    private static int _nextYear = 5000;
    private readonly AfterHoursAdminFactory _factory;
    private static DateTime Now0 => AfterHoursPlayFactory.Start;

    public AfterHoursAdminCycleTests(AfterHoursAdminFactory factory)
    {
        _factory = factory;
        factory.Now = Now0;
        using var db = factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext();
        db.AfterHoursTuningSettings.ExecuteDelete();
        // Test isolation only: park whatever an earlier test left active.
        db.AfterHoursGameCycles.Where(c => c.Status == GameCycleStatus.Active)
            .ExecuteUpdate(s => s.SetProperty(c => c.Status, GameCycleStatus.Finished));
    }

    [Fact]
    public async Task CreatePilot_StartsIt_KeepsOneActiveCycle_AndCreatesNoPlayerState()
    {
        var owner = await OwnerAsync();
        var year = await FiscalYearAsync();

        var pilot = await Admin().CreatePilotAsync(owner, year.Id, Now0, Now0.AddDays(AfterHoursAdminService.PilotDefaultDays), false);

        (pilot.Kind, pilot.Status, pilot.PlayableNow, pilot.Players, pilot.EndUtc - pilot.StartUtc).Should().Be(
            (GameCycleKind.Pilot, GameCycleStatus.Active, true, 0, TimeSpan.FromDays(10)));
        await FluentActions.Invoking(() => Admin().CreatePilotAsync(owner, year.Id, Now0, Now0.AddDays(10), false))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*Another cycle is Active*");
        await using var db = await DbAsync();
        (await db.AfterHoursGameCycles.CountAsync(c => c.Status == GameCycleStatus.Active)).Should().Be(1);
        (await db.AfterHoursPlayerCycleStates.CountAsync(s => s.GameCycleId == pilot.Id)).Should().Be(0);
    }

    [Fact]
    public async Task CreatePilot_Validates_AndConcurrentCreatesMakeOneActiveCycle()
    {
        var owner = await OwnerAsync();
        var year = await FiscalYearAsync();

        await FluentActions.Invoking(() => Admin().CreatePilotAsync(owner, year.Id, Now0, Now0.AddDays(3), false))
            .Should().ThrowAsync<ArgumentException>().WithMessage("*outside the recommended 7–14*");
        await FluentActions.Invoking(() => Admin().CreatePilotAsync(owner, year.Id, Now0, Now0, true)).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => Admin().CreatePilotAsync(owner, year.Id, DateTime.SpecifyKind(Now0, DateTimeKind.Local), Now0.AddDays(10), false))
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => Admin().CreatePilotAsync(owner, year.Id, Now0.AddDays(-20), Now0.AddDays(-10), false))
            .Should().ThrowAsync<ArgumentException>().WithMessage("*already be over*");
        await FluentActions.Invoking(() => Admin().CreatePilotAsync(owner, 999_999, Now0, Now0.AddDays(10), false))
            .Should().ThrowAsync<InvalidOperationException>();

        var attempts = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(async () =>
        {
            try { await Admin().CreatePilotAsync(owner, year.Id, Now0, Now0.AddDays(3), true); return true; }
            catch (InvalidOperationException) { return false; }
        })));

        attempts.Count(ok => ok).Should().Be(1);
        await using var db = await DbAsync();
        (await db.AfterHoursGameCycles.CountAsync(c => c.Status == GameCycleStatus.Active)).Should().Be(1);
    }

    [Fact]
    public async Task PilotToLive_UsesTheRolloverService_NonOfficial_Idempotent_AndAwardsSurvive()
    {
        var owner = await OwnerAsync();
        var year = await FiscalYearAsync();
        var pilot = await Admin().CreatePilotAsync(owner, year.Id, Now0, Now0.AddDays(10), false);
        var player = await Service<IPlayerCycleStateService>().GetOrCreateForActiveCycleAsync(await UserAsync("pl"));
        var award = await Admin().GrantAwardAsync(owner, player!.UserId, "Pilot Pioneer", null, year.Id, null);
        _factory.Now = Now0.AddDays(2);
        var (start, end) = (_factory.Now, RolloverRules.SeptemberStartUtc(year.EndYear));

        var first = await Admin().TransitionPilotToLiveAsync(owner, pilot.Id, year.Id, start, end);
        var again = await Admin().TransitionPilotToLiveAsync(owner, pilot.Id, year.Id, start, end);

        again.Should().Be(first with { Replayed = true });
        var archive = (await Service<IYearbookService>().GetArchivesAsync()).Single(a => a.Id == first.ArchiveId);
        (archive.Kind, archive.Official).Should().Be((GameCycleKind.Pilot, false));
        var status = await Admin().GetStatusAsync(owner);
        (status.Active!.Id, status.Active.Kind).Should().Be((first.TargetCycleId, GameCycleKind.Live));
        (await Service<IYearbookService>().GetActiveAwardsAsync(player.UserId)).Should().ContainSingle(a => a.Id == award.Id, "a title is not cycle power");
    }

    [Fact]
    public async Task LiveRollover_IsBlockedBeforeTheEnd_ShowsAMissingFiscalYear_AndRunsTheSafeServiceOnce()
    {
        var owner = await OwnerAsync();
        var (live, next) = await LiveCycleAsync(withNextYear: false);
        _factory.Now = live.EndUtc.AddDays(-1);

        var early = await Admin().GetStatusAsync(owner);
        early.LiveRollover!.AvailableNow.Should().BeFalse();
        early.LiveRollover.Blocker.Should().StartWith("No RTUB fiscal year starts in", "the missing fiscal year is reported first");
        early.NextFiscalYearExists.Should().BeFalse();
        early.Warnings.Should().Contain(w => w.Contains("does not exist"));
        early.Readiness.Single(c => c.Name == "Next fiscal year").Should().Match<ReadinessCheck>(c => !c.Passed && !c.Blocking);

        next = await FiscalYearAsync(next.StartYear);
        var withYear = await Admin().GetStatusAsync(owner);
        (withYear.LiveRollover!.Next!.FiscalYearId, withYear.LiveRollover.AvailableNow).Should().Be((next.Id, false));
        withYear.LiveRollover.Blocker.Should().StartWith("Available from");
        await FluentActions.Invoking(() => Admin().RolloverLiveAsync(owner, live.Id))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*waits for its end*", "force rollover never bypasses the end rule");

        _factory.Now = live.EndUtc;
        (await Admin().GetStatusAsync(owner)).LiveRollover!.AvailableNow.Should().BeTrue();
        var result = await Admin().RolloverLiveAsync(owner, live.Id);
        (await Admin().RolloverLiveAsync(owner, live.Id)).Should().Be(result with { Replayed = true });
        var after = await Admin().GetStatusAsync(owner);
        (after.Active!.Id, after.Active.StartUtc).Should().Be((result.TargetCycleId, RolloverRules.SeptemberStartUtc(next.StartYear)));
    }

    [Fact]
    public async Task Readiness_IsReadyOnTheHappyPath_AndEachCriticalBlockerBlocks()
    {
        var owner = await OwnerAsync();
        (await Admin().GetStatusAsync(owner)).Should().Match<AfterHoursAdminStatus>(s => !s.Ready && s.Active == null
            && s.Readiness.Any(c => c.Name == "Active cycle" && !c.Passed));

        var (live, _) = await LiveCycleAsync(withNextYear: true);
        var ready = await Admin().GetStatusAsync(owner);
        ready.Readiness.Where(c => c.Blocking).Should().OnlyContain(c => c.Passed, string.Join("; ", ready.Readiness.Select(c => $"{c.Name}: {c.Detail}")));
        ready.Ready.Should().BeTrue();
        ready.Readiness.Select(c => c.Name).Should().Contain(["After Hours enabled here", "Database schema", "Playable now", "Cycle boundaries", "PvP",
            "Tuning overrides valid", "Family cap is structural 4", "Level cap is structural 20", "Bank fee resolves", "Next fiscal year",
            "Rollover service", "No unarchived finished cycle", "Objectives and standings", "Yearbook"]);

        await Admin().SetPvpEnabledAsync(owner, false);
        (await Admin().GetStatusAsync(owner)).Ready.Should().BeTrue("an explicit pause is a decision, not a blocker");
        await Admin().SetPvpEnabledAsync(owner, true);

        _factory.Now = live.EndUtc;
        (await Admin().GetStatusAsync(owner)).Should().Match<AfterHoursAdminStatus>(s => !s.Ready
            && s.Warnings.Any(w => w.Contains("outside its playable window")));
    }

    [Fact]
    public async Task Status_ReportsAFinishedCycleWithoutAnArchive()
    {
        var owner = await OwnerAsync();
        var year = await FiscalYearAsync();
        var orphan = await Service<IGameCycleService>().CreateCycleAsync(year.Id, GameCycleKind.Live, Now0.AddDays(-30), Now0.AddDays(-1));
        await using (var db = await DbAsync())
            await db.AfterHoursGameCycles.Where(c => c.Id == orphan.Id).ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, GameCycleStatus.Finished));

        var status = await Admin().GetStatusAsync(owner);

        status.Warnings.Should().Contain(w => w.Contains($"Cycle {orphan.Id}") && w.Contains("without a yearbook archive"));
        status.Readiness.Single(c => c.Name == "No unarchived finished cycle").Should().Match<ReadinessCheck>(c => !c.Passed && !c.Blocking);
    }

    // ------------------------------------------------------------------ helpers

    private IAfterHoursAdminService Admin() => Service<IAfterHoursAdminService>();
    private T Service<T>() where T : notnull => _factory.Services.CreateScope().ServiceProvider.GetRequiredService<T>();

    private Task<ApplicationDbContext> DbAsync() =>
        _factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();

    private async Task<string> UserAsync(string label, string? role = null)
    {
        var name = $"ah10c-{label}-{Guid.NewGuid():N}"[..34];
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = name, Email = $"{name}@test.com", FirstName = "A", LastName = "H", Nickname = name };
        (await users.CreateAsync(user)).Succeeded.Should().BeTrue();
        if (role is not null) (await users.AddToRoleAsync(user, role)).Succeeded.Should().BeTrue();
        return user.Id;
    }

    private Task<string> OwnerAsync() => UserAsync("owner", "Owner");

    private async Task<FiscalYear> FiscalYearAsync(int? startYear = null)
    {
        var start = startYear ?? Interlocked.Add(ref _nextYear, 3);
        await using var db = await DbAsync();
        var year = FiscalYear.Create(start, start + 1);
        db.FiscalYears.Add(year);
        await db.SaveChangesAsync();
        return year;
    }

    /// <summary>An Active Live cycle over a fresh fiscal year, clock 100 days in; the next year optionally created.</summary>
    private async Task<(GameCycle Live, FiscalYear Next)> LiveCycleAsync(bool withNextYear)
    {
        var year = await FiscalYearAsync();
        var next = withNextYear ? await FiscalYearAsync(year.EndYear) : FiscalYear.Create(year.EndYear, year.EndYear + 1);
        var cycles = Service<IGameCycleService>();
        var live = await cycles.CreateCycleAsync(year.Id, GameCycleKind.Live, RolloverRules.SeptemberStartUtc(year.StartYear), RolloverRules.SeptemberStartUtc(year.EndYear));
        await cycles.ActivateAsync(live.Id);
        _factory.Now = live.StartUtc.AddDays(100);
        return (live, next);
    }
}
