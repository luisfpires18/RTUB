using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

// "As minhas ações pendentes" (task 073): the Logística and Questões panels, for every signed-in member (Leitões and
// Reformados included: it is their own work). Each panel is the module's own data and rules; each command is the
// module's own service call, as the member, with the roles read from the database for this call.

internal static class TaskRows
{
    /// <summary>The module's result as the page's: same status, same field errors.</summary>
    public static EventResult<bool> From<T>(EventResult<T> result) =>
        new(result.Status, result.Status == EventResultStatus.Ok, result.Errors);
}

/// <summary>
/// Logística: the member's own open cards (owner or collaborator), on boards they may see, not archived, not done - the
/// same cards as "As minhas tarefas" (<see cref="LogisticsKanbanService.WorkableCards"/>), minus done ones and, for
/// managers, minus cards without a responsible member (those are not the member's work). A card is real work: it is
/// never dismissible and leaves only when it is done (here or on the board) or no longer the member's.
/// </summary>
public sealed class LogisticsPendingActionSource : IPendingActionSource, IPendingActionCommand
{
    public const string KindCode = "logistics.card";

    private static readonly IReadOnlySet<string> Commands = new HashSet<string> { "done" };
    private readonly ILogisticsKanbanService _logistics;

    public LogisticsPendingActionSource(ILogisticsKanbanService logistics)
    {
        _logistics = logistics;
    }

    public string Kind => KindCode;
    public IReadOnlySet<string> Answers => Commands;
    public string Panel => "logistics";
    public string Title => "Logística";
    public string Category => "tasks";
    public IReadOnlyList<(string Key, string Label)> Filters => [];

    public bool AppliesTo(PendingActionMember member) => true;

    public Task<int> CountAsync(ApplicationDbContext db, PendingActionMember member, string? filter) => Query(db, member).CountAsync();

    public async Task<IReadOnlyList<PendingActionRowDto>> PageAsync(ApplicationDbContext db, PendingActionMember member, string? filter, int skip, int take)
    {
        // Soonest deadline first, cards without one last, as "As minhas tarefas".
        var cards = await Query(db, member)
            .OrderBy(c => c.DueDate == null).ThenBy(c => c.DueDate).ThenBy(c => c.Id)
            .Skip(skip).Take(take)
            .Select(c => new Card(c.Id, c.List.BoardId, c.List.Board.Name, c.Title, c.Status, c.Priority, c.DueDate, c.AssignedToUserId, c.ChecklistJson))
            .ToListAsync();
        return cards.Select(c => Row(c, member.UserId)).ToList();
    }

    public async Task<(PendingActionRowDto Row, string Period)?> FindAsync(ApplicationDbContext db, PendingActionMember member, string kind, string sourceKey)
    {
        if (kind != Kind || AttendanceFilters.Id(sourceKey) is not { } id)
        {
            return null;
        }

        var card = await Query(db, member).Where(c => c.Id == id)
            .Select(c => new Card(c.Id, c.List.BoardId, c.List.Board.Name, c.Title, c.Status, c.Priority, c.DueDate, c.AssignedToUserId, c.ChecklistJson))
            .FirstOrDefaultAsync();
        return card is null ? null : (Row(card, member.UserId), "");
    }

    /// <summary>"Concluir": the board's own status change (the card moves to the board's done column, history, version).</summary>
    public async Task<EventResult<bool>> RunAsync(ApplicationDbContext db, PendingActionMember member, string sourceKey, string answer)
    {
        if (AttendanceFilters.Id(sourceKey) is not { } id || !await Query(db, member).AnyAsync(c => c.Id == id))
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        var result = await _logistics.SetStatusAsync(id, new LogisticsStatusInput(nameof(CardStatus.Done)), member.ToPrincipal());
        // An archived board or card answers Closed in the module: for the page it is simply no longer pending.
        return result.Status == EventResultStatus.Closed ? EventResult<bool>.Fail(EventResultStatus.NotFound) : TaskRows.From(result);
    }

    private static IQueryable<RTUB.Core.Entities.LogisticsCard> Query(ApplicationDbContext db, PendingActionMember member)
    {
        var me = member.UserId;
        return LogisticsKanbanService.WorkableCards(db, member.ToPrincipal())
            .Where(c => c.Status != CardStatus.Done && (c.AssignedToUserId == me || c.Assignments.Any(a => a.UserId == me)));
    }

    private sealed record Card(int Id, int BoardId, string Board, string Title, CardStatus Status, CardPriority Priority, DateTime? DueDate,
        string? AssignedTo, string? ChecklistJson);

    private static readonly IReadOnlyDictionary<CardStatus, string> StatusNames = new Dictionary<CardStatus, string>
    {
        [CardStatus.Todo] = "Por fazer", [CardStatus.InProgress] = "Em curso", [CardStatus.Blocked] = "Bloqueado",
    };

    private static PendingActionRowDto Row(Card c, string me)
    {
        var (done, total) = LogisticsKanbanService.ChecklistProgress(c.ChecklistJson);
        var url = $"/logistics/{c.BoardId}?card={c.Id}";
        return new PendingActionRowDto(
            KindCode,
            c.Id.ToString(CultureInfo.InvariantCulture),
            c.Title,
            c.DueDate is { } due ? AttendanceFilters.Day(due) : null,
            null,
            AttendanceFilters.Join(
                c.Board,
                StatusNames.GetValueOrDefault(c.Status),
                c.Priority >= CardPriority.High ? (c.Priority == CardPriority.Urgent ? "Urgente" : "Prioridade alta") : null,
                total > 0 ? $"Lista {done}/{total}" : null,
                c.AssignedTo == me ? "Responsável" : "Colaborador"),
            Required: false,
            Dismissible: false,
            [new PendingActionButtonDto("Concluir", null, true, "done"), new PendingActionButtonDto("Abrir", url, false)],
            url);
    }
}

/// <summary>
/// Questões: the member's own open questions (author, not closed, not deleted) - the ones QuestionsAuthorization lets
/// them close. "Fechar questão" is the module's own close (author only; thread and replies kept). Answered ones (the
/// member's turn) first, then the most recent activity.
/// </summary>
public sealed class QuestionsPendingActionSource : IPendingActionSource, IPendingActionCommand
{
    public const string KindCode = "question.close";

    private static readonly IReadOnlySet<string> Commands = new HashSet<string> { "close" };
    private readonly IQuestionBoardService _questions;

    public QuestionsPendingActionSource(IQuestionBoardService questions)
    {
        _questions = questions;
    }

    public string Kind => KindCode;
    public IReadOnlySet<string> Answers => Commands;
    public string Panel => "questions";
    public string Title => "Questões";
    public string Category => "tasks";
    public IReadOnlyList<(string Key, string Label)> Filters => [];

    /// <summary>Task 087: only a member of the Direção in office (owner decision); the rows stay their own open questions.</summary>
    public bool AppliesTo(PendingActionMember member) => member.HoldsDirecao;

    public Task<int> CountAsync(ApplicationDbContext db, PendingActionMember member, string? filter) => Query(db, member).CountAsync();

    public async Task<IReadOnlyList<PendingActionRowDto>> PageAsync(ApplicationDbContext db, PendingActionMember member, string? filter, int skip, int take)
    {
        var questions = await Query(db, member)
            .OrderByDescending(q => q.IsAwaitingUserReply).ThenByDescending(q => q.LastActivityAt).ThenByDescending(q => q.Id)
            .Skip(skip).Take(take)
            .Select(q => new Item(q.Id, q.Title, q.AssignedPosition, q.IsAwaitingUserReply, q.Status))
            .ToListAsync();
        return questions.Select(Row).ToList();
    }

    public async Task<(PendingActionRowDto Row, string Period)?> FindAsync(ApplicationDbContext db, PendingActionMember member, string kind, string sourceKey)
    {
        if (kind != Kind || AttendanceFilters.Id(sourceKey) is not { } id)
        {
            return null;
        }

        var item = await Query(db, member).Where(q => q.Id == id)
            .Select(q => new Item(q.Id, q.Title, q.AssignedPosition, q.IsAwaitingUserReply, q.Status)).FirstOrDefaultAsync();
        return item is null ? null : (Row(item), "");
    }

    public async Task<EventResult<bool>> RunAsync(ApplicationDbContext db, PendingActionMember member, string sourceKey, string answer)
    {
        if (AttendanceFilters.Id(sourceKey) is not { } id || !await Query(db, member).AnyAsync(q => q.Id == id))
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        return TaskRows.From(await _questions.CloseAsync(id, member.ToPrincipal()));
    }

    private static IQueryable<RTUB.Core.Entities.Question> Query(ApplicationDbContext db, PendingActionMember member) =>
        db.Questions.AsNoTracking().Where(q => q.AuthorId == member.UserId && q.Status != QuestionStatus.Closed && !q.IsDeleted);

    private sealed record Item(int Id, string Title, Position Position, bool YourTurn, QuestionStatus Status);

    private static PendingActionRowDto Row(Item q)
    {
        var url = $"/questions?question={q.Id}";
        return new PendingActionRowDto(
            KindCode,
            q.Id.ToString(CultureInfo.InvariantCulture),
            q.Title,
            null,
            null,
            AttendanceFilters.Join($"Para {StatusHelper.GetPositionDisplay(q.Position)}", q.YourTurn ? "Respondida" : "À espera de resposta"),
            Required: false,
            Dismissible: false,
            [
                new PendingActionButtonDto("Fechar questão", null, q.YourTurn, "close",
                    "Fechar esta questão? Deixa de aceitar respostas; a conversa fica guardada."),
                new PendingActionButtonDto("Abrir", url, !q.YourTurn),
            ],
            url);
    }
}
