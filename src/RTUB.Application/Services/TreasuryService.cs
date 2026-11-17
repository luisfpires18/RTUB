using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// The React /treasury and /treasury/reports/{id} (React track 024,
/// docs/react-treasury.md). Same tables, same totals, and the writes still go through the old services (reports,
/// activities, transactions with their receipts and audit log, bank / cash), so nothing is computed or stored differently.
/// Every rule is now checked here (<see cref="TreasuryAuthorization"/>); the old pages only hid buttons.
/// - a published report is read-only (activities can still be locked / unlocked, as before); a locked activity takes no
///   new transaction, but its transactions can still be edited or deleted (as before);
/// - amounts are euros &gt; 0 with at most two decimals; receipts are an image or a PDF ≤ 10 MB (the old page checked the
///   size only in the browser);
/// - deleting an activity or a draft report deletes it with its transactions in one database transaction (rows and their
///   audit log commit together or not at all): <c>Transactions.ActivityId</c> has no ON DELETE action, so the old delete
///   failed with a foreign-key error whenever an activity had transactions. Their receipts are removed from storage after
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
