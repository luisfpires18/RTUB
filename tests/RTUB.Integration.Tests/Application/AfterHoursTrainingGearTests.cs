using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Application.Data;
using RTUB.Application.Interfaces.AfterHours;
using RTUB.Core.Entities;
using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;
using RTUB.Core.Helpers.AfterHours;
using Xunit;

namespace RTUB.Integration.Tests.Application;

/// <summary>
/// AH-005 training and gear against the real SQLite schema, with the AH-003 frozen clock
/// (12:00 UTC, mid-day in Lisbon). Each test uses a fresh player.
/// </summary>
public class AfterHoursTrainingGearTests : IClassFixture<AfterHoursPlayFactory>
{
    private readonly AfterHoursPlayFactory _factory;

    public AfterHoursTrainingGearTests(AfterHoursPlayFactory factory)
    {
        _factory = factory;
        factory.Now = AfterHoursPlayFactory.Start;
        factory.EnsureActiveCycleAsync().GetAwaiter().GetResult();
    }

    // ------------------------------------------------------------------ training

    [Fact]
    public async Task NewPlayer_ReadsOnePoint_AndThePointsGrowDaily()
    {
        var user = await CreateUserAsync();

        (await ViewAsync(user)).TrainingPoints.Should().Be(1, "no backlog on first login");

        try
        {
            _factory.Now = AfterHoursPlayFactory.Start.AddDays(5);
            (await ViewAsync(user)).TrainingPoints.Should().Be(3);
        }
        finally
        {
            _factory.Now = AfterHoursPlayFactory.Start;
        }
    }

    [Fact]
    public async Task Train_Persists_AndRetryIsIdempotent()
    {
        var user = await CreateUserAsync();
        await UpdateStateAsync(user, s => { s.XP = AfterHoursLevels.XpForLevel(4); s.Level = 4; });

        var first = await Actions().TrainSkillAsync(user, PlayerSkill.Charisma, "t1");
        var replay = await Actions().TrainSkillAsync(user, PlayerSkill.Charisma, "t1");

        first.Accepted.Should().BeTrue();
        replay.Replayed.Should().BeTrue();
        replay.Receipt!.Id.Should().Be(first.Receipt!.Id);
        var state = await StateAsync(user);
        (state.Charisma, state.TrainingPoints, state.Energy, state.WalletCash).Should().Be((5, 0, 220, 280L));

        (await Actions().TrainSkillAsync(user, PlayerSkill.Charisma, "t2")).Error.Should().StartWith("No training points");
    }

    [Fact]
    public async Task ConcurrentTraining_CannotDoubleSpendPoints()
    {
        var user = await CreateUserAsync();
        await UpdateStateAsync(user, s =>
        {
            s.XP = AfterHoursLevels.XpForLevel(12);
            s.Level = 12;
            s.WalletCash = 5_000;
            s.TrainingPoints = 3;
            s.TrainingPointsDay = LisbonCalendar.DateOf(AfterHoursPlayFactory.Start);
        });

        var results = await Parallel(10, i => Actions().TrainSkillAsync(user, PlayerSkill.Stealth, $"ct{i}"));

        results.Count(r => r.Accepted).Should().Be(3);
        var state = await StateAsync(user);
        (state.Stealth, state.TrainingPoints, state.Energy, state.WalletCash)
            .Should().Be((7, 0, 180, 5_000L - 120 - 160 - 200));
    }

    // ------------------------------------------------------------------ gear

    [Fact]
    public async Task Purchase_IsIdempotent_AndOnlyOnce()
    {
        var user = await CreateUserAsync();
        await UpdateStateAsync(user, s => { s.XP = AfterHoursLevels.XpForLevel(3); s.Level = 3; s.WalletCash = 1_000; });

        var bought = await Actions().PurchaseGearAsync(user, "O1", "b1");
        var replay = await Actions().PurchaseGearAsync(user, "O1", "b1");
        var again = await Actions().PurchaseGearAsync(user, "O1", "b2");

        bought.Accepted.Should().BeTrue();
        replay.Replayed.Should().BeTrue();
        again.Error.Should().Be("You already own this.");
        var state = await StateAsync(user);
        (state.WalletCash, state.EquippedOutfitKey).Should().Be((500L, "O1"));
        (await GearAsync(user)).Should().ContainSingle().Which.ItemKey.Should().Be("O1");
    }

    [Fact]
    public async Task GearRows_AreUniquePerStateAndItem()
    {
        var user = await CreateUserAsync();
        var stateId = (await ViewAsync(user)).Id;

        await using var db = await DbAsync();
        db.AfterHoursPlayerGear.Add(new PlayerGear { PlayerCycleStateId = stateId, ItemKey = "W1", Slot = GearSlot.Weapon, Tier = 1 });
        db.AfterHoursPlayerGear.Add(new PlayerGear { PlayerCycleStateId = stateId, ItemKey = "W1", Slot = GearSlot.Weapon, Tier = 1 });
        await FluentActions.Invoking(() => db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task ConcurrentPurchasesOfOneItem_ChargeOnce()
    {
        var user = await CreateUserAsync();
        await UpdateStateAsync(user, s => { s.XP = AfterHoursLevels.XpForLevel(3); s.Level = 3; s.WalletCash = 5_000; });

        var results = await Parallel(10, i => Actions().PurchaseGearAsync(user, "V1", $"cp{i}"));

        results.Count(r => r.Accepted).Should().Be(1);
        (await StateAsync(user)).WalletCash.Should().Be(4_400);
        (await GearAsync(user)).Should().ContainSingle();
    }

    [Fact]
    public async Task ConcurrentPurchasesAndEquips_StayConsistent()
    {
        var user = await CreateUserAsync();
        await UpdateStateAsync(user, s => { s.XP = AfterHoursLevels.XpForLevel(10); s.Level = 10; s.WalletCash = 6_000; });

        var purchases = await Parallel(6, i => Actions().PurchaseGearAsync(user, new[] { "W1", "W2", "O1", "O2", "V1", "V2" }[i], $"cb{i}"));
        // Which of the six fit in 6,000 depends on arrival order; the books must balance either way.
        purchases.Should().OnlyContain(r => r.Accepted || r.Error == "Not enough cash in your wallet.");
        var afterBuy = await StateAsync(user);
        var gear = await GearAsync(user);
        afterBuy.WalletCash.Should().Be(6_000 - gear.Sum(g => GearCatalogue.Find(g.ItemKey)!.Price));
        afterBuy.WalletCash.Should().BeGreaterThanOrEqualTo(0);

        var weapons = gear.Where(g => g.Slot == GearSlot.Weapon).Select(g => g.ItemKey).ToList();
        var equips = await Parallel(8, i => Actions().EquipGearAsync(user, weapons[i % weapons.Count], $"ce{i}"));
        equips.Should().OnlyContain(r => r.Accepted || r.Error == "Already equipped.");
        var final = await StateAsync(user);
        weapons.Should().Contain(final.EquippedWeaponKey);
        final.WalletCash.Should().Be(afterBuy.WalletCash, "equipping costs nothing");
    }

    [Fact]
    public async Task TrainingAndGear_LeaveMyTunoAndFinanceUntouched()
    {
        var user = await CreateUserAsync();
        await UpdateStateAsync(user, s => { s.XP = AfterHoursLevels.XpForLevel(4); s.Level = 4; s.WalletCash = 2_000; });
        var before = await SnapshotOutsideStateAsync(user);

        await Actions().TrainSkillAsync(user, PlayerSkill.Toughness, "o1");
        await Actions().PurchaseGearAsync(user, "W1", "o2");
        await Actions().UnequipGearAsync(user, "W1", "o3");
        await Actions().EquipGearAsync(user, "W1", "o4");

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
        var name = $"ah5-{Guid.NewGuid():N}";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = name, Email = $"{name}@test.com", FirstName = "A", LastName = "H", Nickname = name };
        (await users.CreateAsync(user)).Succeeded.Should().BeTrue();
        return user.Id;
    }

    private async Task<PlayerCycleState> ViewAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<IPlayerCycleStateService>().GetOrCreateForActiveCycleAsync(userId))!;
    }

    private async Task UpdateStateAsync(string userId, Action<PlayerCycleState> change)
    {
        var id = (await ViewAsync(userId)).Id;
        await using var db = await DbAsync();
        var state = await db.AfterHoursPlayerCycleStates.SingleAsync(s => s.Id == id);
        change(state);
        await db.SaveChangesAsync();
    }

    private async Task<PlayerCycleState> StateAsync(string userId)
    {
        await using var db = await DbAsync();
        return await db.AfterHoursPlayerCycleStates.AsNoTracking()
            .SingleAsync(s => s.UserId == userId && s.GameCycle!.Status == GameCycleStatus.Active);
    }

    private async Task<List<PlayerGear>> GearAsync(string userId)
    {
        await using var db = await DbAsync();
        return await db.AfterHoursPlayerGear
            .Join(db.AfterHoursPlayerCycleStates.Where(s => s.UserId == userId), g => g.PlayerCycleStateId, s => s.Id, (g, _) => g)
            .ToListAsync();
    }

    private async Task<(decimal, int, int, int, int)> SnapshotOutsideStateAsync(string userId)
    {
        await using var db = await DbAsync();
        return (await db.Users.Where(u => u.Id == userId).Select(u => u.FidelisBalance).SingleAsync(),
            await db.Characters.CountAsync(),
            await db.InventoryItems.CountAsync(),
            await db.ForgedWeapons.CountAsync(),
            await db.Transactions.CountAsync());
    }
}
