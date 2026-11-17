// Logistics: events, card detail and card edits (labels, links, assignments, delete).
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

public sealed partial class LogisticsKanbanService
{
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
}