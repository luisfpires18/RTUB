// Logistics: shared helpers (refusals, notifications, validation, DTO mapping).
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

public sealed partial class LogisticsKanbanService
{
    /// <summary>401 for visitors (or a deleted account); 403 for expelled accounts and, on management, for non-managers.</summary>
    private async Task<EventResult<T>?> RefusalAsync<T>(ClaimsPrincipal user, bool manage)
    {
        if (LogisticsAuthorization.UserId(user) is not { } id)
        {
            return EventResult<T>.Fail(EventResultStatus.SignInRequired);
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id) is not { } member)
        {
            return EventResult<T>.Fail(EventResultStatus.SignInRequired);
        }

        return LogisticsAuthorization.CanView(member, user) && (!manage || LogisticsAuthorization.CanManage(user))
            ? null
            : EventResult<T>.Fail(EventResultStatus.Forbidden);
    }

    /// <summary>
    /// One card change. <paramref name="assignedMayChange"/>: the card's owner and collaborators may make it too, not only
    /// managers (<see cref="CardAccess.CanWork"/>, read from the database on this request, so a removed assignee is
    /// refused). The change answers null (save) or the refusal (invalid, forbidden, conflict: nothing saved).
    ///
    /// The check, the change and the save are one transaction, and Microsoft.Data.Sqlite begins it as BEGIN IMMEDIATE:
    /// it takes SQLite's write lock before it reads anything, so the reads see the newest committed rows and no other
    /// write can land between the check and the save: an assignment removal (or a manager's checklist edit) either
    /// committed first - and is seen - or waits until this change commits (LogisticsAssignmentRaceTests pins it). Without it, a removal committed after the check still let the former
    /// assignee's write through, and two assignees ticking at once could lose a tick.
    /// </summary>
    private async Task<EventResult<LogisticsCardDetailDto>> ChangeCardAsync(int id, ClaimsPrincipal user, bool assignedMayChange,
        Func<ApplicationDbContext, LogisticsCard, CardAccess, Task<EventResult<LogisticsCardDetailDto>?>> change)
    {
        if (await RefusalAsync<LogisticsCardDetailDto>(user, manage: false) is { } refused)
        {
            return refused;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (await db.LogisticsCards.FindAsync(id) is not { } card)
        {
            return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.NotFound);
        }

        // 067: a card of a board the caller may not see is not disclosed; archived work is read-only.
        var access = await AccessAsync(db, card, user);
        if (!access.Viewer)
        {
            return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.NotFound);
        }

        if (!access.CanWork || (!assignedMayChange && !access.Manager))
        {
            return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.Forbidden);
        }

        if (access.ReadOnly)
        {
            return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.Closed);
        }

        if (await change(db, card, access) is { } failed)
        {
            return failed;
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        await NotifyAsync(db, card, access);
        return EventResult<LogisticsCardDetailDto>.Ok(await DetailAsync(db, id, user));
    }

    /// <summary>Task 069: the change's pushes, after the commit, never to the member who made it.</summary>
    private async Task NotifyAsync(ApplicationDbContext db, LogisticsCard card, CardAccess access)
    {
        if (_notifier is null || access.Notices.Count == 0)
        {
            return;
        }

        var actor = access.UserId is null ? null : await NameAsync(db, access.UserId);
        var about = new LogisticsNotifier.Card(access.BoardId, card.Id, card.Title, access.Board.Name);
        foreach (var (kind, to) in access.Notices)
        {
            await _notifier.SendAsync(kind, about, to, access.UserId, actor);
        }
    }

    private static Dictionary<string, string[]> CardErrors(string? title, string? description)
    {
        var errors = new Dictionary<string, string[]>();
        var t = title?.Trim() ?? "";
        if (t == "" || t.Length > 200)
        {
            errors["title"] = new[] { "O título do cartão é obrigatório (máx. 200 caracteres)." };
        }

        if ((description?.Trim().Length ?? 0) > 2000)
        {
            errors["description"] = new[] { "A descrição não pode exceder 2000 caracteres" };
        }

        return errors;
    }

    private static DateTime? Day(string? value, string field, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            return day;
        }

        errors[field] = new[] { "Data inválida." };
        return null;
    }

    private static string? ListName(LogisticsListInput input) =>
        input.Name?.Trim() is { Length: > 0 and <= 100 } name ? name : null;

    private static Dictionary<string, string[]> Error(string field, string message) => new() { [field] = new[] { message } };

    private static void Renumber(List<LogisticsCard> cards)
    {
        for (var i = 0; i < cards.Count; i++)
        {
            cards[i].UpdatePosition(i);
        }
    }

    private static LogisticsEventDto? Event(Event? e) => e is null ? null : new LogisticsEventDto(e.Id, e.Name, e.Date);

    // Collaborators: the owner left out (an owner who also has an assignment row counts once, as the owner).
    private static async Task<Dictionary<int, int>> AssignmentCountsAsync(ApplicationDbContext db, List<int> cardIds) =>
        await db.LogisticsCardAssignments.AsNoTracking().Where(a => cardIds.Contains(a.CardId) && a.UserId != a.Card.AssignedToUserId)
            .GroupBy(a => a.CardId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count);

    private static LogisticsPersonDto? Person(ApplicationUser? u)
    {
        if (u is null)
        {
            return null;
        }

        var who = GovernanceService.ToMember(u.Nickname, u.FirstName, u.LastName, u.ImageUrl);
        return new LogisticsPersonDto(who.DisplayName, who.FullName, who.AvatarUrl);
    }

    private static LogisticsAssigneeDto Assignee(ApplicationUser u, bool withId)
    {
        var who = GovernanceService.ToMember(u.Nickname, u.FirstName, u.LastName, u.ImageUrl);
        return new LogisticsAssigneeDto(withId ? u.Id : null, who.DisplayName, who.FullName, who.AvatarUrl);
    }

    private static LogisticsCardDto Face(LogisticsCard c, int assignments, bool mine, int comments, int attachments)
    {
        var checklist = Tasks(c.ChecklistJson);
        var description = c.Description ?? "";
        var marker = description.IndexOf(LegacyFilesMarker, StringComparison.Ordinal);
        return new LogisticsCardDto(c.Id, c.Title, marker < 0 ? description : description[..marker].TrimEnd(), c.Status.ToString(), Labels(c.Labels),
            c.StartDate, c.DueDate, c.Event?.Name, Person(c.AssignedToUser), assignments, checklist.Count(t => t.Done), checklist.Count,
            Links(c.AttachmentsJson).Count, mine, c.Priority.ToString(), comments, attachments);
    }

    /// <summary>JSON labels, or the oldest comma-separated text (purple), as the old page read them.</summary>
    private static IReadOnlyList<LogisticsLabelDto> Labels(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<LogisticsLabelDto>();
        }

        try
        {
            return (JsonSerializer.Deserialize<List<StoredLabel>>(raw) ?? new()).Select(l => new LogisticsLabelDto(l.Text ?? "", l.Color ?? "#6f42c1")).ToList();
        }
        catch (JsonException)
        {
            return raw.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(l => new LogisticsLabelDto(l.Trim(), "#6f42c1")).ToList();
        }
    }

    private static List<StoredTask> Tasks(string? raw) => Read<StoredTask>(raw);

    private static List<StoredLink> Links(string? raw) => Read<StoredLink>(raw);

    private static List<T> Read<T>(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new();
        }

        try
        {
            return JsonSerializer.Deserialize<List<T>>(raw) ?? new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    /// <summary>Allowed board-folder characters: letters, digits, hyphen, underscore, space and accented Latin-1 letters.</summary>
    private static readonly Regex SafePathRegex = new(@"[^a-zA-Z0-9\-_\sÀ-ÿ]", RegexOptions.Compiled);

    /// <summary>
    /// The board name as one folder name: no "..", no separator or other unsafe character, "unnamed" when nothing is
    /// left. Moved unchanged from the old LogisticsCardService (removed in 042, no caller).
    /// </summary>
    internal static string SanitizePathComponent(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var sanitized = SafePathRegex.Replace(input.Replace("..", ""), "").Trim();
        return string.IsNullOrEmpty(sanitized) ? "unnamed" : sanitized;
    }
}