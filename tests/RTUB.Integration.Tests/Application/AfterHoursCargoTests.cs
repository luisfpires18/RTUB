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
/// AH-004 cargo, fence and buyer contracts against the real SQLite schema, with the AH-003 frozen
/// clock and scripted dice. The clock starts at 12:00 UTC, the first instant of a rotation window.
/// </summary>
public class AfterHoursCargoTests : IClassFixture<AfterHoursPlayFactory>
{
    private readonly AfterHoursPlayFactory _factory;

    public AfterHoursCargoTests(AfterHoursPlayFactory factory)
    {
        _factory = factory;
        factory.Dice.Reset();
        factory.Now = AfterHoursPlayFactory.Start;
        factory.EnsureActiveCycleAsync().GetAwaiter().GetResult();
    }

    // ------------------------------------------------------------------ crime cargo

    [Fact]
    public async Task CrimeCargo_IsAwardedOnSuccessOnly_AndNeverTwicePerKey()
    {
        var user = await CreateUserAsync();

        await Actions().CommitCrimeAsync(user, "C01", CrimeApproach.Standard, "crime-1");
        var replay = await Actions().CommitCrimeAsync(user, "C01", CrimeApproach.Standard, "crime-1");
        replay.Replayed.Should().BeTrue();
        (await CargoAsync(user, CargoType.Phone)).Should().Be(1);

        _factory.Dice.Script(100, 100); // fail, no jail
        var failed = await Actions().CommitCrimeAsync(user, "C01", CrimeApproach.Standard, "crime-2");
        failed.Receipt!.Succeeded.Should().BeFalse();
        (await CargoAsync(user, CargoType.Phone)).Should().Be(1);

        await Actions().CommitCrimeAsync(user, "C01", CrimeApproach.Standard, "crime-3");
        (await CargoAsync(user, CargoType.Phone)).Should().Be(2);
    }

    // ------------------------------------------------------------------ persistence

    [Fact]
    public async Task CargoRows_AreUniquePerStateAndType_AndNeverNegative()
    {
        var user = await CreateUserAsync();
        var stateId = await StateIdAsync(user);

        await using (var db = await DbAsync())
        {
            db.AfterHoursPlayerCargo.Add(new PlayerCargo { PlayerCycleStateId = stateId, CargoType = CargoType.Spirits, Quantity = 1 });
            db.AfterHoursPlayerCargo.Add(new PlayerCargo { PlayerCycleStateId = stateId, CargoType = CargoType.Spirits, Quantity = 1 });
            await FluentActions.Invoking(() => db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
        }

        await using (var db = await DbAsync())
        {
            db.AfterHoursPlayerCargo.Add(new PlayerCargo { PlayerCycleStateId = stateId, CargoType = CargoType.ArtPiece, Quantity = -1 });
            await FluentActions.Invoking(() => db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
        }
    }

    // ------------------------------------------------------------------ fence

    [Fact]
    public async Task Fence_SellsPartOfTheStack_AtTheBasePrice_Idempotently()
    {
        var user = await CreateUserAsync();
        await GiveCargoAsync(user, CargoType.Electronics, 5);

        var sale = await Actions().SellToFenceAsync(user, CargoType.Electronics, 3, "sell-1");
        var replay = await Actions().SellToFenceAsync(user, CargoType.Electronics, 3, "sell-1");

        sale.Receipt!.WalletDelta.Should().Be(90);
        replay.Replayed.Should().BeTrue();
        replay.Receipt!.Id.Should().Be(sale.Receipt.Id);
        (await CargoAsync(user, CargoType.Electronics)).Should().Be(2);
        (await StateAsync(user)).WalletCash.Should().Be(490);

        (await Actions().SellToFenceAsync(user, CargoType.Electronics, 3, "sell-2")).Error.Should().Be("You don't have that much.");
        (await Actions().SellToFenceAsync(user, CargoType.Electronics, 0, "sell-3")).Error.Should().NotBeNull();
        (await Actions().SellToFenceAsync(user, CargoType.Electronics, -2, "sell-4")).Error.Should().NotBeNull();
        (await CargoAsync(user, CargoType.Electronics)).Should().Be(2);
    }

    [Fact]
    public async Task ConcurrentFenceSales_CannotSellTheSameCargoTwice()
    {
        var user = await CreateUserAsync();
        await GiveCargoAsync(user, CargoType.Phone, 5);

        var results = await Parallel(12, i => Actions().SellToFenceAsync(user, CargoType.Phone, 1, $"p{i}"));

        results.Count(r => r.Accepted).Should().Be(5);
        (await CargoAsync(user, CargoType.Phone)).Should().Be(0);
        (await StateAsync(user)).WalletCash.Should().Be(400 + 5 * 15);
    }

    [Fact]
    public async Task ConcurrentCrimesAndSales_KeepCargoAndCashConsistent()
    {
        var user = await CreateUserAsync();
        await GiveCargoAsync(user, CargoType.Phone, 3);

        var results = await Parallel(20, i => i % 2 == 0
            ? Actions().CommitCrimeAsync(user, "C01", CrimeApproach.Standard, $"mix{i}")
            : Actions().SellToFenceAsync(user, CargoType.Phone, 1, $"mix{i}"));

        var receipts = await ReceiptsAsync(user);
        receipts.Should().HaveCount(results.Count(r => r.Accepted));
        (await CargoAsync(user, CargoType.Phone)).Should().Be(3 + receipts.Where(r => r.CargoType == CargoType.Phone).Sum(r => r.CargoDelta));
        (await StateAsync(user)).WalletCash.Should().Be(400 + receipts.Sum(r => r.WalletDelta));
    }

    // ------------------------------------------------------------------ rotation

    [Fact]
    public async Task Rotation_SameWindow_SameThreeContracts()
    {
        var user = await CreateUserAsync();

        var first = await Contracts().GetCurrentContractsAsync(user);
        var second = await Contracts().GetCurrentContractsAsync(user);

        first.Should().HaveCount(3);
        second.Select(c => c.Contract.Id).Should().Equal(first.Select(c => c.Contract.Id));
        first.Should().OnlyContain(c => c.Contract.AvailableFromUtc == AfterHoursPlayFactory.Start
            && c.Contract.ExpiresAtUtc == AfterHoursPlayFactory.Start.AddHours(12)
            && c.Contract.ExpiresAtUtc.Kind == DateTimeKind.Utc);
    }

    [Fact]
    public async Task Rotation_BoundaryIsUtc_AndTheNextWindowRotates()
    {
        var user = await CreateUserAsync();
        var current = await Contracts().GetCurrentContractsAsync(user);

        try
        {
            _factory.Now = AfterHoursPlayFactory.Start.AddHours(12).AddTicks(-1);
            (await Contracts().GetCurrentContractsAsync(user)).Select(c => c.Contract.Id)
                .Should().Equal(current.Select(c => c.Contract.Id));

            _factory.Now = AfterHoursPlayFactory.Start.AddHours(12);
            var next = await Contracts().GetCurrentContractsAsync(user);
            next.Should().HaveCount(3);
            next.Select(c => c.Contract.Id).Should().NotIntersectWith(current.Select(c => c.Contract.Id));
            next.Select(c => c.Contract.TemplateKey).Should().NotEqual(current.Select(c => c.Contract.TemplateKey));
        }
        finally
        {
            _factory.Now = AfterHoursPlayFactory.Start;
        }
    }

    [Fact]
    public async Task Rotation_ConcurrentFirstVisits_CreateOneRotation()
    {
        var user = await CreateUserAsync();
        var window = AfterHoursPlayFactory.Start.AddDays(3);
        try
        {
            _factory.Now = window.AddMinutes(1);
            var results = await Task.WhenAll(Enumerable.Range(0, 10)
                .Select(_ => Task.Run(() => Contracts().GetCurrentContractsAsync(user))));

            results.Should().OnlyContain(r => r.Count == 3);
            results.Select(r => string.Join(",", r.Select(c => c.Contract.Id))).Distinct().Should().ContainSingle();
            await using var db = await DbAsync();
            (await db.AfterHoursBuyerContracts.CountAsync(c => c.RotationStartUtc == window)).Should().Be(3);
        }
        finally
        {
            _factory.Now = AfterHoursPlayFactory.Start;
        }
    }

    // ------------------------------------------------------------------ completion

    [Fact]
    public async Task Delivery_TakesExactCargo_PaysCashAndXp_Once()
    {
        var user = await CreateUserAsync();
        var contract = (await Contracts().GetCurrentContractsAsync(user))[0].Contract;
        await GiveCargoAsync(user, contract.CargoType, contract.Quantity + 1);

        var delivered = await Actions().DeliverContractAsync(user, contract.Id, "deliver-1");
        var replay = await Actions().DeliverContractAsync(user, contract.Id, "deliver-1");
        var again = await Actions().DeliverContractAsync(user, contract.Id, "deliver-2");

        delivered.Accepted.Should().BeTrue();
        replay.Replayed.Should().BeTrue();
        replay.Receipt!.Id.Should().Be(delivered.Receipt!.Id);
        again.Error.Should().Be("You already delivered this contract.");

        var state = await StateAsync(user);
        (await CargoAsync(user, contract.CargoType)).Should().Be(1);
        state.WalletCash.Should().Be(400 + contract.CashReward);
        state.XP.Should().Be(contract.XpReward);
        state.Level.Should().Be(AfterHoursLevels.LevelForXp(contract.XpReward));
        (await Contracts().GetCurrentContractsAsync(user)).Single(c => c.Contract.Id == contract.Id).Completed.Should().BeTrue();
        await using var db = await DbAsync();
        (await db.AfterHoursBuyerContractCompletions.CountAsync(c => c.BuyerContractId == contract.Id && c.PlayerCycleStateId == state.Id))
            .Should().Be(1);
    }

    [Fact]
    public async Task Delivery_WithoutEnoughCargo_IsRefused()
    {
        var user = await CreateUserAsync();
        var contract = (await Contracts().GetCurrentContractsAsync(user))[1].Contract;
        await GiveCargoAsync(user, contract.CargoType, contract.Quantity - 1);

        (await Actions().DeliverContractAsync(user, contract.Id, "short")).Accepted.Should().BeFalse();
        (await CargoAsync(user, contract.CargoType)).Should().Be(contract.Quantity - 1);
        (await StateAsync(user)).WalletCash.Should().Be(400);
    }

    [Fact]
    public async Task ConcurrentDeliveries_PayOnce()
    {
        var user = await CreateUserAsync();
        var contract = (await Contracts().GetCurrentContractsAsync(user))[2].Contract;
        await GiveCargoAsync(user, contract.CargoType, contract.Quantity * 3);

        var results = await Parallel(10, i => Actions().DeliverContractAsync(user, contract.Id, $"cd{i}"));

        results.Count(r => r.Accepted).Should().Be(1);
        (await CargoAsync(user, contract.CargoType)).Should().Be(contract.Quantity * 2);
        (await StateAsync(user)).WalletCash.Should().Be(400 + contract.CashReward);
    }

    [Fact]
    public async Task Delivery_AfterExpiry_IsRefused()
    {
        var user = await CreateUserAsync();
        var contract = (await Contracts().GetCurrentContractsAsync(user))[0].Contract;
        await GiveCargoAsync(user, contract.CargoType, contract.Quantity);

        try
        {
            _factory.Now = contract.ExpiresAtUtc;
            (await Actions().DeliverContractAsync(user, contract.Id, "late")).Error.Should().Be("That contract has expired.");
        }
        finally
        {
            _factory.Now = AfterHoursPlayFactory.Start;
        }
        (await CargoAsync(user, contract.CargoType)).Should().Be(contract.Quantity);
    }

    [Fact]
    public async Task DifferentPlayers_DeliverTheSameContractIndependently()
    {
        var alice = await CreateUserAsync();
        var bob = await CreateUserAsync();
        var contract = (await Contracts().GetCurrentContractsAsync(alice))[0].Contract;
        await GiveCargoAsync(alice, contract.CargoType, contract.Quantity);
        await GiveCargoAsync(bob, contract.CargoType, contract.Quantity);

        (await Actions().DeliverContractAsync(alice, contract.Id, "same-key")).Accepted.Should().BeTrue();
        (await Actions().DeliverContractAsync(bob, contract.Id, "same-key")).Accepted.Should().BeTrue("keys and completions are per player");

        (await StateAsync(alice)).WalletCash.Should().Be(400 + contract.CashReward);
        (await StateAsync(bob)).WalletCash.Should().Be(400 + contract.CashReward);
    }

    // ------------------------------------------------------------------ helpers

    private IAfterHoursActionService Actions() =>
        _factory.Services.CreateScope().ServiceProvider.GetRequiredService<IAfterHoursActionService>();

    private IBuyerContractService Contracts() =>
        _factory.Services.CreateScope().ServiceProvider.GetRequiredService<IBuyerContractService>();

    private Task<ApplicationDbContext> DbAsync() =>
        _factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();

    private static Task<AfterHoursActionResult[]> Parallel(int count, Func<int, Task<AfterHoursActionResult>> action) =>
        Task.WhenAll(Enumerable.Range(0, count).Select(i => Task.Run(() => action(i))));

    private async Task<string> CreateUserAsync()
    {
        var name = $"ah4-{Guid.NewGuid():N}";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = name, Email = $"{name}@test.com", FirstName = "A", LastName = "H", Nickname = name };
        (await users.CreateAsync(user)).Succeeded.Should().BeTrue();
        return user.Id;
    }

    private async Task<int> StateIdAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var state = await scope.ServiceProvider.GetRequiredService<IPlayerCycleStateService>().GetOrCreateForActiveCycleAsync(userId);
        return state!.Id;
    }

    private async Task<PlayerCycleState> StateAsync(string userId)
    {
        await using var db = await DbAsync();
        return await db.AfterHoursPlayerCycleStates.AsNoTracking()
            .SingleAsync(s => s.UserId == userId && s.GameCycle!.Status == GameCycleStatus.Active);
    }

    private async Task<int> CargoAsync(string userId, CargoType cargo)
    {
        await using var db = await DbAsync();
        return await db.AfterHoursPlayerCargo
            .Where(c => c.CargoType == cargo)
            .Join(db.AfterHoursPlayerCycleStates.Where(s => s.UserId == userId), c => c.PlayerCycleStateId, s => s.Id, (c, _) => c.Quantity)
            .SumAsync();
    }

    private async Task GiveCargoAsync(string userId, CargoType cargo, int quantity)
    {
        var stateId = await StateIdAsync(userId);
        await using var db = await DbAsync();
        db.AfterHoursPlayerCargo.Add(new PlayerCargo { PlayerCycleStateId = stateId, CargoType = cargo, Quantity = quantity });
        await db.SaveChangesAsync();
    }

    private async Task<List<PlayerActionReceipt>> ReceiptsAsync(string userId)
    {
        await using var db = await DbAsync();
        return await db.AfterHoursPlayerActionReceipts.AsNoTracking()
            .Where(r => r.PlayerCycleState!.UserId == userId).ToListAsync();
    }
}
