using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// The old Blazor /rehearsals management behind the React pages (React track 014, docs/react-rehearsals.md).
/// Same rules, now server-side and for Admin or Owner (was IsInRole("Admin") only, in the UI):
/// - create one rehearsal (a date already taken is refused) or every Tuesday and Thursday of a range (≤90 days,
///   not starting in the past; taken dates skipped); 21:30-00:00 as always;
/// - edit location, theme, description and notes; delete (presenças cascade);
/// - cancel with a reason (presenças deleted, no email) and reactivate an upcoming one;
/// - push a notice about an upcoming rehearsal to subscribed members (optionally Leitões and Caloiros), audited;
/// - once a rehearsal can be approved: approve a pending presença, remove one, add a member (approved).
/// Changed on purpose: description and notes typed when creating are now saved (the old form dropped them).
/// </summary>
public sealed class RehearsalAdminService : IRehearsalAdminService
{
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
        ILogger<RehearsalAdminService> logger)
    {
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
        if (Refusal<RehearsalSummaryDto>(user) is { } refused)
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
        if (Refusal<RehearsalRangeResultDto>(user) is { } refused)
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
        else if (from < DateTime.Today)
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
        if (await RefusalAsync<RehearsalSummaryDto>(id, user) is { } refused)
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
        if (!r!.IsCanceled || RehearsalAgendaService.IsPast(r))
        {
            return EventResult<bool>.Fail(EventResultStatus.Closed);
        }

        await _rehearsals.UncancelRehearsalAsync(id);
        return EventResult<bool>.Ok(true);
    }

    public async Task<EventResult<RehearsalNoticeAudienceDto>> GetNoticeAudienceAsync(int id, ClaimsPrincipal user)
    {
        if (await RefusalAsync<RehearsalNoticeAudienceDto>(id, user) is { } refused)
        {
            return refused;
        }

        var (all, few) = await AudienceAsync();
        return EventResult<RehearsalNoticeAudienceDto>.Ok(new RehearsalNoticeAudienceDto(
            all.Count(u => u.Subscribed), all.Count, few.Count(u => u.Subscribed), few.Count));
    }

    public async Task<EventResult<RehearsalNoticeResultDto>> SendNoticeAsync(int id, RehearsalNoticeInput input, ClaimsPrincipal user, string baseUrl)
    {
        if (await RefusalAsync<RehearsalNoticeResultDto>(id, user) is { } refused)
        {
            return refused;
        }

        var r = await FindAsync(id);
        if (r!.IsCanceled || RehearsalAgendaService.IsPast(r))
        {
            return EventResult<RehearsalNoticeResultDto>.Fail(EventResultStatus.Closed);
        }

        var message = RehearsalAgendaService.Blank(input.Message);
        if (message is null || message.Length > NoticeMax)
        {
            return EventResult<RehearsalNoticeResultDto>.Invalid("message", message is null ? "A mensagem da notificação é obrigatória." : $"A mensagem não pode exceder {NoticeMax} caracteres.");
        }

        var (all, few) = await AudienceAsync();
        var recipients = (input.OnlyLeitoesAndCaloiros ? few : all).Where(u => u.Subscribed).Select(u => u.Id).ToList();
        if (recipients.Count == 0)
        {
            return EventResult<RehearsalNoticeResultDto>.Invalid("message", "Ninguém deste grupo tem as notificações ativas.");
        }

        var notification = _pushFactory.CreateRehearsalNotification(r, message, baseUrl);
        var (sent, failed) = await _push.SendToSelectedUsersAsync(recipients, notification);

        try
        {
            await _audit.AddAsync(new AuditLog
            {
                EntityType = "Rehearsal",
                EntityId = r.Id,
                Action = "PushNotificationSent",
                UserId = RehearsalsAuthorization.UserId(user),
                UserName = user.Identity!.Name,
                Timestamp = DateTime.UtcNow,
                Changes = JsonSerializer.Serialize(new { RecipientCount = recipients.Count, NotificationBody = message }),
                EntityDisplayName = $"Ensaio - {r.Date:dd/MM/yyyy} {(string.IsNullOrEmpty(r.Theme) ? r.Location : r.Theme)}",
                IsCriticalAction = false,
            });
        }
        catch (Exception ex)
        {
            // As before: the notice went out even if its audit row could not be written.
            _logger.LogError(ex, "Failed to audit a push notice for rehearsal {RehearsalId}", r.Id);
        }

        return EventResult<RehearsalNoticeResultDto>.Ok(new RehearsalNoticeResultDto(sent, failed));
    }

    public async Task<EventResult<bool>> ApproveAsync(int id, int attendanceId, ClaimsPrincipal user)
    {
        if (await RefusalAsync<bool>(id, user) is { } refused)
        {
            return refused;
        }

        var (r, a) = await AttendanceAsync(id, attendanceId);
        if (a is null)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        if (!RehearsalAgendaService.IsApprovable(r!) || !a.WillAttend || a.Attended)
        {
            return EventResult<bool>.Fail(EventResultStatus.Closed);
        }

        await _attendance.UpdateAttendanceAsync(a.Id, true, a.Instrument, RehearsalsAuthorization.UserId(user));
        return EventResult<bool>.Ok(true);
    }

    public async Task<EventResult<bool>> RemoveAttendanceAsync(int id, int attendanceId, ClaimsPrincipal user)
    {
        if (RehearsalsAuthorization.UserId(user) is not { } me)
        {
            return EventResult<bool>.Fail(EventResultStatus.SignInRequired);
        }

        var (r, a) = await AttendanceAsync(id, attendanceId);
        if (a is null)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        if (a.UserId != me && !(RehearsalsAuthorization.CanManage(user) && RehearsalAgendaService.IsApprovable(r!)))
        {
            return EventResult<bool>.Fail(EventResultStatus.Forbidden);
        }

        await _attendance.DeleteAttendanceAsync(a.Id);
        return EventResult<bool>.Ok(true);
    }

    public async Task<EventResult<IReadOnlyList<EventMemberOptionDto>>> SearchMembersAsync(int id, string? query, ClaimsPrincipal user)
    {
        if (await RefusalAsync<IReadOnlyList<EventMemberOptionDto>>(id, user) is { } refused)
        {
            return refused;
        }

        var q = EventParticipantsAdminService.Fold(query);
        if (q.Length == 0)
        {
            return EventResult<IReadOnlyList<EventMemberOptionDto>>.Ok(Array.Empty<EventMemberOptionDto>());
        }

        await using var db = await _contexts.CreateDbContextAsync();
        var answered = db.RehearsalAttendances.Where(a => a.RehearsalId == id).Select(a => a.UserId);
        // ponytail: every member in memory (~100) to fold accents the Portuguese way; page it if it grows.
        var members = await db.Users.AsNoTracking()
            .Where(u => !u.IsExpelled && !answered.Contains(u.Id))
            .Select(u => new { u.Id, u.Nickname, u.FirstName, u.LastName, u.ImageUrl })
            .ToListAsync();

        return EventResult<IReadOnlyList<EventMemberOptionDto>>.Ok(members
            .Select(u => (u, who: EventDiscussionBoardService.Author(u.Nickname, u.FirstName, u.LastName, u.ImageUrl, null, null)))
            .Where(x => EventParticipantsAdminService.Fold(x.u.Nickname).Contains(q)
                        || EventParticipantsAdminService.Fold($"{x.u.FirstName} {x.u.LastName}").Contains(q))
            .OrderBy(x => x.who.Name, StringComparer.Create(CultureInfo.GetCultureInfo("pt-PT"), true))
            .Take(SearchLimit)
            .Select(x => new EventMemberOptionDto(x.u.Id, x.who.Name, x.who.FullName, x.who.AvatarUrl))
            .ToList());
    }

    public async Task<EventResult<bool>> AddAttendeeAsync(int id, RehearsalAttendeeInput input, ClaimsPrincipal user)
    {
        if (await RefusalAsync<bool>(id, user) is { } refused)
        {
            return refused;
        }

        var r = await FindAsync(id);
        if (r!.IsCanceled || !RehearsalAgendaService.IsApprovable(r))
        {
            return EventResult<bool>.Fail(EventResultStatus.Closed);
        }

        await using (var db = await _contexts.CreateDbContextAsync())
        {
            if (string.IsNullOrWhiteSpace(input.UserId) || !await db.Users.AnyAsync(u => u.Id == input.UserId && !u.IsExpelled))
            {
                return EventResult<bool>.Invalid("userId", "Escolha um membro da lista.");
            }

            if (await db.RehearsalAttendances.AnyAsync(a => a.RehearsalId == id && a.UserId == input.UserId))
            {
                return EventResult<bool>.Invalid("userId", "Este membro já respondeu a este ensaio.");
            }
        }

        var primary = await _instruments.GetPrimaryInstrumentAsync(input.UserId);
        await _attendance.CreateAttendanceWithApprovalAsync(id, input.UserId, primary?.InstrumentType);
        return EventResult<bool>.Ok(true);
    }

    // ---------- helpers ----------

    private static EventResult<T>? Refusal<T>(ClaimsPrincipal user) =>
        !RehearsalsAuthorization.IsMember(user) ? EventResult<T>.Fail(EventResultStatus.SignInRequired)
        : !RehearsalsAuthorization.CanManage(user) ? EventResult<T>.Fail(EventResultStatus.Forbidden)
        : null;

    private async Task<EventResult<T>?> RefusalAsync<T>(int id, ClaimsPrincipal user) =>
        Refusal<T>(user) ?? (await FindAsync(id) is null ? EventResult<T>.Fail(EventResultStatus.NotFound) : null);

    private async Task<Rehearsal?> FindAsync(int id)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        return await db.Rehearsals.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
    }

    private async Task<(Rehearsal? Rehearsal, RehearsalAttendance? Attendance)> AttendanceAsync(int id, int attendanceId)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var a = await db.RehearsalAttendances.AsNoTracking().Include(x => x.Rehearsal)
            .FirstOrDefaultAsync(x => x.Id == attendanceId && x.RehearsalId == id);
        return (a?.Rehearsal, a);
    }

    private async Task<RehearsalSummaryDto> SummaryAsync(int id)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var r = await db.Rehearsals.AsNoTracking().FirstAsync(x => x.Id == id);
        var going = await db.RehearsalAttendances.CountAsync(a => a.RehearsalId == id && a.WillAttend);
        var pending = await db.RehearsalAttendances.CountAsync(a => a.RehearsalId == id && a.WillAttend && !a.Attended);
        return RehearsalAgendaService.ToSummary(r, going, pending, null);
    }

    private Task SaveDetailsAsync(int id, RehearsalInput input) => _rehearsals.UpdateRehearsalAsync(id, input.Location!.Trim(),
        RehearsalAgendaService.Blank(input.Theme), RehearsalAgendaService.Blank(input.Description), RehearsalAgendaService.Blank(input.Notes));

    private sealed record Member(string Id, bool Subscribed);

    /// <summary>Everyone (as the old modal: all users), and the Leitões and Caloiros among them, with their push state.</summary>
    private async Task<(List<Member> All, List<Member> LeitoesCaloiros)> AudienceAsync()
    {
        var subscribed = (await _push.GetSubscribedUserIdsAsync()).ToHashSet();
        await using var db = await _contexts.CreateDbContextAsync();
        var users = await db.Users.AsNoTracking().ToListAsync();
        var all = users.Select(u => new Member(u.Id, subscribed.Contains(u.Id))).ToList();
        var few = users.Where(u => u.IsLeitao() || u.IsCaloiro()).Select(u => new Member(u.Id, subscribed.Contains(u.Id))).ToList();
        return (all, few);
    }

    private static Dictionary<string, string[]> DetailErrors(RehearsalInput input)
    {
        var errors = new Dictionary<string, string[]>();
        var location = input.Location?.Trim() ?? string.Empty;
        if (location.Length == 0)
        {
            errors["location"] = new[] { "A localização é obrigatória." };
        }
        else if (location.Length > LocationMax)
        {
            errors["location"] = new[] { $"A localização não pode exceder {LocationMax} caracteres." };
        }

        if ((input.Theme?.Trim().Length ?? 0) > ThemeMax)
        {
            errors["theme"] = new[] { $"O tema não pode exceder {ThemeMax} caracteres." };
        }

        if ((input.Description?.Trim().Length ?? 0) > TextMax)
        {
            errors["description"] = new[] { $"A descrição não pode exceder {TextMax} caracteres." };
        }

        if ((input.Notes?.Trim().Length ?? 0) > TextMax)
        {
            errors["notes"] = new[] { $"As notas não podem exceder {TextMax} caracteres." };
        }

        return errors;
    }

    private static DateTime? ParseDate(string? text) =>
        DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
}
