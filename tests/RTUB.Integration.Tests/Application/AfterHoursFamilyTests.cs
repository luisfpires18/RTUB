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
/// AH-007 families against the real SQLite schema (filtered unique indexes, write-lock serialization),
/// with the AH-003 frozen clock. Every test uses fresh players and uniquely named families.
/// </summary>
public class AfterHoursFamilyTests : IClassFixture<AfterHoursPlayFactory>
{
    private readonly AfterHoursPlayFactory _factory;
    private static DateTime Start => AfterHoursPlayFactory.Start;

    public AfterHoursFamilyTests(AfterHoursPlayFactory factory)
    {
        _factory = factory;
        factory.Now = Start;
        factory.EnsureActiveCycleAsync().GetAwaiter().GetResult();
    }

    // ------------------------------------------------------------------ creation

    [Fact]
    public async Task Create_ChargesWalletOnly_MakesCreatorBoss_OpensTreasury_AndReplays()
    {
        var boss = await PlayerAsync(wallet: 2_000, bank: 5_000);
        var name = Name();

        var created = await Actions().CreateFamilyAsync(boss.UserId, $"  {name} ", "  We own the night ", "c1");
        var replay = await Actions().CreateFamilyAsync(boss.UserId, name, "We own the night", "c1");

        created.Accepted.Should().BeTrue();
        replay.Replayed.Should().BeTrue();
        replay.Receipt!.FamilyId.Should().Be(created.Receipt!.FamilyId);
        var familyId = created.Receipt.FamilyId!.Value;

        var state = await StateAsync(boss.UserId);
        (state.WalletCash, state.BankCash).Should().Be((500L, 5_000L));
        await using var db = await DbAsync();
        var family = await db.AfterHoursFamilies.SingleAsync(f => f.Id == familyId);
        (family.Name, family.Motto, family.NormalizedName).Should().Be((name, "We own the night", name.ToUpperInvariant()));
        (await db.AfterHoursFamilyMemberships.SingleAsync(m => m.FamilyId == familyId)).Role.Should().Be(FamilyRole.Boss);
        (await db.AfterHoursFamilyCycleStates.SingleAsync(s => s.FamilyId == familyId)).TreasuryCash.Should().Be(0);
        (await db.AfterHoursFamilies.CountAsync(f => f.CreatedByUserId == boss.UserId)).Should().Be(1);
    }

    [Fact]
    public async Task Create_Refusals()
    {
        var low = await PlayerAsync(level: 4, wallet: 5_000);
        (await Actions().CreateFamilyAsync(low.UserId, Name(), null, "r1")).Error.Should().Be("Creating a family requires level 5.");

        var broke = await PlayerAsync(wallet: 1_499, bank: 10_000);
        (await Actions().CreateFamilyAsync(broke.UserId, Name(), null, "r2")).Error.Should().Be("Not enough cash in your wallet.");

        var p = await PlayerAsync();
        (await Actions().CreateFamilyAsync(p.UserId, "  ", null, "r3")).Error.Should().Be("Enter a family name.");
        (await Actions().CreateFamilyAsync(p.UserId, "ab", null, "r4")).Error.Should().Contain("3–24");

        var taken = Name();
        await CreateAsync(await PlayerAsync(), taken);
        (await Actions().CreateFamilyAsync(p.UserId, $"  {taken.ToLowerInvariant()} ", null, "r5")).Error.Should().Be("That family name is taken.");

        await CreateAsync(p, Name());
        (await Actions().CreateFamilyAsync(p.UserId, Name(), null, "r6")).Error.Should().Be("You are already in a family.");
    }

    [Fact]
    public async Task ConcurrentCreates_BySamePlayer_MakeOneFamily_AndChargeOnce()
    {
        var boss = await PlayerAsync(wallet: 10_000);

        var results = await Parallel(6, i => Actions().CreateFamilyAsync(boss.UserId, Name(), null, $"cc{i}"));

        results.Count(r => r.Accepted).Should().Be(1);
        (await StateAsync(boss.UserId)).WalletCash.Should().Be(8_500);
        await using var db = await DbAsync();
        (await db.AfterHoursFamilyMemberships.CountAsync(m => m.UserId == boss.UserId)).Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentCreates_OfOneNormalizedName_MakeOneFamily()
    {
        var name = Name();
        var players = new List<Player>();
        for (var i = 0; i < 6; i++) players.Add(await PlayerAsync());

        var results = await Parallel(6, i => Actions().CreateFamilyAsync(players[i].UserId, i % 2 == 0 ? name.ToUpperInvariant() : $" {name.ToLowerInvariant()} ", null, "same-name"));

        results.Count(r => r.Accepted).Should().Be(1);
        results.Where(r => !r.Accepted).Should().OnlyContain(r => r.Error == "That family name is taken.");
        await using var db = await DbAsync();
        (await db.AfterHoursFamilies.CountAsync(f => f.NormalizedName == name.ToUpperInvariant())).Should().Be(1);
    }

    // ------------------------------------------------------------------ invitations

    [Fact]
    public async Task Invitations_BossOnly_NoSelf_NoMembers_NoDuplicates_DeclineAndCancel()
    {
        var boss = await PlayerAsync();
        var familyId = await CreateAsync(boss, Name());
        var member = await JoinAsync(boss, familyId, await PlayerAsync());
        var outsider = await PlayerAsync();
        var otherBoss = await PlayerAsync();
        var otherFamily = await CreateAsync(otherBoss, Name());

        (await Actions().InviteToFamilyAsync(member.UserId, outsider.StateId, "i1")).Error.Should().Be("Only the family Boss can invite.");
        (await Actions().InviteToFamilyAsync(boss.UserId, boss.StateId, "i2")).Error.Should().Be("You can't invite yourself.");
        (await Actions().InviteToFamilyAsync(boss.UserId, otherBoss.StateId, "i3")).Error.Should().Be("That player is already in a family.");
        (await Actions().InviteToFamilyAsync(boss.UserId, 999_999, "i4")).Error.Should().Be("That player is not in this cycle.");

        var invitation = await InviteAsync(boss, outsider);
        (await Actions().InviteToFamilyAsync(boss.UserId, outsider.StateId, "i5")).Error.Should().Contain("already has an invitation");
        (await Actions().CancelFamilyInvitationAsync(otherBoss.UserId, invitation, "i6")).Accepted.Should().BeFalse("another family's Boss");
        (await Actions().AcceptFamilyInvitationAsync(member.UserId, invitation, "i7")).Error.Should().Be("Unknown invitation.", "not addressed to them");

        (await Actions().DeclineFamilyInvitationAsync(outsider.UserId, invitation, "i8")).Accepted.Should().BeTrue();
        (await InvitationAsync(invitation)).Status.Should().Be(FamilyInvitationStatus.Declined);
        (await Overview(outsider.UserId))!.JoinAllowedFromUtc.Should().BeNull("declining starts no cooldown");

        var second = await InviteAsync(boss, outsider);
        (await Actions().CancelFamilyInvitationAsync(boss.UserId, second, "i9")).Accepted.Should().BeTrue();
        (await InvitationAsync(second)).Status.Should().Be(FamilyInvitationStatus.Cancelled);
        (await Actions().AcceptFamilyInvitationAsync(outsider.UserId, second, "i10")).Error.Should().Be("That invitation is no longer pending.");
        otherFamily.Should().NotBe(familyId);
    }

    [Fact]
    public async Task Accept_JoinsAsMember_ClosesOtherInvitations_AndRechecksCapacity()
    {
        var bossA = await PlayerAsync();
        var familyA = await CreateAsync(bossA, Name());
        var bossB = await PlayerAsync();
        await CreateAsync(bossB, Name());
        var joiner = await PlayerAsync();
        var fromA = await InviteAsync(bossA, joiner);
        var fromB = await InviteAsync(bossB, joiner);

        (await Actions().AcceptFamilyInvitationAsync(joiner.UserId, fromA, "a1")).Accepted.Should().BeTrue();

        (await ActiveMembershipAsync(joiner.UserId))!.Should().BeEquivalentTo(new { FamilyId = familyA, Role = FamilyRole.Member });
        (await InvitationAsync(fromA)).Status.Should().Be(FamilyInvitationStatus.Accepted);
        (await InvitationAsync(fromB)).Status.Should().Be(FamilyInvitationStatus.Cancelled);

        // Fill family A to 4, then an older pending invitation cannot push it to 5.
        var late = await PlayerAsync();
        var lateInvite = await InviteAsync(bossA, late);
        await JoinAsync(bossA, familyA, await PlayerAsync());
        await JoinAsync(bossA, familyA, await PlayerAsync());
        (await Actions().AcceptFamilyInvitationAsync(late.UserId, lateInvite, "a2")).Error.Should().Be("That family is full.");
        (await ActiveCountAsync(familyA)).Should().Be(4);
    }

    [Fact]
    public async Task ConcurrentAccepts_IntoTwoFamilies_JoinOnlyOne()
    {
        var bossA = await PlayerAsync();
        await CreateAsync(bossA, Name());
        var bossB = await PlayerAsync();
        await CreateAsync(bossB, Name());
        var joiner = await PlayerAsync();
        var invites = new[] { await InviteAsync(bossA, joiner), await InviteAsync(bossB, joiner) };

        var results = await Parallel(2, i => Actions().AcceptFamilyInvitationAsync(joiner.UserId, invites[i], $"two{i}"));

        results.Count(r => r.Accepted).Should().Be(1);
        await using var db = await DbAsync();
        (await db.AfterHoursFamilyMemberships.CountAsync(m => m.UserId == joiner.UserId && m.LeftAtUtc == null)).Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentAccepts_ForTheLastSlot_NeverExceedFour()
    {
        var boss = await PlayerAsync();
        var familyId = await CreateAsync(boss, Name());
        await JoinAsync(boss, familyId, await PlayerAsync());
        await JoinAsync(boss, familyId, await PlayerAsync());
        var racers = new List<(Player Player, int Invite)>();
        for (var i = 0; i < 4; i++)
        {
            var p = await PlayerAsync();
            racers.Add((p, await InviteAsync(boss, p)));
        }

        var results = await Parallel(4, i => Actions().AcceptFamilyInvitationAsync(racers[i].Player.UserId, racers[i].Invite, $"slot{i}"));

        results.Count(r => r.Accepted).Should().Be(1);
        results.Where(r => !r.Accepted).Should().OnlyContain(r => r.Error == "That family is full.");
        (await ActiveCountAsync(familyId)).Should().Be(4);
    }

    // ------------------------------------------------------------------ roles and Boss

    [Fact]
    public async Task Roles_BossOnly_NeverBoss_NotSelf()
    {
        var boss = await PlayerAsync();
        var familyId = await CreateAsync(boss, Name());
        var a = await JoinAsync(boss, familyId, await PlayerAsync());
        var b = await JoinAsync(boss, familyId, await PlayerAsync());
        var aMembership = (await ActiveMembershipAsync(a.UserId))!.Id;
        var bossMembership = (await ActiveMembershipAsync(boss.UserId))!.Id;

        (await Actions().SetFamilyRoleAsync(boss.UserId, aMembership, FamilyRole.Enforcer, "ro1")).Accepted.Should().BeTrue();
        (await Actions().SetFamilyRoleAsync(boss.UserId, aMembership, FamilyRole.Fixer, "ro2")).Accepted.Should().BeTrue();
        (await ActiveMembershipAsync(a.UserId))!.Role.Should().Be(FamilyRole.Fixer);

        (await Actions().SetFamilyRoleAsync(boss.UserId, aMembership, FamilyRole.Boss, "ro3")).Error.Should().Be("Use Transfer Boss to change the Boss.");
        (await Actions().SetFamilyRoleAsync(boss.UserId, bossMembership, FamilyRole.Member, "ro4")).Accepted.Should().BeFalse("the Boss cannot demote themselves");
        (await Actions().SetFamilyRoleAsync(b.UserId, aMembership, FamilyRole.Member, "ro5")).Error.Should().Be("Only the family Boss can change roles.");

        var otherBoss = await PlayerAsync();
        await CreateAsync(otherBoss, Name());
        (await Actions().SetFamilyRoleAsync(otherBoss.UserId, aMembership, FamilyRole.Member, "ro6")).Accepted.Should().BeFalse("a different family's member");
        (await BossCountAsync(familyId)).Should().Be(1);
    }

    [Fact]
    public async Task TransferBoss_KeepsExactlyOneBoss_EvenUnderRacingTransfers()
    {
        var boss = await PlayerAsync();
        var familyId = await CreateAsync(boss, Name());
        var a = await JoinAsync(boss, familyId, await PlayerAsync());
        var b = await JoinAsync(boss, familyId, await PlayerAsync());
        var targets = new[] { (await ActiveMembershipAsync(a.UserId))!.Id, (await ActiveMembershipAsync(b.UserId))!.Id };

        var results = await Parallel(4, i => Actions().TransferFamilyBossAsync(boss.UserId, targets[i % 2], $"tb{i}"));

        results.Count(r => r.Accepted).Should().Be(1);
        (await BossCountAsync(familyId)).Should().Be(1);
        (await ActiveMembershipAsync(boss.UserId))!.Role.Should().Be(FamilyRole.Member);
    }

    [Fact]
    public async Task Boss_CannotLeaveWithMembers_ButCanAfterTransfer()
    {
        var boss = await PlayerAsync();
        var familyId = await CreateAsync(boss, Name());
        var heir = await JoinAsync(boss, familyId, await PlayerAsync());

        (await Actions().LeaveFamilyAsync(boss.UserId, "l1")).Error.Should().Be("Transfer the Boss role before leaving.");
        await Actions().TransferFamilyBossAsync(boss.UserId, (await ActiveMembershipAsync(heir.UserId))!.Id, "l2");
        (await Actions().LeaveFamilyAsync(boss.UserId, "l3")).Accepted.Should().BeTrue();

        (await ActiveMembershipAsync(heir.UserId))!.Role.Should().Be(FamilyRole.Boss);
        (await FamilyAsync(familyId)).IsDisbanded.Should().BeFalse();
    }

    [Fact]
    public async Task LeaveVsTransfer_Race_EndsValid()
    {
        var boss = await PlayerAsync();
        var familyId = await CreateAsync(boss, Name());
        var member = await JoinAsync(boss, familyId, await PlayerAsync());
        var memberId = (await ActiveMembershipAsync(member.UserId))!.Id;

        await Task.WhenAll(
            Task.Run(() => Actions().LeaveFamilyAsync(member.UserId, "race-leave")),
            Task.Run(() => Actions().TransferFamilyBossAsync(boss.UserId, memberId, "race-transfer")));

        var active = await ActiveMembersAsync(familyId);
        active.Count(m => m.Role == FamilyRole.Boss).Should().Be(1, "a family with members always has exactly one Boss");
    }

    // ------------------------------------------------------------------ leave, disband, cooldown

    [Fact]
    public async Task Leave_KeepsHistory_AndStartsA72HourCooldown()
    {
        var boss = await PlayerAsync();
        var familyId = await CreateAsync(boss, Name());
        var leaver = await JoinAsync(boss, familyId, await PlayerAsync());
        var otherBoss = await PlayerAsync();
        await CreateAsync(otherBoss, Name());

        (await Actions().LeaveFamilyAsync(leaver.UserId, "lv1")).Accepted.Should().BeTrue();
        await using (var db = await DbAsync())
            (await db.AfterHoursFamilyMemberships.SingleAsync(m => m.UserId == leaver.UserId)).LeftAtUtc.Should().Be(Start);

        var invite = await InviteAsync(otherBoss, leaver);
        (await Actions().AcceptFamilyInvitationAsync(leaver.UserId, invite, "lv2")).Error.Should().StartWith("You left a family recently");
        (await Actions().CreateFamilyAsync(leaver.UserId, Name(), null, "lv3")).Error.Should().StartWith("You left a family recently");

        try
        {
            _factory.Now = Start.AddHours(72).AddTicks(-1);
            (await Actions().AcceptFamilyInvitationAsync(leaver.UserId, invite, "lv4")).Accepted.Should().BeFalse();
            _factory.Now = Start.AddHours(72);
            (await Actions().AcceptFamilyInvitationAsync(leaver.UserId, invite, "lv5")).Accepted.Should().BeTrue("the cooldown ends exactly at 72 hours");
        }
        finally
        {
            _factory.Now = Start;
        }

        await using var check = await DbAsync();
        (await check.AfterHoursFamilyMemberships.CountAsync(m => m.UserId == leaver.UserId)).Should().Be(2, "history is kept");
    }

    [Fact]
    public async Task SoloBoss_Leaving_Disbands_CancelsInvitations_AndKeepsTheNameReserved()
    {
        var boss = await PlayerAsync();
        var name = Name();
        var familyId = await CreateAsync(boss, name);
        var invited = await PlayerAsync();
        var invite = await InviteAsync(boss, invited);

        (await Actions().LeaveFamilyAsync(boss.UserId, "d1")).Accepted.Should().BeTrue();

        var family = await FamilyAsync(familyId);
        family.IsDisbanded.Should().BeTrue();
        (await InvitationAsync(invite)).Status.Should().Be(FamilyInvitationStatus.Cancelled);
        (await Actions().AcceptFamilyInvitationAsync(invited.UserId, invite, "d2")).Accepted.Should().BeFalse();
        (await Actions().CreateFamilyAsync((await PlayerAsync()).UserId, name, null, "d3")).Error.Should().Be("That family name is taken.");
        (await ActiveMembershipAsync(boss.UserId)).Should().BeNull();
    }

    [Fact]
    public async Task AcceptVsDisband_Race_NeverJoinsADisbandedFamily()
    {
        var boss = await PlayerAsync();
        var familyId = await CreateAsync(boss, Name());
        var joiner = await PlayerAsync();
        var invite = await InviteAsync(boss, joiner);

        await Task.WhenAll(
            Task.Run(() => Actions().LeaveFamilyAsync(boss.UserId, "rd-leave")),
            Task.Run(() => Actions().AcceptFamilyInvitationAsync(joiner.UserId, invite, "rd-accept")));

        var family = await FamilyAsync(familyId);
        var joined = await ActiveMembershipAsync(joiner.UserId);
        if (family.IsDisbanded)
            joined.Should().BeNull("a disbanded family takes no members");
        else
            (joined!.FamilyId, (await BossCountAsync(familyId))).Should().Be((familyId, 1), "the accept won, so the Boss could not leave");
    }

    // ------------------------------------------------------------------ treasury

    [Fact]
    public async Task Donate_FromWalletToTreasury_Idempotently()
    {
        var boss = await PlayerAsync(wallet: 2_000, bank: 3_000);
        var familyId = await CreateAsync(boss, Name());

        (await Actions().DonateToFamilyAsync(boss.UserId, 200, "dn1")).Accepted.Should().BeTrue();
        (await Actions().DonateToFamilyAsync(boss.UserId, 200, "dn1")).Replayed.Should().BeTrue();
        (await Actions().DonateToFamilyAsync(boss.UserId, 0, "dn2")).Error.Should().Be("Enter an amount above zero.");
        (await Actions().DonateToFamilyAsync(boss.UserId, 301, "dn3")).Error.Should().Be("Not enough cash in your wallet.");
        (await Actions().DonateToFamilyAsync((await PlayerAsync()).UserId, 10, "dn4")).Error.Should().Be("You are not in a family.");

        var state = await StateAsync(boss.UserId);
        (state.WalletCash, state.BankCash, await TreasuryAsync(familyId)).Should().Be((300L, 3_000L, 200L));
    }

    [Fact]
    public async Task ConcurrentDonations_NeverOverspend_AndTheTreasuryIsTheExactSum()
    {
        var boss = await PlayerAsync(wallet: 1_600);
        var familyId = await CreateAsync(boss, Name()); // wallet now 100
        var a = await JoinAsync(boss, familyId, await PlayerAsync(wallet: 500));
        var b = await JoinAsync(boss, familyId, await PlayerAsync(wallet: 500));

        var results = await Parallel(24, i => (i % 3) switch
        {
            0 => Actions().DonateToFamilyAsync(boss.UserId, 30, $"cd{i}"),
            1 => Actions().DonateToFamilyAsync(a.UserId, 70, $"cd{i}"),
            _ => Actions().DonateToFamilyAsync(b.UserId, 45, $"cd{i}")
        });

        results.Count(r => r.Accepted).Should().Be(3 + 7 + 8);
        var accepted = results.Where(r => r.Accepted).Sum(r => -r.Receipt!.WalletDelta);
        (await TreasuryAsync(familyId)).Should().Be(accepted).And.Be(90 + 490 + 360);
        ((await StateAsync(boss.UserId)).WalletCash, (await StateAsync(a.UserId)).WalletCash, (await StateAsync(b.UserId)).WalletCash)
            .Should().Be((10L, 10L, 140L));
    }

    // ------------------------------------------------------------------ persistence boundary

    [Fact]
    public async Task ANewCycle_StartsTheTreasuryAtZero_WithTheSameFamilyAndMembership()
    {
        var boss = await PlayerAsync();
        var familyId = await CreateAsync(boss, Name());
        var membershipId = (await ActiveMembershipAsync(boss.UserId))!.Id;
        await Actions().DonateToFamilyAsync(boss.UserId, 150, "cy1");
        int oldCycle;
        await using (var db = await DbAsync())
            oldCycle = (await db.AfterHoursGameCycles.SingleAsync(c => c.Status == GameCycleStatus.Active)).Id;

        await using (var db = await DbAsync())
            await db.AfterHoursGameCycles.Where(c => c.Id == oldCycle).ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, GameCycleStatus.Finished));
        await _factory.EnsureActiveCycleAsync(); // a second cycle

        (await Actions().DonateToFamilyAsync(boss.UserId, 40, "cy2")).Accepted.Should().BeTrue("the new cycle's state is created with its own wallet");

        await using var check = await DbAsync();
        var states = await check.AfterHoursFamilyCycleStates.Where(s => s.FamilyId == familyId).OrderBy(s => s.GameCycleId).ToListAsync();
        states.Select(s => s.TreasuryCash).Should().Equal(150, 40);
        (await check.AfterHoursFamilies.CountAsync(f => f.Id == familyId)).Should().Be(1);
        (await check.AfterHoursFamilyMemberships.SingleAsync(m => m.UserId == boss.UserId)).Id.Should().Be(membershipId);
    }

    // ------------------------------------------------------------------ profile and isolation

    [Fact]
    public async Task Profile_BossOnly_UniqueName_Trimmed()
    {
        var boss = await PlayerAsync();
        var familyId = await CreateAsync(boss, Name());
        var member = await JoinAsync(boss, familyId, await PlayerAsync());
        var taken = Name();
        await CreateAsync(await PlayerAsync(), taken);

        (await Actions().UpdateFamilyProfileAsync(member.UserId, Name(), null, "p1")).Error.Should().Be("Only the family Boss can edit the family.");
        (await Actions().UpdateFamilyProfileAsync(boss.UserId, taken.ToLowerInvariant(), null, "p2")).Error.Should().Be("That family name is taken.");
        var renamed = Name();
        (await Actions().UpdateFamilyProfileAsync(boss.UserId, $" {renamed} ", "  new motto ", "p3")).Accepted.Should().BeTrue();

        var family = await FamilyAsync(familyId);
        (family.Name, family.NormalizedName, family.Motto).Should().Be((renamed, renamed.ToUpperInvariant(), "new motto"));
    }

    [Fact]
    public async Task Families_LeaveFidelisMyTunoAndFinanceUntouched()
    {
        var boss = await PlayerAsync(wallet: 3_000);
        await using (var db = await DbAsync())
        {
            var before = (await db.Users.Where(u => u.Id == boss.UserId).Select(u => u.FidelisBalance).SingleAsync(),
                await db.Transactions.CountAsync(), await db.Characters.CountAsync());
            await CreateAsync(boss, Name());
            await Actions().DonateToFamilyAsync(boss.UserId, 100, "fin");
            await using var after = await DbAsync();
            (await after.Users.Where(u => u.Id == boss.UserId).Select(u => u.FidelisBalance).SingleAsync(),
                await after.Transactions.CountAsync(), await after.Characters.CountAsync()).Should().Be(before);
        }
    }

    // ------------------------------------------------------------------ helpers

    private sealed record Player(string UserId, int StateId);

    private IAfterHoursActionService Actions() =>
        _factory.Services.CreateScope().ServiceProvider.GetRequiredService<IAfterHoursActionService>();

    private Task<FamilyOverview?> Overview(string userId) =>
        _factory.Services.CreateScope().ServiceProvider.GetRequiredService<IFamilyService>().GetOverviewAsync(userId);

    private Task<ApplicationDbContext> DbAsync() =>
        _factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();

    private static string Name() => $"F{Guid.NewGuid():N}"[..16];

    private static Task<AfterHoursActionResult[]> Parallel(int count, Func<int, Task<AfterHoursActionResult>> action) =>
        Task.WhenAll(Enumerable.Range(0, count).Select(i => Task.Run(() => action(i))));

    private async Task<Player> PlayerAsync(int level = 5, long wallet = 2_000, long bank = 0)
    {
        var name = $"ah7-{Guid.NewGuid():N}";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = name, Email = $"{name}@test.com", FirstName = "A", LastName = "H", Nickname = name };
        (await users.CreateAsync(user)).Succeeded.Should().BeTrue();
        var state = await scope.ServiceProvider.GetRequiredService<IPlayerCycleStateService>().GetOrCreateForActiveCycleAsync(user.Id);

        await using var db = await DbAsync();
        var row = await db.AfterHoursPlayerCycleStates.SingleAsync(s => s.Id == state!.Id);
        (row.Level, row.XP, row.WalletCash, row.BankCash) = (level, RTUB.Core.Helpers.AfterHours.AfterHoursLevels.XpForLevel(level), wallet, bank);
        await db.SaveChangesAsync();
        return new Player(user.Id, state!.Id);
    }

    private async Task<int> CreateAsync(Player boss, string name)
    {
        var result = await Actions().CreateFamilyAsync(boss.UserId, name, null, $"create-{Guid.NewGuid():N}");
        result.Accepted.Should().BeTrue(result.Error);
        return result.Receipt!.FamilyId!.Value;
    }

    private async Task<int> InviteAsync(Player boss, Player target)
    {
        var result = await Actions().InviteToFamilyAsync(boss.UserId, target.StateId, $"invite-{Guid.NewGuid():N}");
        result.Accepted.Should().BeTrue(result.Error);
        await using var db = await DbAsync();
        return (await db.AfterHoursFamilyInvitations.SingleAsync(i =>
            i.FamilyId == result.Receipt!.FamilyId && i.InvitedUserId == target.UserId && i.Status == FamilyInvitationStatus.Pending)).Id;
    }

    private async Task<Player> JoinAsync(Player boss, int familyId, Player joiner)
    {
        var invite = await InviteAsync(boss, joiner);
        var result = await Actions().AcceptFamilyInvitationAsync(joiner.UserId, invite, $"join-{Guid.NewGuid():N}");
        result.Accepted.Should().BeTrue(result.Error);
        result.Receipt!.FamilyId.Should().Be(familyId);
        return joiner;
    }

    private async Task<PlayerCycleState> StateAsync(string userId)
    {
        await using var db = await DbAsync();
        return await db.AfterHoursPlayerCycleStates.AsNoTracking()
            .SingleAsync(s => s.UserId == userId && s.GameCycle!.Status == GameCycleStatus.Active);
    }

    private async Task<FamilyMembership?> ActiveMembershipAsync(string userId)
    {
        await using var db = await DbAsync();
        return await db.AfterHoursFamilyMemberships.AsNoTracking().SingleOrDefaultAsync(m => m.UserId == userId && m.LeftAtUtc == null);
    }

    private async Task<List<FamilyMembership>> ActiveMembersAsync(int familyId)
    {
        await using var db = await DbAsync();
        return await db.AfterHoursFamilyMemberships.AsNoTracking().Where(m => m.FamilyId == familyId && m.LeftAtUtc == null).ToListAsync();
    }

    private async Task<int> ActiveCountAsync(int familyId) => (await ActiveMembersAsync(familyId)).Count;

    private async Task<int> BossCountAsync(int familyId) => (await ActiveMembersAsync(familyId)).Count(m => m.Role == FamilyRole.Boss);

    private async Task<Family> FamilyAsync(int familyId)
    {
        await using var db = await DbAsync();
        return await db.AfterHoursFamilies.AsNoTracking().SingleAsync(f => f.Id == familyId);
    }

    private async Task<FamilyInvitation> InvitationAsync(int id)
    {
        await using var db = await DbAsync();
        return await db.AfterHoursFamilyInvitations.AsNoTracking().SingleAsync(i => i.Id == id);
    }

    private async Task<long> TreasuryAsync(int familyId)
    {
        await using var db = await DbAsync();
        var cycle = await db.AfterHoursGameCycles.SingleAsync(c => c.Status == GameCycleStatus.Active);
        return await db.AfterHoursFamilyCycleStates.Where(s => s.FamilyId == familyId && s.GameCycleId == cycle.Id).Select(s => s.TreasuryCash).SingleAsync();
    }
}
