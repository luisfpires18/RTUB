using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// The management side of the React /rehearsals pages (React track 014, docs/react-rehearsals.md).
/// Same rules, now server-side and for Admin or Owner (was IsInRole("Admin") only, in the UI):
/// - create one rehearsal (a date already taken is refused) or every Tuesday and Thursday of a range (≤90 days,
///   not starting in the past; taken dates skipped); 21:30-00:00 as always;
/// - edit location, theme, description and notes; delete (presenças cascade);
/// - cancel with a reason (presenças deleted, no email) and reactivate an upcoming one;
/// - push a notice about an upcoming rehearsal to subscribed members (optionally Leitões and Caloiros), audited;
/// - once a rehearsal can be approved: approve a pending presença, remove one, add a member (approved).
/// Changed on purpose: description and notes typed when creating are now saved (the old form dropped them).
/// </summary>
public sealed partial class RehearsalAdminService : IRehearsalAdminService
{
    private readonly TimeProvider _time;

    private DateTime Today => PortugalTime.Today(_time);

    public const int LocationMax = 200;

    public const int ThemeMax = 500;

    public const int TextMax = 1000;

    public const int NoticeMax = 500;

    public const int RangeMaxDays = 90;

    public const int SearchLimit = 20;

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;

    private readonly IRehearsalService _rehearsals;

    private readonly IRehearsalAttendanceService _attendance;

    private readonly IMemberInstrumentService _instruments;

    private readonly IPushNotificationService _push;

    private readonly IPushNotificationFactory _pushFactory;

    private readonly IAuditLogService _audit;

    private readonly ILogger<RehearsalAdminService> _logger;

    public RehearsalAdminService(
        IDbContextFactory<ApplicationDbContext> contexts,
        IRehearsalService rehearsals,
        IRehearsalAttendanceService attendance,
        IMemberInstrumentService instruments,
        IPushNotificationService push,
        IPushNotificationFactory pushFactory,
        IAuditLogService audit,
        ILogger<RehearsalAdminService> logger,
        TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
        _contexts = contexts;
        _rehearsals = rehearsals;
        _attendance = attendance;
        _instruments = instruments;
        _push = push;
        _pushFactory = pushFactory;
        _audit = audit;
        _logger = logger;
    }

    public async Task<EventResult<RehearsalSummaryDto>> CreateAsync(RehearsalInput input, ClaimsPrincipal user)
    {
        if (EditRefusal<RehearsalSummaryDto>(user) is { } refused)
        {
            return refused;
        }

        var errors = DetailErrors(input);
        var date = ParseDate(input.Date);
        if (date is null)
        {
            errors["date"] = new[] { "Indique a data." };
        }
        else if (await _rehearsals.GetRehearsalByDateAsync(date.Value) is not null)
        {
            errors["date"] = new[] { $"Já existe um ensaio marcado para {date.Value:dd/MM/yyyy}." };
        }

        if (errors.Count > 0)
        {
            return new EventResult<RehearsalSummaryDto>(EventResultStatus.Invalid, Errors: errors);
        }

        var created = await _rehearsals.CreateRehearsalAsync(date!.Value, input.Location!.Trim(), RehearsalAgendaService.Blank(input.Theme));
        if (RehearsalAgendaService.Blank(input.Description) is not null || RehearsalAgendaService.Blank(input.Notes) is not null)
        {
            await SaveDetailsAsync(created.Id, input);
        }

        return EventResult<RehearsalSummaryDto>.Ok(await SummaryAsync(created.Id));
    }

    public async Task<EventResult<RehearsalRangeResultDto>> CreateRangeAsync(RehearsalRangeInput input, ClaimsPrincipal user)
    {
        if (EditRefusal<RehearsalRangeResultDto>(user) is { } refused)
        {
            return refused;
        }

        var errors = DetailErrors(new RehearsalInput(null, input.Location, input.Theme, input.Description, null));
        var from = ParseDate(input.From);
        var to = ParseDate(input.To);
        if (from is null)
        {
            errors["from"] = new[] { "Indique a data de início." };
        }
        else if (from < Today)
        {
            errors["from"] = new[] { "A data de início não pode ser anterior a hoje." };
        }

        if (to is null)
        {
            errors["to"] = new[] { "Indique a data de fim." };
        }
        else if (from is not null && to < from)
        {
            errors["to"] = new[] { "A data de fim não pode ser anterior à data de início." };
        }
        else if (from is not null && (to.Value - from.Value).TotalDays > RangeMaxDays)
        {
            errors["to"] = new[] { "O período máximo é de 3 meses." };
        }

        if (errors.Count > 0)
        {
            return new EventResult<RehearsalRangeResultDto>(EventResultStatus.Invalid, Errors: errors);
        }

        int created = 0, skipped = 0;
        for (var day = from!.Value; day <= to!.Value; day = day.AddDays(1))
        {
            if (day.DayOfWeek is not (DayOfWeek.Tuesday or DayOfWeek.Thursday))
            {
                continue;
            }

            if (await _rehearsals.GetRehearsalByDateAsync(day) is not null)
            {
                skipped++;
                continue;
            }

            var r = await _rehearsals.CreateRehearsalAsync(day, input.Location!.Trim(), RehearsalAgendaService.Blank(input.Theme));
            if (RehearsalAgendaService.Blank(input.Description) is not null)
            {
                await SaveDetailsAsync(r.Id, new RehearsalInput(null, input.Location, input.Theme, input.Description, null));
            }

            created++;
        }

        return EventResult<RehearsalRangeResultDto>.Ok(new RehearsalRangeResultDto(created, skipped));
    }

    public async Task<EventResult<RehearsalSummaryDto>> UpdateAsync(int id, RehearsalInput input, ClaimsPrincipal user)
    {
        if (await RefusalAsync<RehearsalSummaryDto>(id, user, edit: true) is { } refused)
        {
            return refused;
        }

        if (DetailErrors(input) is { Count: > 0 } errors)
        {
            return new EventResult<RehearsalSummaryDto>(EventResultStatus.Invalid, Errors: errors);
        }

        await SaveDetailsAsync(id, input);
        return EventResult<RehearsalSummaryDto>.Ok(await SummaryAsync(id));
    }

    public async Task<EventResult<bool>> DeleteAsync(int id, ClaimsPrincipal user)
    {
        if (await RefusalAsync<bool>(id, user) is { } refused)
        {
            return refused;
        }

        await _rehearsals.DeleteRehearsalAsync(id);
        return EventResult<bool>.Ok(true);
    }

    public async Task<EventResult<bool>> CancelAsync(int id, RehearsalCancelInput input, ClaimsPrincipal user)
    {
        if (await RefusalAsync<bool>(id, user) is { } refused)
        {
            return refused;
        }

        var r = await FindAsync(id);
        if (r!.IsCanceled)
        {
            return EventResult<bool>.Fail(EventResultStatus.Closed);
        }

        var reason = RehearsalAgendaService.Blank(input.Reason);
        if (reason is null || reason.Length > TextMax)
        {
            return EventResult<bool>.Invalid("reason", reason is null ? "O motivo do cancelamento é obrigatório." : $"O motivo não pode exceder {TextMax} caracteres.");
        }

        await _rehearsals.CancelRehearsalAsync(id, reason);
        return EventResult<bool>.Ok(true);
    }

    public async Task<EventResult<bool>> ReactivateAsync(int id, ClaimsPrincipal user)
    {
        if (await RefusalAsync<bool>(id, user) is { } refused)
        {
            return refused;
        }

        var r = await FindAsync(id);
        if (!r!.IsCanceled || RehearsalAgendaService.IsPast(r, Today))
        {
            return EventResult<bool>.Fail(EventResultStatus.Closed);
        }

        await _rehearsals.UncancelRehearsalAsync(id);
        return EventResult<bool>.Ok(true);
    }
}
