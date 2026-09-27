using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RTUB.Application.Data;
using RTUB.Application.Interfaces.AfterHours;
using RTUB.Core.Entities;
using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;
using Xunit;

namespace RTUB.Integration.Tests.Application;

/// <summary>
/// AH-003 actions against the real SQLite schema: transactions, receipts, idempotency and
/// concurrency are database behaviour. The clock is frozen and the dice scripted, so energy never
/// regenerates mid-test and every outcome is known. Each test uses a fresh player.
/// </summary>
public class AfterHoursActionTests : IClassFixture<AfterHoursPlayFactory>
{
    private readonly AfterHoursPlayFactory _factory;

    public AfterHoursActionTests(AfterHoursPlayFactory factory)
    {
        _factory = factory;
        factory.Dice.Reset();
        factory.EnsureActiveCycleAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task Crime_Success_IsPersistedWithItsReceipt()
    {
        var user = await CreateUserAsync();

        var result = await Actions().CommitCrimeAsync(user, "C01", CrimeApproach.Standard, "k1");

        result.Accepted.Should().BeTrue();
        result.Replayed.Should().BeFalse();
        var state = await LoadStateAsync(user);
        (state.Energy, state.WalletCash, state.XP, state.Heat).Should().Be((230, 435L, 20L, 3));
        var receipt = (await ReceiptsAsync(user)).Should().ContainSingle().Subject;
        (receipt.IdempotencyKey, receipt.Request, receipt.Succeeded, receipt.WalletDelta).Should().Be(("k1", "crime:C01:Standard", true, 35L));
    }

    [Fact]
    public async Task Retry_SameKey_ReturnsTheSavedResult_WithoutRewardEnergyOrDiceTwice()
    {
        var user = await CreateUserAsync();

        var first = await Actions().CommitCrimeAsync(user, "C01", CrimeApproach.Bold, "same");
        var rollsAfterFirst = _factory.Dice.Calls;
        var second = await Actions().CommitCrimeAsync(user, "C01", CrimeApproach.Bold, "same");

        second.Replayed.Should().BeTrue();
        second.Receipt!.Id.Should().Be(first.Receipt!.Id);
        second.Receipt.Should().BeEquivalentTo(first.Receipt, o => o.Excluding(r => r.PlayerCycleState));
        _factory.Dice.Calls.Should().Be(rollsAfterFirst, "a replay never rolls again");
        var state = await LoadStateAsync(user);
        (state.Energy, state.WalletCash, state.XP).Should().Be((230, 444L, 22L));
        (await ReceiptsAsync(user)).Should().ContainSingle();
    }

    [Fact]
    public async Task FailedCrime_JailIsPersisted_BlocksCrimes_AndEndsByItself()
    {
        var user = await CreateUserAsync();
        _factory.Dice.Script(99, 1); // fail, then jailed

        var caught = await Actions().CommitCrimeAsync(user, "C01", CrimeApproach.Standard, "fail");

        caught.Receipt!.Succeeded.Should().BeFalse();
        caught.Receipt.Jailed.Should().BeTrue();
        var jailUntil = _factory.Now.AddMinutes(2);
        caught.Receipt.JailUntilUtc.Should().Be(jailUntil);
        (await LoadStateAsync(user)).JailUntilUtc.Should().Be(jailUntil);
        (await LoadStateAsync(user)).XP.Should().Be(5, "a failure pays 25% of 20 XP");

        var replay = await Actions().CommitCrimeAsync(user, "C01", CrimeApproach.Standard, "fail");
        replay.Receipt!.Jailed.Should().BeTrue("the replay returns the saved jail, it does not reroll");

        (await Actions().CommitCrimeAsync(user, "C01", CrimeApproach.Standard, "while-jailed")).Error.Should().Be("You are in jail.");

        _factory.Now = jailUntil;
        try
        {
            (await Actions().CommitCrimeAsync(user, "C01", CrimeApproach.Standard, "after-jail")).Accepted.Should().BeTrue();
        }
        finally
        {
            _factory.Now = AfterHoursPlayFactory.Start;
        }
    }

    [Fact]
    public async Task Key_ReusedForADifferentAction_IsRefused_AndChangesNothing()
    {
        var user = await CreateUserAsync();
        await Actions().DepositAsync(user, 100, "shared-key");

        var reuse = await Actions().WithdrawAsync(user, 50, "shared-key");

        reuse.Accepted.Should().BeFalse();
        var state = await LoadStateAsync(user);
        (state.WalletCash, state.BankCash).Should().Be((300L, 98L));
    }

    [Fact]
    public async Task ConcurrentCrimes_CannotOverspendEnergy_OrLoseUpdates()
    {
        var user = await CreateUserAsync();

        var results = await Parallel(30, i => Actions().CommitCrimeAsync(user, "C01", CrimeApproach.Standard, $"c{i}"));

        results.Count(r => r.Accepted).Should().Be(24, "240 energy buys exactly 24 crimes at 10");
        results.Where(r => !r.Accepted).Should().OnlyContain(r => r.Error == "Not enough energy.");
        var state = await LoadStateAsync(user);
        (state.Energy, state.WalletCash, state.XP, state.Heat).Should().Be((0, 400L + 24 * 35, 24 * 20L, 24 * 3));
        (await ReceiptsAsync(user)).Should().HaveCount(24);
    }

    [Fact]
    public async Task ConcurrentRetries_OfOneKey_ProduceOneAcceptedAction()
    {
        var user = await CreateUserAsync();
        var rollsBefore = _factory.Dice.Calls;

        var results = await Parallel(10, _ => Actions().CommitCrimeAsync(user, "C01", CrimeApproach.Careful, "one-key"));

        results.Should().OnlyContain(r => r.Accepted);
        results.Select(r => r.Receipt!.Id).Distinct().Should().ContainSingle();
        results.Count(r => !r.Replayed).Should().Be(1);
        (_factory.Dice.Calls - rollsBefore).Should().Be(1);
        (await LoadStateAsync(user)).WalletCash.Should().Be(428);
    }

    [Fact]
    public async Task Bank_DepositAndWithdraw_Atomically_WithTheFee()
    {
        var user = await CreateUserAsync();

        (await Actions().DepositAsync(user, 100, "d1")).Accepted.Should().BeTrue();
        (await Actions().WithdrawAsync(user, 50, "w1")).Accepted.Should().BeTrue();
        (await Actions().WithdrawAsync(user, 49, "w2")).Error.Should().Be("Not enough cash in the bank.");
        (await Actions().DepositAsync(user, 351, "d2")).Error.Should().Be("Not enough cash in your wallet.");

        var state = await LoadStateAsync(user);
        (state.WalletCash, state.BankCash).Should().Be((350L, 48L));
    }

    [Fact]
    public async Task ConcurrentBankOperations_NeverGoNegative()
    {
        var user = await CreateUserAsync();

        var deposits = await Parallel(10, i => Actions().DepositAsync(user, 100, $"d{i}"));
        deposits.Count(r => r.Accepted).Should().Be(4);
        var afterDeposits = await LoadStateAsync(user);
        (afterDeposits.WalletCash, afterDeposits.BankCash).Should().Be((0L, 392L));

        var withdrawals = await Parallel(10, i => Actions().WithdrawAsync(user, 100, $"w{i}"));
        withdrawals.Count(r => r.Accepted).Should().Be(3);
        var final = await LoadStateAsync(user);
        (final.WalletCash, final.BankCash).Should().Be((300L, 92L));
    }

    [Fact]
    public async Task MixedConcurrentActions_EndExactlyAtTheSumOfTheirReceipts()
    {
        var user = await CreateUserAsync();
        await SetHeatAsync(user, 60); // cover jobs repeatable

        var results = await Parallel(24, i => (i % 4) switch
        {
            0 => Actions().CommitCrimeAsync(user, "C01", CrimeApproach.Bold, $"m{i}"),
            1 => Actions().TakeCoverJobAsync(user, $"m{i}"),
            2 => Actions().DepositAsync(user, 30, $"m{i}"),
            _ => Actions().WithdrawAsync(user, 10, $"m{i}")
        });

        results.Should().Contain(r => r.Accepted);
        var receipts = await ReceiptsAsync(user);
        receipts.Should().HaveCount(results.Count(r => r.Accepted));
        var state = await LoadStateAsync(user);
        state.WalletCash.Should().Be(400 + receipts.Sum(r => r.WalletDelta));
        state.BankCash.Should().Be(receipts.Sum(r => r.BankDelta));
        state.XP.Should().Be(receipts.Sum(r => r.XpDelta));
        state.Energy.Should().Be(240 + receipts.Sum(r => r.EnergyDelta));
        state.Heat.Should().Be(60 + receipts.Sum(r => r.HeatDelta));
        state.WalletCash.Should().BeGreaterThanOrEqualTo(0);
        state.BankCash.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task CoverJob_DailyLimitHolds_AcrossRequests()
    {
        var user = await CreateUserAsync();

        (await Actions().TakeCoverJobAsync(user, "cover-1")).Accepted.Should().BeTrue();
        (await Actions().TakeCoverJobAsync(user, "cover-2")).Error.Should().Be("You already worked a cover job today.");

        var state = await LoadStateAsync(user);
        (state.Energy, state.WalletCash, state.XP, state.Heat).Should().Be((230, 420L, 12L, 0));
    }

    [Fact]
    public async Task Reads_ReconcileEnergy_WithoutWriting()
    {
        var user = await CreateUserAsync();
        await Actions().CommitCrimeAsync(user, "C01", CrimeApproach.Standard, "spend");

        _factory.Now = AfterHoursPlayFactory.Start.AddMinutes(13);
        try
        {
            using var scope = _factory.Services.CreateScope();
            var view = await scope.ServiceProvider.GetRequiredService<IPlayerCycleStateService>().GetOrCreateForActiveCycleAsync(user);

            view!.Energy.Should().Be(232);
            view.Heat.Should().Be(2);
            var stored = await LoadStateAsync(user);
            (stored.Energy, stored.Heat).Should().Be((230, 3), "a read never writes");
        }
        finally
        {
            _factory.Now = AfterHoursPlayFactory.Start;
        }
    }

    [Fact]
    public async Task Actions_LeaveFidelisMyTunoAndFinanceUntouched()
    {
        var user = await CreateUserAsync();
        var before = await SnapshotOutsideStateAsync(user);

        await Actions().CommitCrimeAsync(user, "C01", CrimeApproach.Standard, "x1");
        await Actions().DepositAsync(user, 100, "x2");
        await Actions().WithdrawAsync(user, 50, "x3");
        await Actions().TakeCoverJobAsync(user, "x4");

        (await SnapshotOutsideStateAsync(user)).Should().Be(before);
    }

    // ------------------------------------------------------------------ helpers

    private IAfterHoursActionService Actions() =>
        _factory.Services.CreateScope().ServiceProvider.GetRequiredService<IAfterHoursActionService>();

    private Task<ApplicationDbContext> DbAsync() =>
        _factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();

    private static Task<AfterHoursActionResult[]> Parallel(int count, Func<int, Task<AfterHoursActionResult>> action) =>
        Task.WhenAll(Enumerable.Range(0, count).Select(i => Task.Run(() => action(i))));

    private async Task<string> CreateUserAsync()
    {
        var name = $"ah3-{Guid.NewGuid():N}";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = name, Email = $"{name}@test.com", FirstName = "A", LastName = "H", Nickname = name };
        (await users.CreateAsync(user)).Succeeded.Should().BeTrue();
        return user.Id;
    }

    private async Task<PlayerCycleState> LoadStateAsync(string userId)
    {
        await using var db = await DbAsync();
        return await db.AfterHoursPlayerCycleStates.AsNoTracking()
            .SingleAsync(s => s.UserId == userId && s.GameCycle!.Status == GameCycleStatus.Active);
    }

    private async Task<List<PlayerActionReceipt>> ReceiptsAsync(string userId)
    {
        await using var db = await DbAsync();
        return await db.AfterHoursPlayerActionReceipts.AsNoTracking()
            .Where(r => r.PlayerCycleState!.UserId == userId).ToListAsync();
    }

    private async Task SetHeatAsync(string userId, int heat)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPlayerCycleStateService>().GetOrCreateForActiveCycleAsync(userId);
        await using var db = await DbAsync();
        var state = await db.AfterHoursPlayerCycleStates.SingleAsync(s => s.UserId == userId);
        state.Heat = heat;
        state.HeatUpdatedAtUtc = AfterHoursPlayFactory.Start;
        await db.SaveChangesAsync();
    }

    private async Task<(decimal, int, int, int)> SnapshotOutsideStateAsync(string userId)
    {
        await using var db = await DbAsync();
        return (await db.Users.Where(u => u.Id == userId).Select(u => u.FidelisBalance).SingleAsync(),
            await db.Characters.CountAsync(),
            await db.Transactions.CountAsync(),
            await db.GameScores.CountAsync());
    }
}

/// <summary>The shared test host with a settable clock and scripted After Hours dice.</summary>
public class AfterHoursPlayFactory : TestWebApplicationFactory
{
    /// <summary>Whole seconds, well inside the test cycle.</summary>
    public static readonly DateTime Start =
        new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, 12, 0, 0, DateTimeKind.Utc);

    private readonly ManualClock _clock = new() { UtcNow = Start };
    private readonly SemaphoreSlim _cycleLock = new(1, 1);

    public ScriptedDice Dice { get; } = new();

    public DateTime Now
    {
        get => _clock.UtcNow;
        set => _clock.UtcNow = value;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(_clock);
            services.RemoveAll<IAfterHoursDice>();
            services.AddSingleton<IAfterHoursDice>(Dice);
        });
    }

    public async Task EnsureActiveCycleAsync()
    {
        await _cycleLock.WaitAsync();
        try
        {
            using var scope = Services.CreateScope();
            var cycles = scope.ServiceProvider.GetRequiredService<IGameCycleService>();
            if (await cycles.GetActiveCycleAsync() is not null) return;

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var fiscalYear = FiscalYear.Create(2800, 2801);
            db.FiscalYears.Add(fiscalYear);
            await db.SaveChangesAsync();
            var cycle = await cycles.CreateCycleAsync(fiscalYear.Id, GameCycleKind.Pilot, Start.AddDays(-1), Start.AddDays(60));
            await cycles.ActivateAsync(cycle.Id);
        }
        finally
        {
            _cycleLock.Release();
        }
    }

    public sealed class ManualClock : TimeProvider
    {
        private long _ticks;

        public DateTime UtcNow
        {
            get => new(Interlocked.Read(ref _ticks), DateTimeKind.Utc);
            set => Interlocked.Exchange(ref _ticks, value.Ticks);
        }

        public override DateTimeOffset GetUtcNow() => new(UtcNow);
    }

    /// <summary>Rolls scripted values first, then 1 (always succeeds, never jails… unless scripted). Counts every roll.</summary>
    public sealed class ScriptedDice : IAfterHoursDice
    {
        private readonly ConcurrentQueue<int> _script = new();
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public void Script(params int[] rolls)
        {
            foreach (var roll in rolls) _script.Enqueue(roll);
        }

        public void Reset() => _script.Clear();

        public int RollPercent()
        {
            Interlocked.Increment(ref _calls);
            return _script.TryDequeue(out var roll) ? roll : 1;
        }
    }
}
