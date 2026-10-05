using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Constants;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// The old Blazor /finance and /finance/report/{id} behind the React /treasury and /treasury/reports/{id} (React track 024,
/// docs/react-treasury.md). Same tables, same totals, and the writes still go through the old services (reports,
/// activities, transactions with their receipts and audit log, bank / cash), so nothing is computed or stored differently.
/// Every rule is now checked here (<see cref="TreasuryAuthorization"/>); the old pages only hid buttons.
/// - a published report is read-only (activities can still be locked / unlocked, as before); a locked activity takes no
///   new transaction, but its transactions can still be edited or deleted (as before);
/// - amounts are euros &gt; 0 with at most two decimals; receipts are an image or a PDF ≤ 10 MB (the old page checked the
///   size only in the browser);
/// - deleting an activity or a draft report deletes its transactions first, each through the old service (receipt and
///   audit log included): <c>Transactions.ActivityId</c> has no ON DELETE action, so the old delete failed with a foreign-key
///   error whenever an activity had transactions.
/// No notifications, no schema change.
/// </summary>
public sealed class TreasuryService : ITreasuryService
{
    public const long MaxReceiptBytes = 10 * 1024 * 1024;
    public const int FirstYear = 1991;
    private const string Bank = "DINHEIRO NO BANCO";
    private const string Cash = "DINHEIRO EM CAIXA";

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly IReportService _reports;
    private readonly IActivityService _activities;
    private readonly ITransactionService _transactions;
    private readonly IFinanceManagementService _finance;
    private readonly ReportPdfService _pdf;

    public TreasuryService(IDbContextFactory<ApplicationDbContext> contexts, IReportService reports, IActivityService activities,
        ITransactionService transactions, IFinanceManagementService finance, ReportPdfService pdf)
    {
        _contexts = contexts;
        _reports = reports;
        _activities = activities;
        _transactions = transactions;
        _finance = finance;
        _pdf = pdf;
    }

    // ---------------------------------------------------------------- reports

    public async Task<EventResult<TreasuryReportsDto>> GetReportsAsync(ClaimsPrincipal user)
    {
        var (me, refused) = await CallerAsync(user);
        if (refused is { } status)
        {
            return EventResult<TreasuryReportsDto>.Fail(status);
        }

        await using var db = await _contexts.CreateDbContextAsync();
        var reports = await db.Reports.AsNoTracking()
            .Select(r => new { r.Id, r.Title, r.Year, r.IsPublished, r.PublishedAt })
            .ToListAsync();
        var activities = await db.Activities.AsNoTracking().Include(a => a.Transactions).ToListAsync();
        var canCreate = TreasuryAuthorization.CanManageReports(me!, user);
        var taken = reports.Select(r => r.Year).ToHashSet();

        return EventResult<TreasuryReportsDto>.Ok(new TreasuryReportsDto(
            reports.OrderByDescending(r => r.Year)
                .Select(r => Summary(r.Id, r.Title, r.Year, r.IsPublished, r.PublishedAt, activities.Where(a => a.ReportId == r.Id)))
                .ToList(),
            canCreate,
            TreasuryAuthorization.CanPublish(me!, user),
            canCreate ? Years().Where(y => !taken.Contains(y)).ToList() : Array.Empty<int>()));
    }

    public async Task<EventResult<TreasuryReportSummaryDto>> CreateReportAsync(TreasuryReportInput input, ClaimsPrincipal user)
    {
        var (_, refused) = await CallerAsync(user, TreasuryAuthorization.CanManageReports);
        if (refused is { } status)
        {
            return EventResult<TreasuryReportSummaryDto>.Fail(status);
        }

        if (input.Year is not { } year || !Years().Contains(year))
        {
            return EventResult<TreasuryReportSummaryDto>.Invalid("year", "Escolha um ano letivo válido (entre 1991 e o ano atual).");
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await db.Reports.AnyAsync(r => r.Year == year))
        {
            return EventResult<TreasuryReportSummaryDto>.Invalid("year", $"Já existe um relatório para o ano letivo {year}-{year + 1}.");
        }

        var report = await _reports.CreateReportAsync($"Relatório de Contas {year} - {year + 1}", year);
        return EventResult<TreasuryReportSummaryDto>.Ok(Summary(report.Id, report.Title, report.Year, false, null, Array.Empty<Activity>()));
    }

    public async Task<EventResult<TreasuryReportSummaryDto>> PublishReportAsync(int id, ClaimsPrincipal user)
    {
        var (_, refused) = await CallerAsync(user, TreasuryAuthorization.CanPublish);
        if (refused is { } status)
        {
            return EventResult<TreasuryReportSummaryDto>.Fail(status);
        }

        if (await _reports.GetReportByIdAsync(id) is not { } report)
        {
            return EventResult<TreasuryReportSummaryDto>.Fail(EventResultStatus.NotFound);
        }

        if (report.IsPublished)
        {
            return EventResult<TreasuryReportSummaryDto>.Fail(EventResultStatus.Closed);
        }

        await _reports.PublishReportAsync(id);
        report = (await _reports.GetReportByIdAsync(id))!;
        return EventResult<TreasuryReportSummaryDto>.Ok(Summary(report.Id, report.Title, report.Year, report.IsPublished, report.PublishedAt, report.Activities));
    }

    public async Task<EventResult<bool>> DeleteReportAsync(int id, ClaimsPrincipal user)
    {
        var (_, refused) = await CallerAsync(user, TreasuryAuthorization.CanPublish);
        if (refused is { } status)
        {
            return EventResult<bool>.Fail(status);
        }

        if (await _reports.GetReportByIdAsync(id) is not { } report)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        if (report.IsPublished)
        {
            return EventResult<bool>.Fail(EventResultStatus.Closed);
        }

        await DeleteTransactionsAsync(report.Activities.SelectMany(a => a.Transactions));
        await _reports.DeleteReportAsync(id); // its activities cascade
        return EventResult<bool>.Ok(true);
    }

    public async Task<EventResult<TreasuryReportDto>> GetReportAsync(int id, ClaimsPrincipal user)
    {
        var (me, refused) = await CallerAsync(user);
        if (refused is { } status)
        {
            return EventResult<TreasuryReportDto>.Fail(status);
        }

        return await ReportAsync(id, me!, user) is { } report
            ? EventResult<TreasuryReportDto>.Ok(report)
            : EventResult<TreasuryReportDto>.Fail(EventResultStatus.NotFound);
    }

    public async Task<EventResult<TreasuryFileDto>> GetReportPdfAsync(int id, ClaimsPrincipal user)
    {
        var (_, refused) = await CallerAsync(user);
        if (refused is { } status)
        {
            return EventResult<TreasuryFileDto>.Fail(status);
        }

        if (await _reports.GetReportByIdAsync(id) is not { } report)
        {
            return EventResult<TreasuryFileDto>.Fail(EventResultStatus.NotFound);
        }

        // As the old download: activities by name, each with its transactions newest first, plus the year's calotes.
        var activities = report.Activities.OrderBy(a => a.Name).ToList();
        var transactions = activities.Select(a => (a, a.Transactions.OrderByDescending(t => t.Date).ToList())).ToList();
        await using var db = await _contexts.CreateDbContextAsync();
        var pdf = _pdf.GenerateReportPdf(report, activities, transactions, await CalotesAsync(db, report.Year));
        return EventResult<TreasuryFileDto>.Ok(new TreasuryFileDto(pdf, $"Relatorio_{report.Year}_{DateTime.Now:yyyyMMdd}.pdf"));
    }

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
            async _ =>
            {
                if (ActivityError(input) is { } error)
                {
                    return error;
                }

                await _activities.CreateActivityAsync(reportId, input.Name!.Trim(), input.StartDate!.Value.Date, Text(input.Description), input.EndDate?.Date);
                return null;
            });

    public async Task<EventResult<TreasuryReportDto>> UpdateActivityAsync(int id, TreasuryActivityInput input, ClaimsPrincipal user) =>
        await ChangeAsync(user, db => ReportOfActivityAsync(db, id), async _ =>
        {
            if (ActivityError(input) is { } error)
            {
                return error;
            }

            await _activities.UpdateActivityAsync(id, input.Name!.Trim(), input.StartDate!.Value.Date, Text(input.Description), input.EndDate?.Date);
            return null;
        });

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
        await ChangeAsync(user, db => ReportOfActivityAsync(db, id), async db =>
        {
            await DeleteTransactionsAsync(await db.Transactions.AsNoTracking().Where(t => t.ActivityId == id).ToListAsync());
            await _activities.DeleteActivityAsync(id);
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

    // ---------------------------------------------------------------- helpers

    /// <summary>The fiscal start years a report can have: the current one back to 1991.</summary>
    private static IEnumerable<int> Years()
    {
        for (var y = FiscalYearHelper.GetCurrentFiscalYearStartYear(); y >= FirstYear; y--)
        {
            yield return y;
        }
    }

    private static TreasuryReportSummaryDto Summary(int id, string title, int year, bool published, DateTime? publishedAt, IEnumerable<Activity> activities)
    {
        var counted = activities.Where(a => !a.IsHiddenFromCalculations()).SelectMany(a => a.Transactions).ToList();
        var income = counted.Where(t => t.Type == TransactionTypes.Income).Sum(t => t.Amount);
        var expenses = counted.Where(t => t.Type == TransactionTypes.Expense).Sum(t => t.Amount);
        return new TreasuryReportSummaryDto(id, title, year, published, publishedAt, year == FiscalYearHelper.GetCurrentFiscalYearStartYear(),
            income, expenses, income - expenses);
    }

    private async Task<TreasuryReportDto?> ReportAsync(int id, ApplicationUser me, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        if (await db.Reports.AsNoTracking().Where(r => r.Id == id).Select(r => new { r.Id, r.Title, r.Year, r.Summary, r.IsPublished }).SingleOrDefaultAsync()
            is not { } report)
        {
            return null;
        }

        // The old page's order for the bank / cash lookup: latest date first.
        var activities = (await db.Activities.AsNoTracking().Include(a => a.Transactions).Where(a => a.ReportId == id).ToListAsync())
            .OrderByDescending(a => a.EndDate ?? a.StartDate).ToList();
        var counted = activities.Where(a => !a.IsHiddenFromCalculations()).ToList();
        var income = counted.Sum(a => a.TotalIncome);
        var expenses = counted.Sum(a => a.TotalExpenses);
        var bank = activities.FirstOrDefault(a => a.Name.Contains("BANCO", StringComparison.OrdinalIgnoreCase))?.Balance ?? 0;
        var cash = activities.FirstOrDefault(a => a.Name.Contains("CAIXA", StringComparison.OrdinalIgnoreCase))?.Balance ?? 0;

        return new TreasuryReportDto(report.Id, report.Title, report.Year, report.Summary, report.IsPublished,
            new TreasuryTotalsDto(bank + cash, bank, cash, await CalotesAsync(db, report.Year), income, expenses, income - expenses),
            activities.Where(a => !a.IsHiddenFromActivityList())
                .OrderBy(a => a.StartDate).ThenBy(a => a.EndDate ?? a.StartDate).ThenBy(a => a.Id)
                .Select(a => new TreasuryActivityDto(a.Id, a.Name, a.Description, a.StartDate, a.EndDate, a.IsLocked, a.TotalIncome, a.TotalExpenses, a.Balance,
                    a.Transactions.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id)
                        .Select(t => new TreasuryTransactionDto(t.Id, t.Date, t.Description, t.Category, t.Amount, t.Type, t.ReceiptUrl))
                        .ToList()))
                .ToList(),
            TreasuryAuthorization.CanManageReports(me, user),
            TreasuryAuthorization.CanPublish(me, user),
            TreasuryAuthorization.CanSeeHistory(user));
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

    private static (string, string)? ActivityError(TreasuryActivityInput input)
    {
        var name = input.Name?.Trim() ?? "";
        if (name.Length == 0)
        {
            return ("name", "O nome da atividade é obrigatório.");
        }

        if (name.Length > 200)
        {
            return ("name", "O nome da atividade não pode exceder 200 caracteres.");
        }

        if (input.StartDate is null)
        {
            return ("startDate", "A data da atividade é obrigatória.");
        }

        if (input.EndDate is { } end && end.Date < input.StartDate.Value.Date)
        {
            return ("endDate", "A data de fim não pode ser anterior à data de início.");
        }

        return input.Description?.Trim().Length > 1000 ? ("description", "A descrição não pode exceder 1000 caracteres.") : null;
    }

    private static (string, string)? TransactionError(TreasuryTransactionInput input)
    {
        if (input.Date is null)
        {
            return ("date", "A data é obrigatória.");
        }

        var description = input.Description?.Trim() ?? "";
        if (description.Length is 0 or > 500)
        {
            return ("description", description.Length == 0 ? "A descrição é obrigatória." : "A descrição não pode exceder 500 caracteres.");
        }

        var category = input.Category?.Trim() ?? "";
        if (category.Length is 0 or > 100)
        {
            return ("category", category.Length == 0 ? "A categoria é obrigatória." : "A categoria não pode exceder 100 caracteres.");
        }

        if (input.Amount is not { } amount || amount <= 0 || decimal.Round(amount, 2) != amount)
        {
            return ("amount", "O valor deve ser maior que 0, com até duas casas decimais.");
        }

        if (input.Type is not (TransactionTypes.Income or TransactionTypes.Expense))
        {
            return ("type", "Escolha receita ou despesa.");
        }

        return input.Receipt is { } r ? ReceiptError(r) : null;
    }

    internal static (string, string)? ReceiptError(TreasuryReceiptUpload receipt)
    {
        if (receipt.Length > MaxReceiptBytes)
        {
            return ("receipt", "O ficheiro é demasiado grande. O tamanho máximo é 10MB.");
        }

        var type = receipt.ContentType.ToLowerInvariant();
        return receipt.Length == 0 || !(type == "application/pdf" || type.StartsWith("image/", StringComparison.Ordinal))
            ? ("receipt", "O recibo tem de ser uma imagem (JPG, PNG, etc.) ou um PDF.")
            : null;
    }

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // ponytail: one transaction at a time (receipt + audit log each), not atomic; a failure midway leaves the rest to delete again.
    private async Task DeleteTransactionsAsync(IEnumerable<Transaction> transactions)
    {
        foreach (var id in transactions.Select(t => t.Id).ToList())
        {
            await _transactions.DeleteTransactionAsync(id); // its receipt too (refused, and logged, if this environment does not own it)
        }
    }

    /// <summary>The caller's account, or 401 (visitor, deleted account) / 403 (not a treasury member, or not allowed).</summary>
    private async Task<(ApplicationUser? Member, EventResultStatus? Refused)> CallerAsync(ClaimsPrincipal user,
        Func<ApplicationUser, ClaimsPrincipal, bool>? allowed = null)
    {
        if (TreasuryAuthorization.UserId(user) is not { } id)
        {
            return (null, EventResultStatus.SignInRequired);
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id) is not { } member)
        {
            return (null, EventResultStatus.SignInRequired);
        }

        return TreasuryAuthorization.CanView(member, user) && (allowed is null || allowed(member, user))
            ? (member, null)
            : (null, EventResultStatus.Forbidden);
    }
}
