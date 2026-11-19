using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// The React /treasury/calotes, /treasury/mbway and
/// /treasury/nerba[/{eventId}] (docs/react-treasury.md). Same tables; the writes go through the old
/// services. Rules are server-side now (<see cref="TreasuryAuthorization"/>); three of these pages had no sign-in at all.
/// - calotes: treasury members see every debt of a fiscal year, Caloiros and Leitões only their own; Mod / Admin / Owner
///   add (member not a Leitão, amount &gt; 0, a new debt inherits the member's commitment date), edit, remove and set the
///   commitment date for all of a member's debts of the year;
/// - MBWay: every transfer, newest first, totals per recipient (never counted in report totals); Mod / Admin / Owner add,
///   Owner edits and deletes; the recipient is a member (the service refuses anything else);
/// - Nerba: events of type Nerba, upcoming then past; items per day of a multi-day event; Mod / Admin / Owner manage.
/// The member pickers return names and avatars (no e-mail, though it is still searched).
/// </summary>
public sealed partial class TreasuryRecordsService : ITreasuryRecordsService
{
    private readonly TimeProvider _time;

    public const int SearchLimit = 20;

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly IMemberDebtService _debts;
    private readonly IMbwayTransferService _transfers;
    private readonly INerbaOrderService _orders;

    public TreasuryRecordsService(IDbContextFactory<ApplicationDbContext> contexts, IMemberDebtService debts, IMbwayTransferService transfers,
        INerbaOrderService orders,
        TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
        _contexts = contexts;
        _debts = debts;
        _transfers = transfers;
        _orders = orders;
    }

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<ApplicationUser?> MemberAsync(ClaimsPrincipal user)
    {
        if (user.UserId() is not { } id)
        {
            return null;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
    }

    /// <summary>401 for visitors, 403 unless a treasury member (and, if given, <paramref name="allowed"/>).</summary>
    private async Task<EventResult<T>?> RefusalAsync<T>(ClaimsPrincipal user, Func<ClaimsPrincipal, bool>? allowed = null)
    {
        if (await MemberAsync(user) is not { } me)
        {
            return EventResult<T>.Fail(EventResultStatus.SignInRequired);
        }

        return TreasuryAuthorization.CanView(me, user) && (allowed is null || allowed(user)) ? null : EventResult<T>.Fail(EventResultStatus.Forbidden);
    }
}
