using FluentAssertions;
using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;
using RTUB.Core.Helpers.AfterHours;
using Xunit;

namespace RTUB.Core.Tests.Entities.AfterHours;

/// <summary>AH-004 rules as pure functions: cargo catalogue, crime cargo, fence, contracts, rotation.</summary>
public class AfterHoursCargoRulesTests
{
    private static readonly DateTime T0 = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static PlayerCycleState NewState()
    {
        var state = PlayerCycleState.CreateInitial(1, "u", T0);
        state.Id = 5;
        return state;
    }

    [Theory]
    [InlineData(CargoType.Phone, 15)]
    [InlineData(CargoType.Electronics, 30)]
    [InlineData(CargoType.TicketBundle, 20)]
    [InlineData(CargoType.Spirits, 25)]
    [InlineData(CargoType.ArtPiece, 80)]
    public void FencePrices(CargoType cargo, long price) => CargoCatalogue.FencePrice(cargo).Should().Be(price);

    [Theory]
    [InlineData("C01", CargoType.Phone, 1)]
    [InlineData("C02", null, 0)]
    [InlineData("C03", CargoType.Electronics, 1)]
    [InlineData("C04", CargoType.TicketBundle, 1)]
    [InlineData("C05", null, 0)]
    [InlineData("C06", CargoType.Spirits, 2)]
    [InlineData("C07", CargoType.Electronics, 2)]
    [InlineData("C08", CargoType.Electronics, 3)]
    [InlineData("C09", CargoType.Spirits, 3)]
    [InlineData("C10", CargoType.TicketBundle, 3)]
    [InlineData("C11", CargoType.Electronics, 4)]
    [InlineData("C12", CargoType.ArtPiece, 2)]
    public void CrimeCargo_MatchesTheManual(string id, CargoType? cargo, int quantity)
    {
        var crime = CrimeCatalogue.Find(id)!;
        (crime.Cargo, crime.CargoQuantity).Should().Be((cargo, quantity));
    }

    [Fact]
    public void Crime_Success_AwardsCargo_Failure_AwardsNone()
    {
        var won = NewState();
        var r1 = AfterHoursActions.CommitCrime(won, "C01", CrimeApproach.Standard, T0, () => 1).Receipt!;
        won.CargoQuantity(CargoType.Phone).Should().Be(1);
        (r1.CargoType, r1.CargoDelta).Should().Be((CargoType.Phone, 1));

        var lost = NewState();
        var rolls = new Queue<int>([100, 100]);
        var r2 = AfterHoursActions.CommitCrime(lost, "C01", CrimeApproach.Standard, T0, rolls.Dequeue).Receipt!;
        r2.Succeeded.Should().BeFalse();
        lost.Cargo.Should().BeEmpty();
        (r2.CargoType, r2.CargoDelta).Should().Be(((CargoType?)null, 0));
    }

    [Fact]
    public void Cargo_AccumulatesInOneRowPerType()
    {
        var s = NewState();
        s.AddCargo(CargoType.Spirits, 2);
        s.AddCargo(CargoType.Spirits, 3);

        s.Cargo.Should().ContainSingle().Which.Quantity.Should().Be(5);
        FluentActions.Invoking(() => s.AddCargo(CargoType.Spirits, -6)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Fence_PartialSale_PaysQuantityTimesBasePrice()
    {
        var s = NewState();
        s.AddCargo(CargoType.Electronics, 5);

        var receipt = AfterHoursActions.SellToFence(s, CargoType.Electronics, 3).Receipt!;

        s.CargoQuantity(CargoType.Electronics).Should().Be(2);
        s.WalletCash.Should().Be(400 + 90);
        (receipt.Action, receipt.CargoDelta, receipt.WalletDelta, receipt.Request)
            .Should().Be((PlayerActionKind.FenceSale, -3, 90L, "fence:Electronics:3"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3)]
    public void Fence_Refuses_ZeroNegativeAndMoreThanOwned(int quantity)
    {
        var s = NewState();
        s.AddCargo(CargoType.Phone, 2);

        AfterHoursActions.SellToFence(s, CargoType.Phone, quantity).Error.Should().NotBeNull();
        (s.CargoQuantity(CargoType.Phone), s.WalletCash).Should().Be((2, 400L));
    }

    [Fact]
    public void Templates_AlwaysPayMoreThanTheFence_AndIncludeTheManualExample()
    {
        BuyerContractRules.Templates.Should().HaveCount(10);
        BuyerContractRules.Templates.Select(t => t.Key).Should().OnlyHaveUniqueItems();
        BuyerContractRules.Templates.Should().OnlyContain(t => t.Cash > t.Quantity * CargoCatalogue.FencePrice(t.Cargo) && t.Xp > 0);
        BuyerContractRules.Templates.Should().ContainEquivalentOf(
            new BuyerContractTemplate("T01", "The Midnight Collector", CargoType.ArtPiece, 4, 460, 80));
        BuyerContractRules.Templates.Select(t => t.Cargo).Distinct().Should().HaveCount(5);
    }

    [Fact]
    public void Rotation_WindowsAreTwelveUtcHours_AlignedToMidnightAndNoon()
    {
        BuyerContractRules.WindowStart(new DateTime(2026, 10, 1, 11, 59, 59, DateTimeKind.Utc))
            .Should().Be(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        BuyerContractRules.WindowStart(new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc))
            .Should().Be(new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc));
        BuyerContractRules.WindowStart(new DateTime(2026, 10, 1, 23, 59, 59, DateTimeKind.Utc))
            .Should().Be(new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Rotation_IsDeterministic_ThreeDistinctPerWindow_AndMovesOn()
    {
        var window = BuyerContractRules.WindowStart(T0);
        var keys = Enumerable.Range(0, 3).Select(slot => BuyerContractRules.TemplateFor(window, slot).Key).ToList();
        var next = Enumerable.Range(0, 3).Select(slot => BuyerContractRules.TemplateFor(window.AddHours(12), slot).Key).ToList();

        keys.Should().OnlyHaveUniqueItems().And.HaveCount(3);
        Enumerable.Range(0, 3).Select(slot => BuyerContractRules.TemplateFor(window, slot).Key).Should().Equal(keys);
        next.Should().NotEqual(keys);

        var contract = BuyerContractRules.Create(9, window, 1);
        (contract.AvailableFromUtc, contract.ExpiresAtUtc).Should().Be((window, window.AddHours(12)));
        contract.IsOpenAt(window.AddHours(12).AddTicks(-1)).Should().BeTrue();
        contract.IsOpenAt(window.AddHours(12)).Should().BeFalse();
    }

    [Fact]
    public void Contract_Delivery_TakesCargo_PaysCashAndXp_AndLevels()
    {
        var s = NewState();
        s.AddCargo(CargoType.ArtPiece, 5);
        var contract = MidnightCollector(s);

        var receipt = AfterHoursActions.DeliverContract(s, contract, alreadyCompleted: false, T0).Receipt!;

        s.CargoQuantity(CargoType.ArtPiece).Should().Be(1);
        (s.WalletCash, s.XP, s.Level).Should().Be((860L, 80L, 1));
        (receipt.Action, receipt.CargoDelta, receipt.WalletDelta, receipt.XpDelta, receipt.Request)
            .Should().Be((PlayerActionKind.ContractDelivery, -4, 460L, 80L, $"contract:{contract.Id}"));

        var leveller = NewState();
        leveller.XP = 30;
        leveller.AddCargo(CargoType.ArtPiece, 4);
        AfterHoursActions.DeliverContract(leveller, contract, false, T0).Receipt!.LevelAfter.Should().Be(2);
    }

    [Fact]
    public void Contract_Delivery_Refusals()
    {
        var s = NewState();
        s.AddCargo(CargoType.ArtPiece, 3);
        var contract = MidnightCollector(s);

        AfterHoursActions.DeliverContract(s, contract, false, T0).Error.Should().Be("You need 4 art pieces.");

        s.AddCargo(CargoType.ArtPiece, 1);
        AfterHoursActions.DeliverContract(s, contract, true, T0).Error.Should().Be("You already delivered this contract.");
        AfterHoursActions.DeliverContract(s, contract, false, contract.ExpiresAtUtc).Error.Should().Be("That contract has expired.");
        contract.GameCycleId = 99;
        AfterHoursActions.DeliverContract(s, contract, false, T0).Error.Should().Be("That contract is not part of this cycle.");
        (s.CargoQuantity(CargoType.ArtPiece), s.WalletCash).Should().Be((4, 400L));
    }

    private static BuyerContract MidnightCollector(PlayerCycleState state)
    {
        var contract = BuyerContractRules.Create(state.GameCycleId, BuyerContractRules.WindowStart(T0), 0);
        contract.Id = 42;
        contract.CargoType = CargoType.ArtPiece;
        contract.Quantity = 4;
        contract.CashReward = 460;
        contract.XpReward = 80;
        return contract;
    }
}
