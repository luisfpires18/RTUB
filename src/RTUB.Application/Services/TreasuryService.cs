using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// The React /treasury and /treasury/reports/{id}
/// (docs/react-treasury.md). The writes go through the domain services (reports,
/// activities, transactions with their receipts and audit log, bank / cash).
/// Every rule is checked here (<see cref="TreasuryAuthorization"/>).
/// - a published report is read-only (activities can still be locked / unlocked); a locked activity takes no
///   new transaction, but its transactions can still be edited or deleted;
/// - amounts are euros &gt; 0 with at most two decimals; receipts are an image or a PDF ≤ 10 MB;
/// - deleting an activity or a draft report deletes it with its transactions in one database transaction (rows and their
///   audit log commit together or not at all): <c>Transactions.ActivityId</c> has no ON DELETE action, so a plain delete
///   would fail with a foreign-key error whenever an activity has transactions. Their receipts are removed from storage after
///   the commit, best-effort.
/// No notifications, no schema change.
/// </summary>
public sealed partial class TreasuryService : ITreasuryService
{
    private readonly TimeProvider _time;

    public const long MaxReceiptBytes = 10 * 1024 * 1024;
    public const int FirstYear = 1991;

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly IReportService _reports;
    private readonly IActivityService _activities;
    private readonly ITransactionService _transactions;
    private readonly IFinanceManagementService _finance;
    private readonly IReceiptStorageService _receipts;
    private readonly ReportPdfService _pdf;

    public TreasuryService(IDbContextFactory<ApplicationDbContext> contexts, IReportService reports, IActivityService activities,
        ITransactionService transactions, IFinanceManagementService finance, IReceiptStorageService receipts, ReportPdfService pdf,
        TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
        _contexts = contexts;
        _reports = reports;
        _activities = activities;
        _transactions = transactions;
        _finance = finance;
        _receipts = receipts;
        _pdf = pdf;
    }
}
