using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// Task 069 (issue #249): a card's reminders and "As minhas tarefas".
///
/// Reminders live on the card. Managers schedule them for members who see the board; the card's owner and collaborators
/// (Leitões included) schedule their own, for themselves only - any other target is 403. Only the creator of a personal
/// reminder (while still working on the card) or a manager changes or deletes it; pre-069 reminders (no creator) are
/// managers'. Members see their own reminders; managers see all. Archived work: 409. Delivery: LogisticsReminderDelivery.
/// </summary>
public sealed partial class LogisticsKanbanService
{
    public const int MaxRemindersPerCard = 20;

    public Task<EventResult<LogisticsCardDetailDto>> CreateReminderAsync(int cardId, LogisticsReminderInput input, ClaimsPrincipal user) =>
        ChangeCardAsync(cardId, user, assignedMayChange: true, async (db, card, access) =>
        {
            if (await TargetsAsync(db, access, input.UserIds) is not { } targets)
            {
                return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.Forbidden);
            }

            var errors = ReminderErrors(input, out var frequency, out var next);
            if (targets.Count == 0)
            {
                errors["userIds"] = new[] { "Escolha pelo menos um membro com acesso a este quadro." };
            }

            if (await db.LogisticsCardReminders.CountAsync(r => r.CardId == card.Id && r.IsActive) >= MaxRemindersPerCard)
            {
                errors["form"] = new[] { $"Este cartão já tem {MaxRemindersPerCard} lembretes ativos." };
            }

            if (errors.Count > 0)
            {
                return new EventResult<LogisticsCardDetailDto>(EventResultStatus.Invalid, Errors: errors);
            }

            var reminder = LogisticsCardReminder.Create(card.Id, frequency, string.Join(",", targets), next);
            reminder.CreatedByUserId = access.UserId;
            reminder.Personal = !access.Manager;
            reminder.ActivatedAt = _time.GetUtcNow().UtcDateTime;
            db.LogisticsCardReminders.Add(reminder);
            return null;
        });

    public Task<EventResult<LogisticsCardDetailDto>> UpdateReminderAsync(int cardId, int reminderId, LogisticsReminderInput input, ClaimsPrincipal user) =>
        ChangeCardAsync(cardId, user, assignedMayChange: true, async (db, card, access) =>
        {
            if (await db.LogisticsCardReminders.FirstOrDefaultAsync(r => r.Id == reminderId && r.CardId == card.Id) is not { } reminder)
            {
                return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.NotFound);
            }

            if (!MayChange(reminder, access))
            {
                return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.Forbidden);
            }

            var errors = ReminderErrors(input, out var frequency, out var next);
            // A personal reminder keeps its member; a manager's may change its members (board viewers).
            if (!reminder.Personal)
            {
                if (await TargetsAsync(db, access, input.UserIds) is not { Count: > 0 } targets)
                {
                    errors["userIds"] = new[] { "Escolha pelo menos um membro com acesso a este quadro." };
                }
                else
                {
                    reminder.UpdateTargetUsers(string.Join(",", targets));
                }
            }

            if (errors.Count > 0)
            {
                return new EventResult<LogisticsCardDetailDto>(EventResultStatus.Invalid, Errors: errors);
            }

            reminder.Frequency = frequency;
            reminder.NextReminderDate = next;
            reminder.IsActive = true;
            reminder.Attempts = 0;
            reminder.ClaimedUntil = null;
            reminder.ActivatedAt ??= _time.GetUtcNow().UtcDateTime;
            return null;
        });

    public Task<EventResult<LogisticsCardDetailDto>> DeleteReminderAsync(int cardId, int reminderId, ClaimsPrincipal user) =>
        ChangeCardAsync(cardId, user, assignedMayChange: true, async (db, card, access) =>
        {
            if (await db.LogisticsCardReminders.FirstOrDefaultAsync(r => r.Id == reminderId && r.CardId == card.Id) is not { } reminder)
            {
                return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.NotFound);
            }

            if (!MayChange(reminder, access))
            {
                return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.Forbidden);
            }

            db.LogisticsCardReminders.Remove(reminder);
            return null;
        });

    /// <summary>A manager: any reminder of the card. Anyone else: their own personal one (they work on the card).</summary>
    private static bool MayChange(LogisticsCardReminder reminder, CardAccess access) =>
        access.Manager || (reminder.Personal && reminder.CreatedByUserId is not null && reminder.CreatedByUserId == access.UserId);

    /// <summary>
    /// The members a reminder goes to: a manager's choice (board viewers; none = themselves), or the caller alone. Null =
    /// a non-manager named somebody else (forbidden). An empty list = a target without access (invalid).
    /// </summary>
    private static async Task<List<string>?> TargetsAsync(ApplicationDbContext db, CardAccess access, IReadOnlyList<string>? userIds)
    {
        var wanted = (userIds ?? Array.Empty<string>()).Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().ToList();
        if (!access.Manager)
        {
            return wanted.Count == 0 || (wanted.Count == 1 && wanted[0] == access.UserId) ? new List<string> { access.UserId! } : null;
        }

        if (wanted.Count == 0)
        {
            wanted.Add(access.UserId!);
        }

        return wanted.Count <= 100 && (await BoardViewersAsync(db, access.Board, wanted)).Count == wanted.Count ? wanted : new List<string>();
    }

    private Dictionary<string, string[]> ReminderErrors(LogisticsReminderInput input, out ReminderFrequency frequency, out DateTime next)
    {
        var errors = new Dictionary<string, string[]>();
        if (!Enum.TryParse(input.Frequency, out frequency) || !Enum.IsDefined(frequency) || int.TryParse(input.Frequency, out _))
        {
            errors["frequency"] = new[] { "Frequência inválida." };
        }

        next = input.NextReminderDate is { } n ? DateTime.SpecifyKind(new DateTime(n.Year, n.Month, n.Day, n.Hour, n.Minute, 0), DateTimeKind.Unspecified) : default;
        if (input.NextReminderDate is null)
        {
            errors["nextReminderDate"] = new[] { "Indique a data e a hora do lembrete." };
        }
        else if (PortugalTime.ToUtc(next) <= _time.GetUtcNow().UtcDateTime)
        {
            errors["nextReminderDate"] = new[] { "Escolha uma data e hora no futuro." };
        }

        return errors;
    }

    /// <summary>The card's reminders the caller may see: managers all active ones, anyone else their own.</summary>
    private static async Task<(List<LogisticsReminderDto> Reminders, bool CanRemind)> RemindersAsync(ApplicationDbContext db, int cardId, CardAccess access)
    {
        var reminders = await db.LogisticsCardReminders.AsNoTracking().Where(r => r.CardId == cardId && r.IsActive)
            .Where(r => access.Manager || (r.Personal && r.CreatedByUserId == access.UserId))
            .OrderBy(r => r.NextReminderDate).ThenBy(r => r.Id).ToListAsync();
        var ids = reminders.SelectMany(r => r.TargetUserIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id);
        var canWork = access.CanWork && !access.ReadOnly;
        return (reminders.Select(r => new LogisticsReminderDto(r.Id, r.Frequency.ToString(), r.NextReminderDate,
                r.TargetUserIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(users.ContainsKey).Select(u => Assignee(users[u], access.Manager)).ToList(),
                r.Personal, canWork && MayChange(r, access), r.LastSentAt is { } s ? Utc(s) : null)).ToList(),
            canWork);
    }

    // ---------------------------------------------------------------- As minhas tarefas

    /// <summary>
    /// Cards that are work for <paramref name="user"/>'s lists: not archived, on a board they see (members-visible, or
    /// private with them on the access list, or any for Mod / Admin / Owner) that is not archived. The one visibility rule
    /// of "As minhas tarefas" and of "As minhas ações pendentes" → Logística (task 073); callers add the assignment filter.
    /// </summary>
    internal static IQueryable<LogisticsCard> WorkableCards(ApplicationDbContext db, ClaimsPrincipal user)
    {
        var boards = VisibleBoards(db, user).Where(b => b.ArchivedAt == null).Select(b => b.Id);
        return db.LogisticsCards.AsNoTracking().Where(c => c.ArchivedAt == null && boards.Contains(c.List.BoardId));
    }

    /// <summary>Ticked and total checklist items of a card's stored checklist (as "As minhas tarefas" counts them).</summary>
    internal static (int Done, int Total) ChecklistProgress(string? checklistJson)
    {
        var tasks = Tasks(checklistJson);
        return (tasks.Count(t => t.Done), tasks.Count);
    }

    /// <summary>
    /// The caller's cards (owner or collaborator) on the boards they see, archived work left out; managers also get the
    /// open cards without a responsible member. Counts and filters are the page's, over this list.
    /// </summary>
    public async Task<EventResult<LogisticsMyTasksDto>> GetMyTasksAsync(ClaimsPrincipal user)
    {
        if (await RefusalAsync<LogisticsMyTasksDto>(user, manage: false) is { } refused)
        {
            return refused;
        }

        var me = LogisticsAuthorization.UserId(user)!;
        var manage = LogisticsAuthorization.CanManage(user);
        await using var db = await _contexts.CreateDbContextAsync();
        var rows = await WorkableCards(db, user)
            .Where(c => c.AssignedToUserId == me || c.Assignments.Any(a => a.UserId == me) || (manage && c.AssignedToUserId == null && c.Status != CardStatus.Done))
            .Select(c => new { c.Id, c.List.BoardId, Board = c.List.Board.Name, c.Title, c.Status, c.Priority, c.DueDate, c.AssignedToUserId, c.ChecklistJson })
            .ToListAsync();

        var ids = rows.Select(r => r.Id).ToList();
        var collaborations = (await db.LogisticsCardAssignments.AsNoTracking().Where(a => a.UserId == me && ids.Contains(a.CardId))
            .Select(a => a.CardId).ToListAsync()).ToHashSet();
        var tasks = rows
            .Select(c =>
            {
                var checklist = Tasks(c.ChecklistJson);
                var role = c.AssignedToUserId == me ? "owner" : collaborations.Contains(c.Id) ? "collaborator" : null;
                return new LogisticsMyTaskDto(c.Id, c.BoardId, c.Board, c.Title, c.Status.ToString(), c.Priority.ToString(), c.DueDate, role,
                    checklist.Count(t => t.Done), checklist.Count);
            })
            .OrderBy(t => t.Status == nameof(CardStatus.Done))
            .ThenBy(t => t.DueDate ?? DateTime.MaxValue)
            .ThenBy(t => t.Title, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("pt-PT"), true))
            .ToList();
        return EventResult<LogisticsMyTasksDto>.Ok(new LogisticsMyTasksDto(tasks, manage));
    }
}
