using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces.AfterHours;
using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;
using RTUB.Core.Helpers.AfterHours;

namespace RTUB.Application.Services.AfterHours;

/// <summary>
/// Family actions. Each is one <see cref="RunAsync"/> action, so one write transaction with a receipt:
/// every membership, invitation, family and treasury change commits together or not at all, and the
/// write lock serializes racing requests. The acting family always comes from the caller's own active
/// membership (or, for invitations, from an invitation addressed to or sent by the caller).
/// </summary>
public partial class AfterHoursActionService
{
    public Task<AfterHoursActionResult> CreateFamilyAsync(string userId, string name, string? motto, string idempotencyKey) =>
        RunAsync(userId, idempotencyKey, FamilyRules.CreateRequest(name, motto), async (context, state, now, env) =>
        {
            if (FamilyRules.ValidateName(name, out var trimmedName) is { } badName) return ActionAttempt.Reject(badName);
            if (FamilyRules.ValidateMotto(motto, out var trimmedMotto) is { } badMotto) return ActionAttempt.Reject(badMotto);
            if (state.Level < FamilyRules.CreateLevel) return ActionAttempt.Reject($"Creating a family requires level {FamilyRules.CreateLevel}.");
            if (await ActiveMembershipAsync(context, state.UserId) is not null) return ActionAttempt.Reject("You are already in a family.");
            if (await CooldownReasonAsync(context, state.UserId, now) is { } cooling) return ActionAttempt.Reject(cooling);
            var cost = env.Tuning.FamilyCreationCost;
            if (state.WalletCash < cost) return ActionAttempt.Reject("Not enough cash in your wallet.");

            var normalized = FamilyRules.NormalizeName(trimmedName);
            if (await context.AfterHoursFamilies.AnyAsync(f => f.NormalizedName == normalized))
                return ActionAttempt.Reject("That family name is taken.");

            var family = new Family
            {
                Name = trimmedName,
                NormalizedName = normalized,
                Motto = trimmedMotto,
                CreatedAtUtc = now,
                CreatedByUserId = state.UserId
            };
            context.AfterHoursFamilies.Add(family);
            context.AfterHoursFamilyMemberships.Add(new FamilyMembership { Family = family, UserId = state.UserId, Role = FamilyRole.Boss, JoinedAtUtc = now });
            state.WalletCash -= cost;
            await context.SaveChangesAsync(); // assigns the family id for the cycle state and the receipt
            await EnsureFamilyCycleStateAsync(context, family.Id, state.GameCycleId);

            return AfterHoursActions.Accepted(state, PlayerActionKind.CreateFamily, FamilyRules.CreateRequest(name, motto), family.Id, -cost);
        });

    /// <summary>Boss only. The target is identified by their player state in the current cycle.</summary>
    public Task<AfterHoursActionResult> InviteToFamilyAsync(string userId, int targetStateId, string idempotencyKey)
    {
        var request = $"family-invite:{targetStateId}";
        return RunAsync(userId, idempotencyKey, request, async (context, state, now, env) =>
        {
            var boss = await ActiveMembershipAsync(context, state.UserId);
            if (boss is null || boss.Role != FamilyRole.Boss) return ActionAttempt.Reject("Only the family Boss can invite.");

            var target = await context.AfterHoursPlayerCycleStates.AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == targetStateId && s.GameCycleId == state.GameCycleId);
            if (target is null) return ActionAttempt.Reject("That player is not in this cycle.");
            if (target.UserId == state.UserId) return ActionAttempt.Reject("You can't invite yourself.");
            if (await ActiveMembershipAsync(context, target.UserId) is not null) return ActionAttempt.Reject("That player is already in a family.");
            if (await context.AfterHoursFamilyInvitations.AnyAsync(i => i.FamilyId == boss.FamilyId && i.InvitedUserId == target.UserId && i.Status == FamilyInvitationStatus.Pending))
                return ActionAttempt.Reject("That player already has an invitation from your family.");

            context.AfterHoursFamilyInvitations.Add(new FamilyInvitation
            {
                FamilyId = boss.FamilyId,
                InvitedUserId = target.UserId,
                InvitedByUserId = state.UserId,
                CreatedAtUtc = now,
                Status = FamilyInvitationStatus.Pending
            });
            return AfterHoursActions.Accepted(state, PlayerActionKind.InviteToFamily, request, boss.FamilyId);
        });
    }

    public Task<AfterHoursActionResult> CancelFamilyInvitationAsync(string userId, int invitationId, string idempotencyKey)
    {
        var request = $"family-cancel:{invitationId}";
        return RunAsync(userId, idempotencyKey, request, async (context, state, now, env) =>
        {
            var boss = await ActiveMembershipAsync(context, state.UserId);
            var invitation = await context.AfterHoursFamilyInvitations.SingleOrDefaultAsync(i => i.Id == invitationId);
            if (boss is null || boss.Role != FamilyRole.Boss || invitation is null || invitation.FamilyId != boss.FamilyId)
                return ActionAttempt.Reject("Only your family's Boss can cancel that invitation.");
            if (invitation.Status != FamilyInvitationStatus.Pending) return ActionAttempt.Reject("That invitation is no longer pending.");

            Resolve(invitation, FamilyInvitationStatus.Cancelled, now);
            return AfterHoursActions.Accepted(state, PlayerActionKind.CancelFamilyInvitation, request, boss.FamilyId);
        });
    }

    public Task<AfterHoursActionResult> AcceptFamilyInvitationAsync(string userId, int invitationId, string idempotencyKey)
    {
        var request = $"family-accept:{invitationId}";
        return RunAsync(userId, idempotencyKey, request, async (context, state, now, env) =>
        {
            var invitation = await context.AfterHoursFamilyInvitations.SingleOrDefaultAsync(i => i.Id == invitationId && i.InvitedUserId == state.UserId);
            if (invitation is null) return ActionAttempt.Reject("Unknown invitation.");
            if (invitation.Status != FamilyInvitationStatus.Pending) return ActionAttempt.Reject("That invitation is no longer pending.");

            var family = await context.AfterHoursFamilies.SingleAsync(f => f.Id == invitation.FamilyId);
            if (family.IsDisbanded) return ActionAttempt.Reject("That family has disbanded.");
            if (await ActiveMembershipAsync(context, state.UserId) is not null) return ActionAttempt.Reject("You are already in a family.");
            if (await CooldownReasonAsync(context, state.UserId, now) is { } cooling) return ActionAttempt.Reject(cooling);
            if (await context.AfterHoursFamilyMemberships.CountAsync(m => m.FamilyId == family.Id && m.LeftAtUtc == null) >= FamilyRules.MaxActiveMembers)
                return ActionAttempt.Reject("That family is full.");

            context.AfterHoursFamilyMemberships.Add(new FamilyMembership { FamilyId = family.Id, UserId = state.UserId, Role = FamilyRole.Member, JoinedAtUtc = now });
            await EnsureFamilyCycleStateAsync(context, family.Id, state.GameCycleId);
            Resolve(invitation, FamilyInvitationStatus.Accepted, now);
            foreach (var other in await context.AfterHoursFamilyInvitations
                         .Where(i => i.InvitedUserId == state.UserId && i.Status == FamilyInvitationStatus.Pending && i.Id != invitation.Id)
                         .ToListAsync())
                Resolve(other, FamilyInvitationStatus.Cancelled, now);

            return AfterHoursActions.Accepted(state, PlayerActionKind.AcceptFamilyInvitation, request, family.Id);
        });
    }

    public Task<AfterHoursActionResult> DeclineFamilyInvitationAsync(string userId, int invitationId, string idempotencyKey)
    {
        var request = $"family-decline:{invitationId}";
        return RunAsync(userId, idempotencyKey, request, async (context, state, now, env) =>
        {
            var invitation = await context.AfterHoursFamilyInvitations.SingleOrDefaultAsync(i => i.Id == invitationId && i.InvitedUserId == state.UserId);
            if (invitation is null) return ActionAttempt.Reject("Unknown invitation.");
            if (invitation.Status != FamilyInvitationStatus.Pending) return ActionAttempt.Reject("That invitation is no longer pending.");

            Resolve(invitation, FamilyInvitationStatus.Declined, now);
            return AfterHoursActions.Accepted(state, PlayerActionKind.DeclineFamilyInvitation, request, invitation.FamilyId);
        });
    }

    /// <summary>
    /// Leaves the family and starts the 72-hour cooldown. A Boss with other members must transfer first;
    /// a Boss who is the last member disbands the family (AH-007 choice): history is kept, pending
    /// invitations are cancelled, the name stays reserved.
    /// </summary>
    public Task<AfterHoursActionResult> LeaveFamilyAsync(string userId, string idempotencyKey) =>
        RunAsync(userId, idempotencyKey, "family-leave", async (context, state, now, env) =>
        {
            var membership = await ActiveMembershipAsync(context, state.UserId);
            if (membership is null) return ActionAttempt.Reject("You are not in a family.");

            var others = await context.AfterHoursFamilyMemberships
                .CountAsync(m => m.FamilyId == membership.FamilyId && m.LeftAtUtc == null && m.Id != membership.Id);
            if (membership.Role == FamilyRole.Boss && others > 0)
                return ActionAttempt.Reject("Transfer the Boss role before leaving.");

            membership.LeftAtUtc = now;
            if (others == 0)
            {
                membership.Family!.DisbandedAtUtc = now;
                foreach (var pending in await context.AfterHoursFamilyInvitations
                             .Where(i => i.FamilyId == membership.FamilyId && i.Status == FamilyInvitationStatus.Pending).ToListAsync())
                    Resolve(pending, FamilyInvitationStatus.Cancelled, now);
            }

            return AfterHoursActions.Accepted(state, PlayerActionKind.LeaveFamily, "family-leave", membership.FamilyId);
        });

    /// <summary>The Boss hands the role to another active member and becomes a Member.</summary>
    public Task<AfterHoursActionResult> TransferFamilyBossAsync(string userId, int targetMembershipId, string idempotencyKey)
    {
        var request = $"family-boss:{targetMembershipId}";
        return RunAsync(userId, idempotencyKey, request, async (context, state, now, env) =>
        {
            var boss = await ActiveMembershipAsync(context, state.UserId);
            if (boss is null || boss.Role != FamilyRole.Boss) return ActionAttempt.Reject("Only the family Boss can transfer leadership.");
            var target = await context.AfterHoursFamilyMemberships
                .SingleOrDefaultAsync(m => m.Id == targetMembershipId && m.FamilyId == boss.FamilyId && m.LeftAtUtc == null);
            if (target is null || target.Id == boss.Id) return ActionAttempt.Reject("Choose another current member of your family.");

            // Two saves inside the one transaction: the one-Boss index must never see two Bosses.
            boss.Role = FamilyRole.Member;
            await context.SaveChangesAsync();
            target.Role = FamilyRole.Boss;
            return AfterHoursActions.Accepted(state, PlayerActionKind.TransferFamilyBoss, request, boss.FamilyId);
        });
    }

    /// <summary>The Boss sets another member's role to Member, Enforcer or Fixer. Never Boss: that is a transfer.</summary>
    public Task<AfterHoursActionResult> SetFamilyRoleAsync(string userId, int targetMembershipId, FamilyRole role, string idempotencyKey)
    {
        var request = $"family-role:{targetMembershipId}:{(int)role}";
        return RunAsync(userId, idempotencyKey, request, async (context, state, now, env) =>
        {
            if (role is not (FamilyRole.Member or FamilyRole.Enforcer or FamilyRole.Fixer))
                return ActionAttempt.Reject("Use Transfer Boss to change the Boss.");
            var boss = await ActiveMembershipAsync(context, state.UserId);
            if (boss is null || boss.Role != FamilyRole.Boss) return ActionAttempt.Reject("Only the family Boss can change roles.");
            var target = await context.AfterHoursFamilyMemberships
                .SingleOrDefaultAsync(m => m.Id == targetMembershipId && m.FamilyId == boss.FamilyId && m.LeftAtUtc == null);
            if (target is null || target.Id == boss.Id) return ActionAttempt.Reject("Choose another current member of your family.");

            target.Role = role;
            return AfterHoursActions.Accepted(state, PlayerActionKind.SetFamilyRole, request, boss.FamilyId);
        });
    }

    public Task<AfterHoursActionResult> UpdateFamilyProfileAsync(string userId, string name, string? motto, string idempotencyKey) =>
        RunAsync(userId, idempotencyKey, FamilyRules.ProfileRequest(name, motto), async (context, state, now, env) =>
        {
            if (FamilyRules.ValidateName(name, out var trimmedName) is { } badName) return ActionAttempt.Reject(badName);
            if (FamilyRules.ValidateMotto(motto, out var trimmedMotto) is { } badMotto) return ActionAttempt.Reject(badMotto);
            var boss = await ActiveMembershipAsync(context, state.UserId);
            if (boss is null || boss.Role != FamilyRole.Boss) return ActionAttempt.Reject("Only the family Boss can edit the family.");

            var normalized = FamilyRules.NormalizeName(trimmedName);
            if (await context.AfterHoursFamilies.AnyAsync(f => f.NormalizedName == normalized && f.Id != boss.FamilyId))
                return ActionAttempt.Reject("That family name is taken.");

            boss.Family!.Name = trimmedName;
            boss.Family.NormalizedName = normalized;
            boss.Family.Motto = trimmedMotto;
            return AfterHoursActions.Accepted(state, PlayerActionKind.UpdateFamilyProfile, FamilyRules.ProfileRequest(name, motto), boss.FamilyId);
        });

    /// <summary>Wallet cash (never the bank) into this cycle's treasury. No fee, no withdrawals (AH-007).</summary>
    public Task<AfterHoursActionResult> DonateToFamilyAsync(string userId, long amount, string idempotencyKey)
    {
        var request = $"family-donate:{amount}";
        return RunAsync(userId, idempotencyKey, request, async (context, state, now, env) =>
        {
            var membership = await ActiveMembershipAsync(context, state.UserId);
            if (membership is null) return ActionAttempt.Reject("You are not in a family.");
            if (amount < 1) return ActionAttempt.Reject("Enter an amount above zero.");
            if (state.WalletCash < amount) return ActionAttempt.Reject("Not enough cash in your wallet.");

            var treasury = await EnsureFamilyCycleStateAsync(context, membership.FamilyId, state.GameCycleId);
            state.WalletCash -= amount;
            treasury.TreasuryCash += amount;
            return AfterHoursActions.Accepted(state, PlayerActionKind.DonateToFamily, request, membership.FamilyId, -amount);
        });
    }

    // ------------------------------------------------------------------ helpers (inside the transaction)

    private static Task<FamilyMembership?> ActiveMembershipAsync(ApplicationDbContext context, string userId) =>
        context.AfterHoursFamilyMemberships.Include(m => m.Family)
            .SingleOrDefaultAsync(m => m.UserId == userId && m.LeftAtUtc == null);

    private static async Task<string?> CooldownReasonAsync(ApplicationDbContext context, string userId, DateTime now)
    {
        var lastLeft = await context.AfterHoursFamilyMemberships
            .Where(m => m.UserId == userId && m.LeftAtUtc != null)
            .MaxAsync(m => m.LeftAtUtc);
        return FamilyRules.InLeaveCooldown(lastLeft, now)
            ? $"You left a family recently. You can join or create one from {FamilyRules.JoinAllowedFromUtc(lastLeft):dd MMM HH:mm} UTC."
            : null;
    }

    /// <summary>The family's state for this cycle, created with a 0 treasury on first need.</summary>
    private static async Task<FamilyCycleState> EnsureFamilyCycleStateAsync(ApplicationDbContext context, int familyId, int cycleId)
    {
        var existing = await context.AfterHoursFamilyCycleStates.SingleOrDefaultAsync(s => s.FamilyId == familyId && s.GameCycleId == cycleId);
        if (existing is not null) return existing;

        var created = new FamilyCycleState { FamilyId = familyId, GameCycleId = cycleId, TreasuryCash = 0 };
        context.AfterHoursFamilyCycleStates.Add(created);
        return created;
    }

    private static void Resolve(FamilyInvitation invitation, FamilyInvitationStatus status, DateTime now)
    {
        invitation.Status = status;
        invitation.ResolvedAtUtc = now;
    }
}
