using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// The old Blazor /requests ("Gestão de Pedidos") behind the React /requests (task 031, docs/react-requests-questions.md).
/// Same data and rules, through <see cref="IRequestService"/> and <see cref="IRequestToEventService"/>, now enforced
/// server-side (<see cref="RequestsAuthorization"/>; the old page only hid buttons):
/// - the list: every request created in one fiscal year (the current one by default, or every year), newest first,
///   searched over name, email, event type, location and phone (case and accents ignored), filtered by status, split
///   into still to answer and answered;
/// - approve and reject (Admin, Owner): only a request still to answer; approving returns the agenda's create-form link
///   prefilled from the request, as the old "Criar Evento" step did;
/// - delete (Admin, Owner): hard delete, as before.
/// No email or push is sent on approve, reject or delete (none was). No schema change.
/// </summary>
public sealed class RequestAdminService : IRequestAdminService
{
    private static readonly Dictionary<string, RequestStatus[]> StatusFilters = new()
    {
        ["pending"] = new[] { RequestStatus.Pending },
        ["confirmed"] = new[] { RequestStatus.Confirmed },
        ["rejected"] = new[] { RequestStatus.Rejected },
    };

    private readonly IRequestService _requests;
    private readonly IRequestToEventService _toEvent;
    private readonly IFiscalYearService _fiscalYears;
    private readonly IFiscalYearHelper _fiscalYearRanges;
    private readonly IDbContextFactory<ApplicationDbContext> _contexts;

    public RequestAdminService(
        IRequestService requests,
        IRequestToEventService toEvent,
        IFiscalYearService fiscalYears,
        IFiscalYearHelper fiscalYearRanges,
        IDbContextFactory<ApplicationDbContext> contexts)
    {
        _requests = requests;
        _toEvent = toEvent;
        _fiscalYears = fiscalYears;
        _fiscalYearRanges = fiscalYearRanges;
        _contexts = contexts;
    }

    public async Task<EventResult<RequestsDto>> GetAsync(string? fiscalYear, string? search, string? status, ClaimsPrincipal user)
    {
        if (await RefusalAsync(user) is { } refusal)
        {
            return EventResult<RequestsDto>.Fail(refusal);
        }

        var current = FiscalYearHelper.GetCurrentFiscalYearString();
        var years = (await _fiscalYears.GetAllFiscalYearsAsync())
            .Where(fy => fy.StartYear >= FiscalYearHelper.AppYearCreated)
            .Select(fy => fy.GetFiscalYearString())
            .Distinct()
            .OrderByDescending(y => y)
            .ToList();

        // No parameter: the current year, as the old page opened; "" (Todos os anos): every year.
        var selected = fiscalYear ?? current;
        if (selected != "" && selected != current && !years.Contains(selected))
        {
            return EventResult<RequestsDto>.Invalid("fiscalYear", "Ano inválido.");
        }

        var statusKey = status?.Trim() ?? "";
        if (statusKey != "" && !StatusFilters.ContainsKey(statusKey))
        {
            return EventResult<RequestsDto>.Invalid("status", "Estado inválido.");
        }

        IEnumerable<Request> shown = (await _requests.GetAllRequestsAsync()).ToList();
        if (selected != "" && _fiscalYearRanges.GetFiscalYearDateRange(selected) is { } range)
        {
            shown = shown.Where(r => r.CreatedAt >= range.startDate && r.CreatedAt <= range.endDate);
        }

        if (statusKey != "")
        {
            shown = shown.Where(r => StatusFilters[statusKey].Contains(r.Status));
        }

        var searcher = new SearchHelper<Request> { SearchTerm = search?.Trim() ?? "" };
        var found = searcher.FilterMultiple(shown.ToList(), new List<Func<Request, string>>
            {
                r => r.Name,
                r => r.Email,
                r => r.EventType,
                r => r.Location,
                r => r.Phone,
            })
            .OrderByDescending(r => r.CreatedAt)
            .ToList();

        return EventResult<RequestsDto>.Ok(new RequestsDto(
            years.Select(y => new MemberOptionDto(y, y == current ? $"{y} (ATUAL)" : y)).ToList(),
            selected,
            found.Where(IsOpen).Select(ToDto).ToList(),
            found.Where(r => !IsOpen(r)).Select(ToDto).ToList(),
            RequestsAuthorization.CanManage(user)));
    }

    public async Task<EventResult<RequestApprovedDto>> ApproveAsync(int id, ClaimsPrincipal user)
    {
        var (refusal, request) = await AnswerableAsync(id, user);
        if (refusal is { } status)
        {
            return status == EventResultStatus.Invalid
                ? EventResult<RequestApprovedDto>.Invalid("status", AlreadyAnswered)
                : EventResult<RequestApprovedDto>.Fail(status);
        }

        await _requests.UpdateRequestStatusAsync(id, RequestStatus.Confirmed);
        request!.UpdateStatus(RequestStatus.Confirmed);
        return EventResult<RequestApprovedDto>.Ok(new RequestApprovedDto(ToDto(request), CreateEventUrl(request)));
    }

    public async Task<EventResult<RequestItemDto>> RejectAsync(int id, ClaimsPrincipal user)
    {
        var (refusal, request) = await AnswerableAsync(id, user);
        if (refusal is { } status)
        {
            return status == EventResultStatus.Invalid
                ? EventResult<RequestItemDto>.Invalid("status", AlreadyAnswered)
                : EventResult<RequestItemDto>.Fail(status);
        }

        await _requests.UpdateRequestStatusAsync(id, RequestStatus.Rejected);
        request!.UpdateStatus(RequestStatus.Rejected);
        return EventResult<RequestItemDto>.Ok(ToDto(request));
    }

    public async Task<EventResult<bool>> DeleteAsync(int id, ClaimsPrincipal user)
    {
        if (await ManagerRefusalAsync(user) is { } refusal)
        {
            return EventResult<bool>.Fail(refusal);
        }

        if (await _requests.GetRequestByIdAsync(id) is null)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        await _requests.DeleteRequestAsync(id);
        return EventResult<bool>.Ok(true);
    }

    // ---------- helpers ----------

    private const string AlreadyAnswered = "Este pedido já foi respondido.";

    /// <summary>Still to answer: pending, or in analysis (only seeded data uses it; the old page showed it nowhere).</summary>
    private static bool IsOpen(Request r) => r.Status is RequestStatus.Pending or RequestStatus.Analysing;

    /// <summary>Signed in (401) and not a Leitão unless Admin or Owner (403), checked against the stored member.</summary>
    private async Task<EventResultStatus?> RefusalAsync(ClaimsPrincipal user)
    {
        if (RequestsAuthorization.UserId(user) is not { } id)
        {
            return EventResultStatus.SignInRequired;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id) is not { } member)
        {
            return EventResultStatus.SignInRequired;
        }

        return RequestsAuthorization.CanOpen(member, user) ? null : EventResultStatus.Forbidden;
    }

    private async Task<EventResultStatus?> ManagerRefusalAsync(ClaimsPrincipal user)
    {
        if (await RefusalAsync(user) is { } refusal)
        {
            return refusal;
        }

        return RequestsAuthorization.CanManage(user) ? null : EventResultStatus.Forbidden;
    }

    /// <summary>Admin or Owner, an existing request, and one still to answer (Invalid otherwise).</summary>
    private async Task<(EventResultStatus? Refusal, Request? Request)> AnswerableAsync(int id, ClaimsPrincipal user)
    {
        if (await ManagerRefusalAsync(user) is { } refusal)
        {
            return (refusal, null);
        }

        if (await _requests.GetRequestByIdAsync(id) is not { } request)
        {
            return (EventResultStatus.NotFound, null);
        }

        if (!IsOpen(request))
        {
            return (EventResultStatus.Invalid, request);
        }

        return (null, request);
    }

    /// <summary>The React agenda's create form (/events?openModal=true&amp;...; Events.tsx takeCreatePrefill).</summary>
    private string CreateEventUrl(Request request)
    {
        var query = _toEvent.CreateEventNavigationParameters(request)
            .Where(p => p.Value is not null)
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(Convert.ToString(p.Value, System.Globalization.CultureInfo.InvariantCulture)!)}");
        return "/events?" + string.Join("&", query);
    }

    private static RequestItemDto ToDto(Request r) => new(
        r.Id,
        r.Name,
        r.Email,
        r.Phone,
        r.EventType,
        r.PreferredDate.ToString("yyyy-MM-dd"),
        r.IsDateRange && r.PreferredEndDate is { } endDate ? endDate.ToString("yyyy-MM-dd") : null,
        r.Location,
        r.Message,
        r.Status switch
        {
            RequestStatus.Analysing => "analysing",
            RequestStatus.Confirmed => "confirmed",
            RequestStatus.Rejected => "rejected",
            _ => "pending",
        },
        r.CreatedAt);
}
