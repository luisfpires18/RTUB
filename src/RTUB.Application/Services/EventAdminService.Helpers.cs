// Event admin: shared helpers (recipients, lookups, refusals).
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

public sealed partial class EventAdminService
{
    // ---------- helpers ----------

    private sealed record Recipients(List<string> Emails, Dictionary<string, (string nickname, string fullName)> Data);

    /// <summary>The email audience: confirmed addresses with "Notificações por email" on.</summary>
    private async Task<Recipients> EmailRecipientsAsync()
    {
        var people = (await UsersAsync())
            .Where(u => u.Subscribed && u.EmailConfirmed && !string.IsNullOrWhiteSpace(u.Email))
            .DistinctBy(u => u.Email!, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new Recipients(
            people.Select(u => u.Email!).ToList(),
            people.ToDictionary(u => u.Email!, u => (u.Nickname ?? "", $"{u.FirstName ?? ""} {u.LastName ?? ""}".Trim())));
    }

    /// <summary>Runs one email send; a thrown error or a refusal comes back as text for the modal.</summary>
    private async Task<(int Count, string? Warning, string? Error)> SendEmailAsync(
        Func<Task<(bool success, int count, string? errorMessage)>> send, int eventId)
    {
        try
        {
            var (success, count, error) = await send();
            return success ? (count, error, null) : (0, null, error ?? "Não foi possível enviar os emails.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send event emails for event {EventId}", eventId);
            return (0, null, "Não foi possível enviar os emails.");
        }
    }

    // ponytail: whole Users table in memory (~100 rows); filter in SQL if it grows.
    private async Task<List<ApplicationUser>> UsersAsync()
    {
        await using var db = await _contexts.CreateDbContextAsync();
        return await db.Users.AsNoTracking().ToListAsync();
    }

    private async Task<Event?> FindAsync(int id)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        return await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
    }

    /// <summary>The same "past" as the agenda: the last day (EndDate, else Date) is before today.</summary>
    // Past = its last day is before today in Portugal.
    private static bool IsPast(Event e, DateTime today) => (e.EndDate ?? e.Date).Date < today;

    private DateTime Today => PortugalTime.Today(_time);

    /// <summary>Where the emails point: the agenda.</summary>
    private static string EventsLink(string baseUrl) => $"{baseUrl.TrimEnd('/')}/events";

    /// <summary>The event image belongs to editing the event: Mod and above.</summary>
    private static EventResult<T>? EditRefusal<T>(ClaimsPrincipal user) =>
        !EventsAuthorization.IsMember(user) ? EventResult<T>.Fail(EventResultStatus.SignInRequired)
        : !EventsAuthorization.CanEdit(user) ? EventResult<T>.Fail(EventResultStatus.Forbidden)
        : null;

    private static EventResult<T>? Refusal<T>(ClaimsPrincipal user) =>
        !EventsAuthorization.IsMember(user) ? EventResult<T>.Fail(EventResultStatus.SignInRequired)
        : !EventsAuthorization.CanManage(user) ? EventResult<T>.Fail(EventResultStatus.Forbidden)
        : null;
}
