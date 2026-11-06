using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// The old Blazor /logistics and /logistics/{id} behind the React pages (React track 023, docs/react-logistics.md). Same
/// tables and rules, now enforced server-side (<see cref="LogisticsAuthorization"/>; the old pages re-checked in the
/// circuit):
/// - boards: active and completed, newest first, search over name and description; name (≤ 200) required,
///   description ≤ 2000, optional event; finish / reopen; delete takes its lists and cards (cascade);
/// - lists by position (new ones last), name ≤ 100; delete takes its cards; cards by position;
/// - cards: title (≤ 200) required, description ≤ 2000, optional "Atribuir a" member, status TODO / WIP / DONE,
///   labels (text + colour), start and due dates, checklist, links to other cards of the board, assigned members;
///   the JSON columns keep their old shape, so old rows (comma labels included) read as before;
/// - moving a card renumbers the positions of the lists it leaves and enters (the old move wrote only the moved
///   card's position, so positions could collide);
/// - board files: one folder per board for the current fiscal year
///   (docs/{env}/{year}/Logistics/{board}/, shared by all its cards, as before), ≤ 10 MB, the old file types;
/// - reminders (task 069): managers for board viewers, workers for themselves; sent by LogisticsReminderDelivery;
/// - task 065: every member who sees the board (Leitões included) reads the card details; the members a card is
///   assigned to ("Atribuir a" or "Membros atribuídos") may tick its checklist items and change its status, nothing
///   else, re-checked against the database on every write.
/// Task 069: assignment and mention pushes (<see cref="LogisticsNotifier"/>), sent after the commit.
/// </summary>
public sealed partial class LogisticsKanbanService : ILogisticsKanbanService
{
    public const long MaxFileBytes = 10 * 1024 * 1024;
    public const int SearchLimit = 20;
    private const string LegacyFilesMarker = "<!-- FILES:";
    private static readonly Regex Colour = new("^#[0-9a-fA-F]{6}$");
    private static readonly HashSet<string> FileTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf", "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "text/plain",
        "image/png", "image/jpeg", "image/jpg",
    };

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly IDocumentStorageService _storage;
    private readonly DocumentTreeCache _trees;
    private readonly IHostEnvironment _environment;
    private readonly AuditContext _audit;
    private readonly ILogger<LogisticsKanbanService> _logger;
    private readonly TimeProvider _time;
    private readonly LogisticsNotifier? _notifier;

    public LogisticsKanbanService(IDbContextFactory<ApplicationDbContext> contexts, IDocumentStorageService storage, IHostEnvironment environment,
        AuditContext audit, ILogger<LogisticsKanbanService> logger, DocumentTreeCache trees, TimeProvider? time = null,
        LogisticsNotifier? notifier = null)
    {
        _notifier = notifier;
        _time = time ?? TimeProvider.System;
        _trees = trees;
        _contexts = contexts;
        _storage = storage;
        _environment = environment;
        _audit = audit;
        _logger = logger;
    }

    // The JSON columns, in the shape the old page wrote (Id was always 0).
    private sealed record StoredLabel(int Id, string Text, string Color);
    private sealed record StoredTask(int Id, string Task, bool Done);
    private sealed record StoredLink(int Id, string Name, int? CardId);

    // ---------------------------------------------------------------- boards

    public async Task<EventResult<IReadOnlyList<LogisticsEventDto>>> GetEventsAsync(string? search, ClaimsPrincipal user)
    {
        if (await RefusalAsync<IReadOnlyList<LogisticsEventDto>>(user, manage: true) is { } refused)
        {
            return refused;
        }

        var q = EventParticipantsAdminService.Fold(search);
        if (q == "")
        {
            return EventResult<IReadOnlyList<LogisticsEventDto>>.Ok(Array.Empty<LogisticsEventDto>());
        }

        await using var db = await _contexts.CreateDbContextAsync();
        var events = await db.Events.AsNoTracking().OrderByDescending(e => e.Date).Select(e => new LogisticsEventDto(e.Id, e.Name, e.Date)).ToListAsync();
        // The old picker: the event name, the first ten matches.
        return EventResult<IReadOnlyList<LogisticsEventDto>>.Ok(events.Where(e => EventParticipantsAdminService.Fold(e.Name).Contains(q)).Take(10).ToList());
    }

    // ---------------------------------------------------------------- cards

    /// <summary>Every member who sees the board reads the details (task 065); a private board's card is 404 to others (067).</summary>
    public async Task<EventResult<LogisticsCardDetailDto>> GetCardAsync(int id, ClaimsPrincipal user)
    {
        var (refused, db, _, _) = await OpenCardAsync<LogisticsCardDetailDto>(id, user);
        if (refused is not null)
        {
            return refused;
        }

        await using (db)
        {
            return EventResult<LogisticsCardDetailDto>.Ok(await DetailAsync(db!, id, user));
        }
    }

    /// <summary>
    /// The card's own fields (task 066): owner, collaborators and managers. <c>Version</c> must be the card's current one
    /// (409 otherwise, nothing saved). Only a manager changes (or clears) the owner: anyone else sends none (kept) or the
    /// current one; another id is forged (403). Every changed field leaves one history line.
    /// </summary>
    public Task<EventResult<LogisticsCardDetailDto>> UpdateCardAsync(int id, LogisticsCardInput input, ClaimsPrincipal user) =>
        ChangeCardAsync(id, user, assignedMayChange: true, async (db, card, access) =>
        {
            // Non-managers never see member ids: they send no owner (kept) or the current one; any other value is forged.
            var owner = string.IsNullOrEmpty(input.AssignedToUserId) ? null : input.AssignedToUserId;
            if (!access.Manager)
            {
                if (owner is not null && owner != card.AssignedToUserId)
                {
                    return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.Forbidden);
                }

                owner = card.AssignedToUserId;
            }

            var errors = CardErrors(input.Title, input.Description);
            var start = Day(input.StartDate, "startDate", errors);
            var due = Day(input.DueDate, "dueDate", errors);
            var priority = card.Priority;
            if (input.Priority is not null && (!Enum.TryParse(input.Priority, out priority) || !Enum.IsDefined(priority) || int.TryParse(input.Priority, out _)))
            {
                errors["priority"] = new[] { "Prioridade inválida." };
            }

            if (start is { } s && due is { } d && d < s)
            {
                errors["dueDate"] = new[] { "O prazo não pode ser antes do início." };
            }

            if (owner is not null && owner != card.AssignedToUserId && !(await BoardViewersAsync(db, access.Board, new[] { owner })).Contains(owner))
            {
                errors["assignedToUserId"] = new[] { NoAccess };
            }

            if (errors.Count > 0)
            {
                return new EventResult<LogisticsCardDetailDto>(EventResultStatus.Invalid, Errors: errors);
            }

            if (input.Version != card.Version)
            {
                return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.Conflict);
            }

            // A description that still carries the old base64 file block keeps it, unseen, as the old page did.
            var stored = card.Description ?? "";
            var marker = stored.IndexOf(LegacyFilesMarker, StringComparison.Ordinal);
            var description = input.Description?.Trim() ?? "";
            var visible = marker < 0 ? stored : stored[..marker].TrimEnd();
            var title = input.Title!.Trim();
            if (title != card.Title)
            {
                Log(db, card.Id, access, "title", $"Título: «{Cut(card.Title)}» → «{Cut(title)}»");
            }

            if (description != visible)
            {
                Log(db, card.Id, access, "description", "Detalhes alterados");
            }

            if (priority != card.Priority)
            {
                Log(db, card.Id, access, "priority", $"Prioridade: {PriorityName(card.Priority)} → {PriorityName(priority)}");
            }

            if (start != card.StartDate)
            {
                Log(db, card.Id, access, "start", start is null ? "Início removido" : $"Início: {DayText(start)}");
            }

            if (due != card.DueDate)
            {
                Log(db, card.Id, access, "deadline", due is null ? "Prazo removido" : $"Prazo: {DayText(due)}");
            }

            if (owner != card.AssignedToUserId)
            {
                if (owner is not null)
                {
                    access.Notices.Add((LogisticsNotice.Owner, new[] { owner }));
                }

                Log(db, card.Id, access, "owner", owner is null ? "Responsável removido" : $"Responsável: {await NameAsync(db, owner)}");
            }

            card.UpdateContent(title, marker < 0 ? description : $"{description}\n\n{stored[marker..]}");
            card.SetDates(start, due, card.ReminderDate);
            card.AssignToUser(owner);
            card.Priority = priority;
            card.Version++;
            return null;
        });

    public Task<EventResult<LogisticsCardDetailDto>> SetLabelsAsync(int id, LogisticsLabelsInput input, ClaimsPrincipal user) =>
        ChangeCardAsync(id, user, assignedMayChange: false, (db, card, access) =>
        {
            var labels = input.Labels ?? Array.Empty<LogisticsLabelDto>();
            if (labels.Count > 20 || labels.Any(l => string.IsNullOrWhiteSpace(l.Text) || l.Text.Trim().Length > 50 || !Colour.IsMatch(l.Color ?? "")))
            {
                return Bad("labels", "Cada etiqueta precisa de um nome (máx. 50) e de uma cor; no máximo 20.");
            }

            card.SetLabels(JsonSerializer.Serialize(labels.Select(l => new StoredLabel(0, l.Text.Trim(), l.Color.ToLowerInvariant()))));
            return Fine();
        });

    /// <summary>
    /// Tick or untick one item (task 065): managers, owner or collaborators. Only <c>Done</c> changes; the text, order and
    /// count stay. <c>Task</c> must match the item at <paramref name="index"/>, so a checklist changed meanwhile is
    /// refused instead of ticking another item.
    /// </summary>
    public Task<EventResult<LogisticsCardDetailDto>> ToggleChecklistItemAsync(int id, int index, LogisticsChecklistToggleInput input, ClaimsPrincipal user) =>
        ChangeCardAsync(id, user, assignedMayChange: true, (db, card, access) =>
        {
            var items = Tasks(card.ChecklistJson);
            if (index < 0 || index >= items.Count || items[index].Task != input.Task)
            {
                return Bad("items", ChecklistMoved);
            }

            if (items[index].Done != input.Done)
            {
                Log(db, card.Id, access, "checklist", $"{(input.Done ? "Tarefa concluída" : "Tarefa reaberta")}: {Cut(items[index].Task)}");
                items[index] = items[index] with { Done = input.Done };
                card.SetChecklist(JsonSerializer.Serialize(items));
            }

            return Fine();
        });

    public Task<EventResult<LogisticsCardDetailDto>> SetLinksAsync(int id, LogisticsLinksInput input, ClaimsPrincipal user) =>
        ChangeCardAsync(id, user, assignedMayChange: false, async (db, card, access) =>
        {
            var ids = (input.CardIds ?? Array.Empty<int>()).Distinct().ToList();
            var linked = await db.LogisticsCards.AsNoTracking().Where(c => ids.Contains(c.Id) && c.Id != id && c.List.BoardId == access.BoardId)
                .ToDictionaryAsync(c => c.Id, c => c.Title);
            if (ids.Count > 50 || linked.Count != ids.Count)
            {
                return EventResult<LogisticsCardDetailDto>.Invalid("cardIds", "Escolha cartões deste quadro (outros que não este).");
            }

            // Each link keeps a copy of the linked card's title, as before; saving the links refreshes those copies.
            card.SetAttachments(JsonSerializer.Serialize(ids.Select(c => new StoredLink(0, linked[c], c))));
            return null;
        });

    /// <summary>A collaborator (managers only, task 066: reassignment is manager-only); one history line.</summary>
    public Task<EventResult<LogisticsCardDetailDto>> AddAssignmentAsync(int id, LogisticsAssignmentInput input, ClaimsPrincipal user) =>
        ChangeCardAsync(id, user, assignedMayChange: false, async (db, card, access) =>
        {
            // Assignment never grants access (067): the member must already see the board.
            if (string.IsNullOrEmpty(input.UserId) || !(await BoardViewersAsync(db, access.Board, new[] { input.UserId })).Contains(input.UserId))
            {
                return EventResult<LogisticsCardDetailDto>.Invalid("userId", NoAccess);
            }

            if (await db.LogisticsCardAssignments.AnyAsync(a => a.CardId == id && a.UserId == input.UserId))
            {
                return EventResult<LogisticsCardDetailDto>.Invalid("userId", "Utilizador já está atribuído a este cartão");
            }

            db.LogisticsCardAssignments.Add(LogisticsCardAssignment.Create(id, input.UserId));
            access.Notices.Add((LogisticsNotice.Collaborator, new[] { input.UserId }));
            Log(db, id, access, "collaborator", $"Colaborador adicionado: {await NameAsync(db, input.UserId)}");
            return null;
        });

    public Task<EventResult<LogisticsCardDetailDto>> RemoveAssignmentAsync(int id, string userId, ClaimsPrincipal user) =>
        ChangeCardAsync(id, user, assignedMayChange: false, async (db, card, access) =>
        {
            if (await db.LogisticsCardAssignments.FirstOrDefaultAsync(a => a.CardId == id && a.UserId == userId) is not { } assignment)
            {
                return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.NotFound);
            }

            db.LogisticsCardAssignments.Remove(assignment);
            Log(db, id, access, "collaborator", $"Colaborador removido: {await NameAsync(db, userId)}");
            return null;
        });

    public async Task<EventResult<bool>> DeleteCardAsync(int id, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var boardId = await db.LogisticsCards.Where(c => c.Id == id).Select(c => (int?)c.List.BoardId).FirstOrDefaultAsync();
        var (refused, _) = boardId is { } b
            ? await SeeBoardAsync<bool>(db, b, user, manage: true, writable: true)
            : (await RefusalAsync<bool>(user, manage: false) ?? EventResult<bool>.Fail(EventResultStatus.NotFound), null);
        if (refused is not null)
        {
            return refused;
        }

        var card = (await db.LogisticsCards.FindAsync(id))!;

        var files = await AttachmentPathsAsync(db, c => c.Id == id);
        db.LogisticsCards.Remove(card);
        await db.SaveChangesAsync();
        await DeleteStoredAsync(files);
        return EventResult<bool>.Ok(true);
    }

    // ---------------------------------------------------------------- helpers

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
