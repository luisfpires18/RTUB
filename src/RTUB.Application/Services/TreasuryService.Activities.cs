// Treasury: report balance, activity lock and the calotes total.
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Constants;
using RTUB.Core.Entities;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

public sealed partial class TreasuryService
{
    public async Task<EventResult<TreasuryReportDto>> SetBalanceAsync(int reportId, TreasuryBalanceInput input, ClaimsPrincipal user) =>
        await ChangeAsync(user, async db => await db.Reports.AsNoTracking().Where(r => r.Id == reportId).Select(r => (int?)r.Id).SingleOrDefaultAsync(),
            async _ =>
            {
                if (input.Kind is not ("bank" or "cash"))
                {
                    return ("kind", "Escolha banco ou caixa.");
                }

                if (input.Value is not { } value || decimal.Round(value, 2) != value)
                {
                    return ("value", "Indique um valor em euros, com até duas casas decimais.");
                }

                await _finance.SaveBankCashValueAsync(reportId, input.Kind == "bank", value, _activities, _transactions);
                return null;
            });

    // ---------------------------------------------------------------- activities

    public async Task<EventResult<TreasuryReportDto>> CreateActivityAsync(int reportId, TreasuryActivityInput input, ClaimsPrincipal user) =>
        await ChangeAsync(user, async db => await db.Reports.AsNoTracking().Where(r => r.Id == reportId).Select(r => (int?)r.Id).SingleOrDefaultAsync(),
            async db =>
            {
                if ((ActivityError(input) ?? await EventError(db, reportId, input.EventId)) is { } error)
                {
                    return error;
                }

                var created = await _activities.CreateActivityAsync(reportId, input.Name!.Trim(), input.StartDate!.Value.Date, Text(input.Description), input.EndDate?.Date);
                await LinkEventAsync(db, created.Id, input.EventId);
                return null;
            });

    public async Task<EventResult<TreasuryReportDto>> UpdateActivityAsync(int id, TreasuryActivityInput input, ClaimsPrincipal user) =>
        await ChangeAsync(user, db => ReportOfActivityAsync(db, id), async db =>
        {
            var reportId = (await ReportOfActivityAsync(db, id))!.Value;
            if ((ActivityError(input) ?? await EventError(db, reportId, input.EventId)) is { } error)
            {
                return error;
            }

            await _activities.UpdateActivityAsync(id, input.Name!.Trim(), input.StartDate!.Value.Date, Text(input.Description), input.EndDate?.Date);
            await LinkEventAsync(db, id, input.EventId);
            return null;
        });

    /// <summary>
    /// Task 074: an activity may account for an event that is not cancelled and starts in the report's fiscal year (the
    /// same events "As minhas ações pendentes" asks the treasurers about).
    /// </summary>
    private static async Task<(string, string)?> EventError(ApplicationDbContext db, int reportId, int? eventId)
    {
        if (eventId is not { } id)
        {
            return null;
        }

        var year = await db.Reports.AsNoTracking().Where(r => r.Id == reportId).Select(r => r.Year).SingleAsync();
        var (from, to) = (new DateTime(year, 9, 1), new DateTime(year + 1, 9, 1));
        return await db.Events.AsNoTracking().AnyAsync(e => e.Id == id && !e.IsCancelled && e.Date >= from && e.Date < to)
            ? null
            : ("eventId", "Escolha uma atuação deste ano letivo.");
    }

    /// <summary>Sets (or clears) the activity's event, through the context so the change is audited with the activity.</summary>
    private static async Task LinkEventAsync(ApplicationDbContext db, int activityId, int? eventId)
    {
        var activity = await db.Activities.SingleAsync(a => a.Id == activityId);
        if (activity.EventId != eventId)
        {
            activity.EventId = eventId;
            await db.SaveChangesAsync();
        }
    }

    public async Task<EventResult<TreasuryReportDto>> SetActivityLockAsync(int id, TreasuryLockInput input, ClaimsPrincipal user) =>
        await ChangeAsync(user, db => ReportOfActivityAsync(db, id), async _ =>
        {
            if (input.Locked)
            {
                await _activities.LockActivityAsync(id);
            }
            else
            {
                await _activities.UnlockActivityAsync(id);
            }

            return null;
        }, evenIfPublished: true);

    public async Task<EventResult<TreasuryReportDto>> DeleteActivityAsync(int id, ClaimsPrincipal user) =>
        await ChangeAsync(user, db => ReportOfActivityAsync(db, id), async _ =>
        {
            await DeleteWithTransactionsAsync(d => d.Transactions.Where(t => t.ActivityId == id), async d => await d.Activities.Where(a => a.Id == id).ToListAsync());
            return null;
        });

    // ---------------------------------------------------------------- transactions

    public async Task<EventResult<TreasuryReportDto>> CreateTransactionAsync(int activityId, TreasuryTransactionInput input, ClaimsPrincipal user) =>
        await ChangeAsync(user, db => ReportOfActivityAsync(db, activityId), async _ =>
        {
            if (TransactionError(input) is { } error)
            {
                return error;
            }

            var r = input.Receipt;
            await _transactions.CreateTransactionAsync(input.Date!.Value.Date, input.Description!.Trim(), input.Category!.Trim(), input.Amount!.Value,
                input.Type!, activityId, r?.Content, r?.FileName, r?.ContentType);
            return null;
        }, closed: db => db.Activities.AnyAsync(a => a.Id == activityId && a.IsLocked));

    public async Task<EventResult<TreasuryReportDto>> UpdateTransactionAsync(int id, TreasuryTransactionInput input, ClaimsPrincipal user) =>
        await ChangeAsync(user, db => ReportOfTransactionAsync(db, id), async _ =>
        {
            if (TransactionError(input) is { } error)
            {
                return error;
            }

            var r = input.Receipt;
            await _transactions.UpdateTransactionAsync(id, input.Date!.Value.Date, input.Description!.Trim(), input.Category!.Trim(), input.Amount!.Value,
                input.Type!, r?.Content, r?.FileName, r?.ContentType, deleteReceipt: input.RemoveReceipt && r is null);
            return null;
        });

    public async Task<EventResult<TreasuryReportDto>> DeleteTransactionAsync(int id, ClaimsPrincipal user) =>
        await ChangeAsync(user, db => ReportOfTransactionAsync(db, id), async _ =>
        {
            await _transactions.DeleteTransactionAsync(id); // removes its receipt too
            return null;
        });

    public async Task<EventResult<TreasuryHistoryDto>> GetHistoryAsync(int reportId, int page, int pageSize, ClaimsPrincipal user)
    {
        var (_, refused) = await CallerAsync(user, (_, u) => TreasuryAuthorization.CanSeeHistory(u));
        if (refused is { } status)
        {
            return EventResult<TreasuryHistoryDto>.Fail(status);
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (!await db.Reports.AnyAsync(r => r.Id == reportId))
        {
            return EventResult<TreasuryHistoryDto>.Fail(EventResultStatus.NotFound);
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var (entries, total) = await _transactions.GetTransactionHistoryForReportAsync(reportId, page, pageSize);
        return EventResult<TreasuryHistoryDto>.Ok(new TreasuryHistoryDto(
            entries.Select(e => new TreasuryHistoryEntryDto(e.Timestamp, e.ActivityName, e.TransactionDescription, e.UserName ?? "Sistema", e.Action)).ToList(),
            total, page, pageSize));
    }

    private static async Task<decimal> CalotesAsync(ApplicationDbContext db, int year) =>
        (await db.MemberDebts.AsNoTracking().Where(d => d.FiscalYear.StartYear == year).Select(d => d.AmountOwed).ToListAsync()).Sum();

    private static async Task<int?> ReportOfActivityAsync(ApplicationDbContext db, int activityId) =>
        await db.Activities.AsNoTracking().Where(a => a.Id == activityId).Select(a => (int?)a.ReportId).SingleOrDefaultAsync();

    private static async Task<int?> ReportOfTransactionAsync(ApplicationDbContext db, int transactionId) =>
        await db.Transactions.AsNoTracking().Where(t => t.Id == transactionId && t.ActivityId != null).Select(t => (int?)t.Activity!.ReportId).SingleOrDefaultAsync();

    /// <summary>
    /// A report change: managers only, the report must exist and (unless <paramref name="evenIfPublished"/>) be a draft, and
    /// <paramref name="closed"/> (a locked activity) must not hold. <paramref name="change"/> returns a (field, message)
    /// validation error or null; the result is the report as it is now.
    /// </summary>
    private async Task<EventResult<TreasuryReportDto>> ChangeAsync(ClaimsPrincipal user, Func<ApplicationDbContext, Task<int?>> reportOf,
        Func<ApplicationDbContext, Task<(string Field, string Message)?>> change, bool evenIfPublished = false,
        Func<ApplicationDbContext, Task<bool>>? closed = null)
    {
        var (me, refused) = await CallerAsync(user, TreasuryAuthorization.CanManageReports);
        if (refused is { } status)
        {
            return EventResult<TreasuryReportDto>.Fail(status);
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await reportOf(db) is not { } reportId)
        {
            return EventResult<TreasuryReportDto>.Fail(EventResultStatus.NotFound);
        }

        if ((!evenIfPublished && await db.Reports.AnyAsync(r => r.Id == reportId && r.IsPublished)) || (closed is not null && await closed(db)))
        {
            return EventResult<TreasuryReportDto>.Fail(EventResultStatus.Closed);
        }

        if (await change(db) is { } error)
        {
            return EventResult<TreasuryReportDto>.Invalid(error.Field, error.Message);
        }

        return EventResult<TreasuryReportDto>.Ok((await ReportAsync(reportId, me!, user))!);
    }
}
