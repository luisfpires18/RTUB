// Treasury: reports list, create, publish, delete, read and PDF.
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

    private static readonly SemaphoreSlim ReportCreation = new(1, 1);

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

        // Task 074: the check and the insert under one lock, so two treasurers (this page and "As minhas ações pendentes")
        // clicking at once make one report; the loser gets the "already exists" answer.
        // ponytail: process-wide lock, single App Service instance; a unique index on Reports.Year if it ever scales out.
        await ReportCreation.WaitAsync();
        try
        {
            await using var db = await _contexts.CreateDbContextAsync();
            if (await db.Reports.AnyAsync(r => r.Year == year))
            {
                return EventResult<TreasuryReportSummaryDto>.Invalid("year", $"Já existe um relatório para o ano letivo {year}-{year + 1}.");
            }

            var report = await _reports.CreateReportAsync($"Relatório de Contas {year} - {year + 1}", year);
            return EventResult<TreasuryReportSummaryDto>.Ok(Summary(report.Id, report.Title, report.Year, false, null, Array.Empty<Activity>()));
        }
        finally
        {
            ReportCreation.Release();
        }
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

        await DeleteWithTransactionsAsync(db => db.Transactions.Where(t => t.Activity!.ReportId == id), async db =>
            (await db.Activities.Where(a => a.ReportId == id).ToListAsync()).Cast<object>().Concat(await db.Reports.Where(r => r.Id == id).ToListAsync()));
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
        return EventResult<TreasuryFileDto>.Ok(new TreasuryFileDto(pdf, $"Relatorio_{report.Year}_{PortugalTime.Today(_time):yyyyMMdd}.pdf"));
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
        // Task 074: the events this report's activities may account for (not cancelled, starting in its fiscal year).
        var (fyFrom, fyTo) = (new DateTime(report.Year, 9, 1), new DateTime(report.Year + 1, 9, 1));
        var events = (await db.Events.AsNoTracking().Where(e => !e.IsCancelled && e.Date >= fyFrom && e.Date < fyTo)
                .OrderBy(e => e.Date).ThenBy(e => e.Id)
                .Select(e => new TreasuryEventOptionDto(e.Id, e.Name, e.Date, e.EndDate)).ToListAsync())
            .ToDictionary(e => e.Id);
        TreasuryEventOptionDto? Linked(Activity a) => a.EventId is { } eventId ? events.GetValueOrDefault(eventId) : null;
        var bank = activities.FirstOrDefault(a => a.Name.Contains("BANCO", StringComparison.OrdinalIgnoreCase))?.Balance ?? 0;
        var cash = activities.FirstOrDefault(a => a.Name.Contains("CAIXA", StringComparison.OrdinalIgnoreCase))?.Balance ?? 0;

        return new TreasuryReportDto(report.Id, report.Title, report.Year, report.Summary, report.IsPublished,
            new TreasuryTotalsDto(bank + cash, bank, cash, await CalotesAsync(db, report.Year), income, expenses, income - expenses),
            activities.Where(a => !a.IsHiddenFromActivityList())
                .OrderBy(a => a.StartDate).ThenBy(a => a.EndDate ?? a.StartDate).ThenBy(a => a.Id)
                .Select(a => new TreasuryActivityDto(a.Id, a.Name, a.Description, a.StartDate, a.EndDate, a.IsLocked, a.TotalIncome, a.TotalExpenses, a.Balance,
                    a.Transactions.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id)
                        .Select(t => new TreasuryTransactionDto(t.Id, t.Date, t.Description, t.Category, t.Amount, t.Type, t.ReceiptUrl))
                        .ToList(),
                    Linked(a)?.Id, Linked(a)?.Name))
                .ToList(),
            TreasuryAuthorization.CanManageReports(me, user),
            TreasuryAuthorization.CanPublish(me, user),
            TreasuryAuthorization.CanSeeHistory(user),
            TreasuryAuthorization.CanManageReports(me, user) ? events.Values.ToList() : null);
    }
}
