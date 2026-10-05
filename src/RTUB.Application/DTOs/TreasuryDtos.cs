namespace RTUB.Application.DTOs;

// Contracts of the React /treasury pages ("Tesouraria", React track 024; were the Blazor Finance, Report, Calotes,
// MbwayTransfers, Nerba and NerbaOrderDetail pages). Signed-in members only; who sees what is in TreasuryAuthorization.
// No entity leaves the server. A member id reaches only a manager who needs it to change a record; members see names
// and avatars. Amounts are euros as stored (decimal, no rounding).

public sealed record TreasuryPersonDto(string? Id, string DisplayName, string? FullName, string? AvatarUrl);

// ---------------------------------------------------------------- reports

/// <summary>A report card on /treasury. <c>Year</c> is the fiscal start year (2025 = 2025-2026).</summary>
public sealed record TreasuryReportSummaryDto(int Id, string Title, int Year, bool IsPublished, DateTime? PublishedAt, bool IsCurrentYear,
    decimal TotalIncome, decimal TotalExpenses, decimal Balance);

/// <summary>
/// /treasury. <c>CanCreate</c>: Admin, Owner, or a Mod of the treasury team; <c>AvailableYears</c> (only for them): the
/// fiscal start years without a report, newest first. <c>CanPublish</c>: Owner or the treasury team (publish, delete drafts).
/// </summary>
public sealed record TreasuryReportsDto(IReadOnlyList<TreasuryReportSummaryDto> Reports, bool CanCreate, bool CanPublish, IReadOnlyList<int> AvailableYears);

public sealed record TreasuryReportInput(int? Year);

public sealed record TreasuryTotalsDto(decimal TotalMoney, decimal Bank, decimal Cash, decimal Calotes, decimal Income, decimal Expenses, decimal Balance);

public sealed record TreasuryTransactionDto(int Id, DateTime Date, string Description, string Category, decimal Amount, string Type, string? ReceiptUrl);

public sealed record TreasuryActivityDto(int Id, string Name, string? Description, DateTime StartDate, DateTime? EndDate, bool IsLocked,
    decimal Income, decimal Expenses, decimal Balance, IReadOnlyList<TreasuryTransactionDto> Transactions);

/// <summary>
/// /treasury/reports/{id}. Activities in date order without the BANCO / CAIXA / CALOTES ones; transactions newest first.
/// <c>CanManage</c>: as <see cref="TreasuryReportsDto.CanCreate"/> (a published report stays read-only except lock / unlock);
/// <c>CanSeeHistory</c>: Admin and Owner.
/// </summary>
public sealed record TreasuryReportDto(int Id, string Title, int Year, string? Summary, bool IsPublished, TreasuryTotalsDto Totals,
    IReadOnlyList<TreasuryActivityDto> Activities, bool CanManage, bool CanPublish, bool CanSeeHistory);

/// <summary>"Dinheiro no Banco" (<c>Kind</c> = "bank") or "Dinheiro em Caixa" ("cash"); any sign, 0 clears it.</summary>
public sealed record TreasuryBalanceInput(string? Kind, decimal? Value);

public sealed record TreasuryActivityInput(string? Name, DateTime? StartDate, DateTime? EndDate, string? Description);

public sealed record TreasuryLockInput(bool Locked);

/// <summary>A transaction form as the endpoint reads it from multipart fields, with an optional receipt.</summary>
public sealed record TreasuryTransactionInput(DateTime? Date, string? Description, string? Category, decimal? Amount, string? Type, bool RemoveReceipt,
    TreasuryReceiptUpload? Receipt);

public sealed record TreasuryReceiptUpload(Stream Content, string FileName, string ContentType, long Length);

public sealed record TreasuryHistoryEntryDto(DateTime Timestamp, string ActivityName, string Description, string UserName, string Action);

public sealed record TreasuryHistoryDto(IReadOnlyList<TreasuryHistoryEntryDto> Entries, int Total, int Page, int PageSize);

public sealed record TreasuryFileDto(byte[] Content, string FileName);

// ---------------------------------------------------------------- calotes

public sealed record TreasuryDebtDto(int Id, decimal Amount, string? Description);

/// <summary>One member's debts of the year; <c>Member.Id</c> only for managers.</summary>
public sealed record TreasuryDebtGroupDto(TreasuryPersonDto Member, decimal Total, DateTime? CompromisedUntil, IReadOnlyList<TreasuryDebtDto> Debts);

/// <summary>
/// /treasury/calotes. <c>Scope</c> "all" (treasury members: everyone's debts) or "own" (Caloiros and Leitões: theirs only).
/// <c>CanManage</c>: Mod, Admin, Owner.
/// </summary>
public sealed record TreasuryCalotesDto(IReadOnlyList<string> FiscalYears, string? FiscalYear, string Scope, decimal Total,
    IReadOnlyList<TreasuryDebtGroupDto> Groups, bool CanManage);

public sealed record TreasuryDebtInput(string? FiscalYear, string? UserId, decimal? Amount, string? Description);

public sealed record TreasuryCommitmentInput(string? FiscalYear, string? UserId, DateTime? Until);

// ---------------------------------------------------------------- MBWay

public sealed record TreasuryTransferDto(int Id, DateTime Date, decimal Amount, string TransferTo, string? TransferFrom, string? Phone,
    string? Description, string? CreatedBy, string? UpdatedBy, DateTime? UpdatedAt, TreasuryPersonDto? Member);

public sealed record TreasuryTransferTotalDto(string Name, decimal Amount);

/// <summary>/treasury/mbway, newest first. <c>CanAdd</c>: Mod, Admin, Owner; <c>CanEdit</c> (edit, delete): Owner.</summary>
public sealed record TreasuryTransfersDto(IReadOnlyList<TreasuryTransferDto> Transfers, IReadOnlyList<TreasuryTransferTotalDto> Totals, bool CanAdd, bool CanEdit);

public sealed record TreasuryTransferInput(DateTime? Date, decimal? Amount, string? MemberUserId, string? TransferFrom, string? Phone, string? Description);

// ---------------------------------------------------------------- Nerba

public sealed record TreasuryNerbaEventDto(int Id, string Name, DateTime Date, DateTime? EndDate, string? Location);

public sealed record TreasuryNerbaSummaryDto(TreasuryNerbaEventDto Event, int Count, decimal Total);

/// <summary>/treasury/nerba: Nerba events, upcoming (nearest first) and past (latest first). <c>CanManage</c>: Mod, Admin, Owner.</summary>
public sealed record TreasuryNerbaDto(IReadOnlyList<TreasuryNerbaSummaryDto> Upcoming, IReadOnlyList<TreasuryNerbaSummaryDto> Past, bool CanManage);

public sealed record TreasuryNerbaOrderDto(int Id, string Item, string? Type, int Stock, decimal PricePerUnit, decimal Total, DateTime? OrderDate);

/// <summary>/treasury/nerba/{eventId}. <c>Days</c>: every day of a multi-day event (empty for one day).</summary>
public sealed record TreasuryNerbaOrdersDto(TreasuryNerbaEventDto Event, IReadOnlyList<DateTime> Days, IReadOnlyList<TreasuryNerbaOrderDto> Orders, bool CanManage);

public sealed record TreasuryNerbaOrderInput(string? Item, string? Type, int? Stock, decimal? PricePerUnit, DateTime? OrderDate);
