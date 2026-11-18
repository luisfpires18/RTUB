using System.Globalization;
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
/// The card as a task workspace - one owner ("Responsável", the "Atribuir a" member) and
/// collaborators (the "Membros atribuídos" rows), priority, a version against silent overwrites, checklist edits one item
/// at a time, a discussion with @mentions, a history and the card's own files and links.
///
/// Every card rule goes through <see cref="AccessAsync"/>: phase 3 (private boards) adds board access there, once.
/// </summary>
public sealed partial class LogisticsKanbanService
{
    public const int MaxChecklistItems = 100;

    public const int MaxCommentLength = 2000;

    public const int MaxMentions = 20;

    public const int MaxAttachments = 50;

    public const int ActivityShown = 100;

    private const string ChecklistMoved = "A lista de tarefas mudou entretanto. Recarregue o cartão.";

    /// <summary>
    /// What the caller may do on one card. <c>Viewer</c>: reads it (every signed-in member today; phase 3 adds board
    /// access). <c>CanWork</c>: owner, collaborator or manager - edits its fields and checklist, status, comments,
    /// attaches. <c>Manager</c>: everything else.
    /// </summary>
    private sealed record CardAccess(LogisticsBoard Board, string? UserId, bool Viewer, bool Manager, bool Owner, bool Collaborator, bool ReadOnly)
    {
        public int BoardId => Board.Id;

        /// <summary>Owner, collaborator or manager - and still able to see the board: assignment never grants access.</summary>
        public bool CanWork => Viewer && (Manager || Owner || Collaborator);

        /// <summary>Pushes the change sends once it is saved (<see cref="LogisticsNotifier"/>).</summary>
        public List<(LogisticsNotice Kind, string[] To)> Notices { get; } = new();
    }

    private static async Task<CardAccess> AccessAsync(ApplicationDbContext db, LogisticsCard card, ClaimsPrincipal user)
    {
        var me = LogisticsAuthorization.UserId(user);
        var boardId = await db.LogisticsLists.Where(l => l.Id == card.ListId).Select(l => l.BoardId).SingleAsync();
        var board = (await db.LogisticsBoards.FindAsync(boardId))!;
        var collaborator = me is not null && await db.LogisticsCardAssignments.AnyAsync(a => a.CardId == card.Id && a.UserId == me);
        // RefusalAsync already refused visitors and expelled accounts; the board rule decides the rest.
        return new CardAccess(board, me, Viewer: me is not null && await CanSeeBoardAsync(db, board, user), LogisticsAuthorization.CanManage(user),
            Owner: me is not null && card.AssignedToUserId == me, collaborator, ReadOnly: card.ArchivedAt is not null || board.ArchivedAt is not null);
    }

    /// <summary>The caller's view of one card: 401 / 403 / 404, or the card and the access.</summary>
    private async Task<(EventResult<T>? Refused, ApplicationDbContext? Db, LogisticsCard? Card, CardAccess? Access)> OpenCardAsync<T>(int cardId,
        ClaimsPrincipal user)
    {
        if (await RefusalAsync<T>(user, manage: false) is { } refused)
        {
            return (refused, null, null, null);
        }

        var db = await _contexts.CreateDbContextAsync();
        if (await db.LogisticsCards.FindAsync(cardId) is not { } card)
        {
            await db.DisposeAsync();
            return (EventResult<T>.Fail(EventResultStatus.NotFound), null, null, null);
        }

        var access = await AccessAsync(db, card, user);
        if (!access.Viewer)
        {
            // A private board's card is not disclosed.
            await db.DisposeAsync();
            return (EventResult<T>.Fail(EventResultStatus.NotFound), null, null, null);
        }

        return (null, db, card, access);
    }

    // ---------------------------------------------------------------- checklist, one item at a time

    /// <summary>
    /// The workspace for <paramref name="user"/>: member ids only for managers (they change assignments). The owner is
    /// left out of the collaborators. History: newest first, the last <see cref="ActivityShown"/>.
    /// </summary>
    private static async Task<LogisticsCardDetailDto> DetailAsync(ApplicationDbContext db, int id, ClaimsPrincipal user)
    {
        var card = await db.LogisticsCards.AsNoTracking().Include(c => c.Event).Include(c => c.AssignedToUser).SingleAsync(c => c.Id == id);
        var collaborators = await db.LogisticsCardAssignments.AsNoTracking().Include(a => a.User)
            .Where(a => a.CardId == id && a.UserId != card.AssignedToUserId).OrderBy(a => a.Id).Select(a => a.User).ToListAsync();
        var access = await AccessAsync(db, card, user);
        var comments = await db.LogisticsCardComments.AsNoTracking().Include(c => c.Author).Where(c => c.CardId == id)
            .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id).ToListAsync();
        var mentioned = comments.SelectMany(c => Read<string>(c.MentionsJson)).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => mentioned.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => Person(u)!.DisplayName);
        var attachments = await db.LogisticsCardAttachments.AsNoTracking().Include(a => a.UploadedBy).Where(a => a.CardId == id)
            .OrderBy(a => a.CreatedAt).ThenBy(a => a.Id).ToListAsync();
        var activity = await db.LogisticsCardActivities.AsNoTracking().Include(a => a.Actor).Where(a => a.CardId == id)
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id).Take(ActivityShown).ToListAsync();
        var (reminders, canRemind) = await RemindersAsync(db, id, access);

        return new LogisticsCardDetailDto(
            Face(card, collaborators.Count, access.Owner || access.Collaborator, comments.Count, attachments.Count),
            card.ListId,
            card.AssignedToUser is null ? null : Assignee(card.AssignedToUser, access.Manager),
            Tasks(card.ChecklistJson).Select(t => new LogisticsChecklistItemDto(t.Task, t.Done)).ToList(),
            Links(card.AttachmentsJson).Select(l => new LogisticsLinkDto(l.CardId, l.Name)).ToList(),
            collaborators.Select(a => Assignee(a, access.Manager)).ToList(),
            access.Manager,
            access.CanWork && !access.ReadOnly,
            card.Version,
            comments.Select(c => new LogisticsCommentDto(c.Id, Person(c.Author), c.Body,
                Read<string>(c.MentionsJson).Where(names.ContainsKey).Select(m => names[m]).ToList(), Utc(c.CreatedAt), c.EditedAt is { } e ? Utc(e) : null,
                CanEdit: c.AuthorId is not null && c.AuthorId == access.UserId && access.CanWork && !access.ReadOnly,
                CanDelete: !access.ReadOnly && (access.Manager || (c.AuthorId is not null && c.AuthorId == access.UserId)))).ToList(),
            attachments.Select(a => new LogisticsAttachmentDto(a.Id, a.Kind.ToString(), a.Name,
                a.Kind == CardAttachmentKind.Link && Uri.TryCreate(a.Url, UriKind.Absolute, out var u) ? u.Host : null,
                a.SizeBytes, a.Kind == CardAttachmentKind.File ? Path.GetExtension(a.Name) : null, Person(a.UploadedBy), Utc(a.CreatedAt),
                CanDelete: !access.ReadOnly && (access.Manager || (access.CanWork && a.UploadedById is not null && a.UploadedById == access.UserId)))).ToList(),
            activity.Select(a => new LogisticsActivityDto(Person(a.Actor), a.Action, a.Detail, Utc(a.CreatedAt))).ToList(),
            card.ArchivedAt is null ? null : Utc(card.ArchivedAt.Value),
            access.ReadOnly,
            reminders,
            canRemind);
    }

    // ---------------------------------------------------------------- small helpers

    /// <summary>Stored timestamps are UTC (SQLite gives them back unspecified): say so, so the page shows Portugal time.</summary>
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static void Log(ApplicationDbContext db, int cardId, CardAccess access, string action, string detail) =>
        db.LogisticsCardActivities.Add(new LogisticsCardActivity { CardId = cardId, ActorId = access.UserId, Action = action, Detail = Cut(detail, 400) });

    private static Task<EventResult<LogisticsCardDetailDto>?> Fine() => Task.FromResult<EventResult<LogisticsCardDetailDto>?>(null);

    private static Task<EventResult<LogisticsCardDetailDto>?> Bad(string field, string message) =>
        Task.FromResult<EventResult<LogisticsCardDetailDto>?>(EventResult<LogisticsCardDetailDto>.Invalid(field, message));

    private static string Cut(string text, int max = 80) => text.Length <= max ? text : $"{text[..(max - 1)]}…";

    private static string? TaskText(string? value) => value?.Trim() is { Length: > 0 and <= 200 } task ? task : null;

    private static string? CommentText(string? value) =>
        value?.Replace("\r\n", "\n").Trim() is { Length: > 0 and <= MaxCommentLength } body ? body : null;

    /// <summary>The browser's name without any path, quotes or control characters (it ends up in a download header).</summary>
    private static string? SafeFileName(string raw)
    {
        var name = new string(raw.Split('/', '\\')[^1].Where(c => !char.IsControl(c) && c != '"').ToArray()).Trim();
        if (name.Length > 150)
        {
            name = name[^150..];
        }

        return name == "" || name.StartsWith('.') ? null : name;
    }

    private static string PriorityName(CardPriority p) => p switch
    {
        CardPriority.Low => "Baixa",
        CardPriority.High => "Alta",
        CardPriority.Urgent => "Urgente",
        _ => "Normal",
    };

    private static string StatusName(CardStatus s) => s switch
    {
        CardStatus.InProgress => "WIP",
        CardStatus.Done => "DONE",
        _ => "TODO",
    };

    private static string DayText(DateTime? day) => day?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "";

    private static async Task<string> NameAsync(ApplicationDbContext db, string userId) =>
        Person(await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId))?.DisplayName ?? "membro";
}
