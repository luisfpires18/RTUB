// Rehearsal admin: shared helpers (refusals, lookups, validation).
using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

public sealed partial class RehearsalAdminService
{
    // ---------- helpers ----------

    private static EventResult<T>? Refusal<T>(ClaimsPrincipal user) =>
        !RehearsalsAuthorization.IsMember(user) ? EventResult<T>.Fail(EventResultStatus.SignInRequired)
        : !RehearsalsAuthorization.CanManage(user) ? EventResult<T>.Fail(EventResultStatus.Forbidden)
        : null;

    /// <summary>Create and edit: Mod and above. Everything else stays <see cref="Refusal{T}"/> (Admin / Owner).</summary>
    private static EventResult<T>? EditRefusal<T>(ClaimsPrincipal user) =>
        !RehearsalsAuthorization.IsMember(user) ? EventResult<T>.Fail(EventResultStatus.SignInRequired)
        : !RehearsalsAuthorization.CanEdit(user) ? EventResult<T>.Fail(EventResultStatus.Forbidden)
        : null;

    private async Task<EventResult<T>?> RefusalAsync<T>(int id, ClaimsPrincipal user, bool edit = false) =>
        (edit ? EditRefusal<T>(user) : Refusal<T>(user))
        ?? (await FindAsync(id) is null ? EventResult<T>.Fail(EventResultStatus.NotFound) : null);

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
        return RehearsalAgendaService.ToSummary(r, going, pending, null, Today);
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
