using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Application.Data;
using RTUB.Application.Interfaces.AfterHours;
using RTUB.Core.Entities;
using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;
using Xunit;

namespace RTUB.Integration.Tests.Application;

/// <summary>
/// After Hours cycles and per-cycle player state against the real SQLite schema of the test host:
/// the unique indexes, check constraints and UTC round-trip are database behaviour, so they are
/// proven here rather than against the InMemory provider. Tests in this class share one database
/// and run sequentially; each one that needs an active cycle starts its own.
/// </summary>
public class AfterHoursCycleStateTests : IntegrationTestBase
{
    private static int _nextYear = 3000;

    public AfterHoursCycleStateTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Cycle_ReferencesAnExistingFiscalYear()
    {
        var fiscalYear = await CreateFiscalYearAsync();

        var cycle = await Cycles().CreateCycleAsync(fiscalYear.Id, GameCycleKind.Live, Now.AddDays(-1), Now.AddDays(1));

        await using var db = await DbAsync();
        var stored = await db.AfterHoursGameCycles.Include(c => c.FiscalYear).SingleAsync(c => c.Id == cycle.Id);
        stored.FiscalYear!.StartYear.Should().Be(fiscalYear.StartYear);
        stored.Status.Should().Be(GameCycleStatus.Scheduled);
    }

    [Fact]
    public async Task PilotAndLive_AreStoredDistinctly_AndCoexistInOneFiscalYear()
    {
        var fiscalYear = await CreateFiscalYearAsync();

        var pilot = await Cycles().CreateCycleAsync(fiscalYear.Id, GameCycleKind.Pilot, Now, Now.AddDays(10));
        var live = await Cycles().CreateCycleAsync(fiscalYear.Id, GameCycleKind.Live, Now.AddDays(10), Now.AddDays(300));
        var secondPilot = await Cycles().CreateCycleAsync(fiscalYear.Id, GameCycleKind.Pilot, Now.AddDays(-30), Now.AddDays(-20));

        await using var db = await DbAsync();
        var kinds = await db.AfterHoursGameCycles
            .Where(c => c.FiscalYearId == fiscalYear.Id)
            .ToDictionaryAsync(c => c.Id, c => c.Kind);
        kinds.Should().HaveCount(3);
        kinds[pilot.Id].Should().Be(GameCycleKind.Pilot);
        kinds[live.Id].Should().Be(GameCycleKind.Live);
        kinds[secondPilot.Id].Should().Be(GameCycleKind.Pilot);
    }

    [Fact]
    public async Task UtcBoundaries_RoundTripExactly_AsUtc()
    {
        var fiscalYear = await CreateFiscalYearAsync();
        var start = new DateTime(2026, 8, 31, 23, 0, 0, 123, DateTimeKind.Utc); // 00:00 Lisbon, 1 Sept
        var end = new DateTime(2027, 8, 31, 22, 59, 59, 999, DateTimeKind.Utc);

        var cycle = await Cycles().CreateCycleAsync(fiscalYear.Id, GameCycleKind.Live, start, end);

        await using var db = await DbAsync();
        var stored = await db.AfterHoursGameCycles.AsNoTracking().SingleAsync(c => c.Id == cycle.Id);
        stored.StartUtc.Should().Be(start);
        stored.EndUtc.Should().Be(end);
        stored.StartUtc.Kind.Should().Be(DateTimeKind.Utc);
        stored.EndUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task InvalidBoundaries_AreRejected_ByTheServiceAndByTheDatabase()
    {
        var fiscalYear = await CreateFiscalYearAsync();
        var now = Now;

        await FluentActions.Invoking(() => Cycles().CreateCycleAsync(fiscalYear.Id, GameCycleKind.Live, now, now))
            .Should().ThrowAsync<ArgumentException>();

        // Bypassing the domain factory still cannot store it: CK_AfterHoursGameCycles_EndAfterStart.
        await using var db = await DbAsync();
        db.AfterHoursGameCycles.Add(new GameCycle
        {
            FiscalYearId = fiscalYear.Id,
            Kind = GameCycleKind.Live,
            Status = GameCycleStatus.Scheduled,
            StartUtc = Now,
            EndUtc = Now.AddDays(-1)
        });
        await FluentActions.Invoking(() => db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task OnlyOneCycle_CanBeActive()
    {
        var first = await StartActiveCycleAsync();
        var fiscalYear = await CreateFiscalYearAsync();
        var second = await Cycles().CreateCycleAsync(fiscalYear.Id, GameCycleKind.Live, Now.AddDays(-1), Now.AddDays(1));

        await FluentActions.Invoking(() => Cycles().ActivateAsync(second.Id))
            .Should().ThrowAsync<InvalidOperationException>();

        // And not by writing the column directly either: IX_AfterHoursGameCycles_SingleActive.
        await using var db = await DbAsync();
        var tracked = await db.AfterHoursGameCycles.SingleAsync(c => c.Id == second.Id);
        tracked.Status = GameCycleStatus.Active;
        await FluentActions.Invoking(() => db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();

        (await Cycles().GetActiveCycleAsync())!.Id.Should().Be(first.Id);
    }

    [Fact]
    public async Task CycleAndUser_AreUnique_AtTheDatabase()
    {
        var cycle = await StartActiveCycleAsync();
        var user = await CreateUserAsync();

        await using var db = await DbAsync();
        db.AfterHoursPlayerCycleStates.Add(PlayerCycleState.CreateInitial(cycle.Id, user.Id, Now));
        db.AfterHoursPlayerCycleStates.Add(PlayerCycleState.CreateInitial(cycle.Id, user.Id, Now));

        await FluentActions.Invoking(() => db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task NewPlayerState_IsPersistedWithExactlyTheStartingValues()
    {
        var cycle = await StartActiveCycleAsync();
        var user = await CreateUserAsync();

        var created = await States().GetOrCreateForActiveCycleAsync(user.Id);

        await using var db = await DbAsync();
        var stored = await db.AfterHoursPlayerCycleStates.AsNoTracking().SingleAsync(s => s.Id == created!.Id);
        stored.GameCycleId.Should().Be(cycle.Id);
        stored.UserId.Should().Be(user.Id);
        stored.Level.Should().Be(1);
        stored.XP.Should().Be(0);
        stored.WalletCash.Should().Be(400);
        stored.BankCash.Should().Be(0);
        stored.MaxEnergy.Should().Be(240);
        stored.Energy.Should().Be(240);
        stored.Heat.Should().Be(0);
        stored.Toughness.Should().Be(4);
        stored.Stealth.Should().Be(4);
        stored.Smarts.Should().Be(4);
        stored.Charisma.Should().Be(4);
        stored.EnergyUpdatedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        stored.EnergyUpdatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        stored.HeatUpdatedAtUtc.Should().Be(stored.EnergyUpdatedAtUtc);
    }

    [Fact]
    public async Task GetOrCreate_Twice_ReturnsTheSameState()
    {
        await StartActiveCycleAsync();
        var user = await CreateUserAsync();

        var first = await States().GetOrCreateForActiveCycleAsync(user.Id);
        var second = await States().GetOrCreateForActiveCycleAsync(user.Id);

        second!.Id.Should().Be(first!.Id);
        (await CountStatesAsync(user.Id)).Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentFirstCalls_CreateExactlyOneState()
    {
        var cycle = await StartActiveCycleAsync();
        var user = await CreateUserAsync();

        // Separate scopes, as separate tabs or devices would have.
        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => States().GetOrCreateForActiveCycleAsync(user.Id))));

        results.Should().OnlyContain(s => s != null && s.GameCycleId == cycle.Id);
        results.Select(s => s!.Id).Distinct().Should().ContainSingle();
        (await CountStatesAsync(user.Id)).Should().Be(1);
    }

    [Fact]
    public async Task DifferentCycles_GiveTheSameUser_SeparateStates()
    {
        var firstCycle = await StartActiveCycleAsync(GameCycleKind.Pilot);
        var user = await CreateUserAsync();
        var inFirst = await States().GetOrCreateForActiveCycleAsync(user.Id);

        var secondCycle = await StartActiveCycleAsync(GameCycleKind.Live);
        var inSecond = await States().GetOrCreateForActiveCycleAsync(user.Id);

        inFirst!.GameCycleId.Should().Be(firstCycle.Id);
        inSecond!.GameCycleId.Should().Be(secondCycle.Id);
        inSecond.Id.Should().NotBe(inFirst.Id);
        (await CountStatesAsync(user.Id)).Should().Be(2, "the finished cycle's state is kept as history");
    }

    [Fact]
    public async Task CreatingState_LeavesFidelisMyTunoAndFinanceUntouched()
    {
        await StartActiveCycleAsync();
        var user = await CreateUserAsync();
        var before = await SnapshotOutsideStateAsync(user.Id);

        await States().GetOrCreateForActiveCycleAsync(user.Id);

        (await SnapshotOutsideStateAsync(user.Id)).Should().Be(before);
    }

    [Fact]
    public async Task NoActiveCycle_ReturnsNull_AndCreatesNothing()
    {
        await FinishActiveCycleAsync();
        var user = await CreateUserAsync();
        var cyclesBefore = await CountCyclesAsync();

        var state = await States().GetOrCreateForActiveCycleAsync(user.Id);

        state.Should().BeNull();
        (await CountCyclesAsync()).Should().Be(cyclesBefore);
        (await CountStatesAsync(user.Id)).Should().Be(0);
    }

    [Fact]
    public async Task ActiveCycleOutsideItsBoundaries_IsNotPlayable()
    {
        await FinishActiveCycleAsync();
        var fiscalYear = await CreateFiscalYearAsync();
        var expired = await Cycles().CreateCycleAsync(fiscalYear.Id, GameCycleKind.Live, Now.AddDays(-10), Now.AddDays(-1));
        await Cycles().ActivateAsync(expired.Id);
        var user = await CreateUserAsync();

        (await Cycles().GetActiveCycleAsync()).Should().BeNull();
        (await States().GetOrCreateForActiveCycleAsync(user.Id)).Should().BeNull();
        (await CountStatesAsync(user.Id)).Should().Be(0);
    }

    // ------------------------------------------------------------------ helpers

    private static DateTime Now => DateTime.UtcNow;

    private IGameCycleService Cycles() =>
        Factory.Services.CreateScope().ServiceProvider.GetRequiredService<IGameCycleService>();

    private IPlayerCycleStateService States() =>
        Factory.Services.CreateScope().ServiceProvider.GetRequiredService<IPlayerCycleStateService>();

    private Task<ApplicationDbContext> DbAsync() =>
        Factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();

    private async Task<FiscalYear> CreateFiscalYearAsync()
    {
        var start = Interlocked.Increment(ref _nextYear);
        var fiscalYear = FiscalYear.Create(start, start + 1);
        await using var db = await DbAsync();
        db.FiscalYears.Add(fiscalYear);
        await db.SaveChangesAsync();
        return fiscalYear;
    }

    private async Task FinishActiveCycleAsync()
    {
        await using var db = await DbAsync();
        // Test isolation only (there is no unarchived finish in the application any more).
        await db.AfterHoursGameCycles.Where(c => c.Status == GameCycleStatus.Active).ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, GameCycleStatus.Finished));
    }

    /// <summary>Finishes whatever is active, then starts a new cycle around now.</summary>
    private async Task<GameCycle> StartActiveCycleAsync(GameCycleKind kind = GameCycleKind.Live)
    {
        await FinishActiveCycleAsync();
        var fiscalYear = await CreateFiscalYearAsync();
        var cycle = await Cycles().CreateCycleAsync(fiscalYear.Id, kind, Now.AddDays(-1), Now.AddDays(30));
        await Cycles().ActivateAsync(cycle.Id);
        return cycle;
    }

    private async Task<ApplicationUser> CreateUserAsync()
    {
        var name = $"ah-{Guid.NewGuid():N}";
        var user = new ApplicationUser
        {
            UserName = name,
            Email = $"{name}@test.com",
            FirstName = "After",
            LastName = "Hours",
            Nickname = name
        };
        using var scope = Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        (await userManager.CreateAsync(user)).Succeeded.Should().BeTrue();
        return user;
    }

    private async Task<int> CountStatesAsync(string userId)
    {
        await using var db = await DbAsync();
        return await db.AfterHoursPlayerCycleStates.CountAsync(s => s.UserId == userId);
    }

    private async Task<int> CountCyclesAsync()
    {
        await using var db = await DbAsync();
        return await db.AfterHoursGameCycles.CountAsync();
    }

    private async Task<(decimal Fidelis, int Characters, int Transactions, int GameScores, int AuditLogs)> SnapshotOutsideStateAsync(string userId)
    {
        await using var db = await DbAsync();
        var fidelis = await db.Users.Where(u => u.Id == userId).Select(u => u.FidelisBalance).SingleAsync();
        return (fidelis,
            await db.Characters.CountAsync(),
            await db.Transactions.CountAsync(),
            await db.GameScores.CountAsync(),
            await db.AuditLogs.CountAsync());
    }
}
