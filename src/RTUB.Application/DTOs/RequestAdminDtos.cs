namespace RTUB.Application.DTOs;

// Contracts of the React /requests (task 031; was the Blazor "Gestão de Pedidos"). Signed-in members except Leitões,
// as before; the contact details are the ones the requester typed into the public form.

/// <summary>
/// The requests of one fiscal year ("" = every year; by creation date), newest first, split as the old page did:
/// <c>Pending</c> (still to answer: pending or in analysis) and <c>Answered</c> (confirmed or rejected).
/// <c>CanManage</c>: Admin or Owner (approve, reject, delete).
/// </summary>
public sealed record RequestsDto(
    IReadOnlyList<MemberOptionDto> FiscalYears,
    string FiscalYear,
    IReadOnlyList<RequestItemDto> Pending,
    IReadOnlyList<RequestItemDto> Answered,
    bool CanManage);

/// <summary>Dates are yyyy-MM-dd; <c>CreatedAt</c> is UTC (ISO 8601). Status: pending, analysing, confirmed, rejected.</summary>
public sealed record RequestItemDto(
    int Id,
    string Name,
    string Email,
    string Phone,
    string EventType,
    string PreferredDate,
    string? PreferredEndDate,
    string Location,
    string Message,
    string Status,
    DateTime CreatedAt);

/// <summary>
/// After approving: the request, and the React agenda's create-form link prefilled from it (name "EventType em
/// Location", place, date, "Pedido de Name"), as the old page offered right after approval.
/// </summary>
public sealed record RequestApprovedDto(RequestItemDto Request, string CreateEventUrl);
