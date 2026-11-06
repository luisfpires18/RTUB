using System.Globalization;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Task 066 (Logística 2/5): the card as a task workspace - one owner ("Responsável", the old "Atribuir a" member) and
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

        /// <summary>Owner, collaborator or manager - and still able to see the board: assignment never grants access (067).</summary>
        public bool CanWork => Viewer && (Manager || Owner || Collaborator);
    }

    private static async Task<CardAccess> AccessAsync(ApplicationDbContext db, LogisticsCard card, ClaimsPrincipal user)
    {
        var me = LogisticsAuthorization.UserId(user);
        var boardId = await db.LogisticsLists.Where(l => l.Id == card.ListId).Select(l => l.BoardId).SingleAsync();
        var board = (await db.LogisticsBoards.FindAsync(boardId))!;
        var collaborator = me is not null && await db.LogisticsCardAssignments.AnyAsync(a => a.CardId == card.Id && a.UserId == me);
        // RefusalAsync already refused visitors and expelled accounts; the board rule (067) decides the rest.
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
            // A private board's card is not disclosed (067).
            await db.DisposeAsync();
            return (EventResult<T>.Fail(EventResultStatus.NotFound), null, null, null);
        }

        return (null, db, card, access);
    }

    // ---------------------------------------------------------------- checklist, one item at a time

    public Task<EventResult<LogisticsCardDetailDto>> AddChecklistItemAsync(int id, LogisticsChecklistItemInput input, ClaimsPrincipal user) =>
        ChangeCardAsync(id, user, assignedMayChange: true, (db, card, access) =>
        {
            var items = Tasks(card.ChecklistJson);
            if (TaskText(input.Task) is not { } task)
            {
                return Bad("task", "A tarefa precisa de texto (máx. 200 caracteres).");
            }

            if (items.Count >= MaxChecklistItems)
            {
                return Bad("task", $"No máximo {MaxChecklistItems} tarefas.");
            }

            items.Add(new StoredTask(0, task, false));
            card.SetChecklist(JsonSerializer.Serialize(items));
            Log(db, card.Id, access, "checklist", $"Tarefa adicionada: {Cut(task)}");
            return Fine();
        });

    public Task<EventResult<LogisticsCardDetailDto>> RenameChecklistItemAsync(int id, int index, LogisticsChecklistItemInput input, ClaimsPrincipal user) =>
        ChangeCardAsync(id, user, assignedMayChange: true, (db, card, access) =>
        {
            var items = Tasks(card.ChecklistJson);
            if (index < 0 || index >= items.Count || items[index].Task != input.Expected)
            {
                return Bad("items", ChecklistMoved);
            }

            if (TaskText(input.Task) is not { } task)
            {
                return Bad("task", "A tarefa precisa de texto (máx. 200 caracteres).");
            }

            if (task != items[index].Task)
            {
                Log(db, card.Id, access, "checklist", $"Tarefa editada: «{Cut(items[index].Task)}» → «{Cut(task)}»");
                items[index] = items[index] with { Task = task };
                card.SetChecklist(JsonSerializer.Serialize(items));
            }

            return Fine();
        });

    public Task<EventResult<LogisticsCardDetailDto>> RemoveChecklistItemAsync(int id, int index, string? expected, ClaimsPrincipal user) =>
        ChangeCardAsync(id, user, assignedMayChange: true, (db, card, access) =>
        {
            var items = Tasks(card.ChecklistJson);
            if (index < 0 || index >= items.Count || items[index].Task != expected)
            {
                return Bad("items", ChecklistMoved);
            }

            Log(db, card.Id, access, "checklist", $"Tarefa removida: {Cut(items[index].Task)}");
            items.RemoveAt(index);
            card.SetChecklist(JsonSerializer.Serialize(items));
            return Fine();
        });

    // ---------------------------------------------------------------- discussion

    /// <summary>Owner, collaborators and managers comment; every viewer reads. Mentions: board viewers only (≤ 20).</summary>
    public Task<EventResult<LogisticsCardDetailDto>> AddCommentAsync(int cardId, LogisticsCommentInput input, ClaimsPrincipal user) =>
        ChangeCardAsync(cardId, user, assignedMayChange: true, async (db, card, access) =>
        {
            if (CommentText(input.Body) is not { } body)
            {
                return EventResult<LogisticsCardDetailDto>.Invalid("body", $"Escreva o comentário (máx. {MaxCommentLength} caracteres).");
            }

            var mentions = (input.MentionUserIds ?? Array.Empty<string>()).Where(m => !string.IsNullOrWhiteSpace(m)).Distinct().ToList();
            if (mentions.Count > MaxMentions || (await BoardViewersAsync(db, access.Board, mentions)).Count != mentions.Count)
            {
                return EventResult<LogisticsCardDetailDto>.Invalid("mentionUserIds", "Só pode mencionar membros com acesso a este quadro (máx. 20).");
            }

            db.LogisticsCardComments.Add(new LogisticsCardComment
            {
                CardId = card.Id, AuthorId = access.UserId, Body = body, MentionsJson = mentions.Count > 0 ? JsonSerializer.Serialize(mentions) : null,
            });
            return null;
        });

    /// <summary>The author edits their own comment (its mentions stay), while they may still work on the card.</summary>
    public async Task<EventResult<LogisticsCardDetailDto>> EditCommentAsync(int commentId, LogisticsCommentInput input, ClaimsPrincipal user)
    {
        if (await CommentCardAsync(commentId) is not { } cardId)
        {
            return await RefusalAsync<LogisticsCardDetailDto>(user, manage: false) ?? EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.NotFound);
        }

        return await ChangeCardAsync(cardId, user, assignedMayChange: true, async (db, card, access) =>
        {
            if (await db.LogisticsCardComments.FirstOrDefaultAsync(c => c.Id == commentId && c.CardId == card.Id) is not { } comment)
            {
                return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.NotFound);
            }

            if (comment.AuthorId != access.UserId)
            {
                return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.Forbidden);
            }

            if (CommentText(input.Body) is not { } body)
            {
                return EventResult<LogisticsCardDetailDto>.Invalid("body", $"Escreva o comentário (máx. {MaxCommentLength} caracteres).");
            }

            if (body != comment.Body)
            {
                comment.Body = body;
                comment.EditedAt = DateTime.UtcNow;
            }

            return null;
        });
    }

    /// <summary>
    /// The author deletes their own comment; a manager deletes anyone's (moderation, one history line). Any viewer who
    /// is the author may delete, even after the assignment is gone.
    /// </summary>
    public async Task<EventResult<LogisticsCardDetailDto>> DeleteCommentAsync(int commentId, ClaimsPrincipal user)
    {
        var (refused, db, card, access) = await OpenCardAsync<LogisticsCardDetailDto>(await CommentCardAsync(commentId) ?? 0, user);
        if (refused is not null)
        {
            return refused;
        }

        await using (db)
        {
            if (access!.ReadOnly)
            {
                return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.Closed);
            }

            await using var transaction = await db!.Database.BeginTransactionAsync();
            if (await db.LogisticsCardComments.Include(c => c.Author).FirstOrDefaultAsync(c => c.Id == commentId && c.CardId == card!.Id) is not { } comment)
            {
                return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.NotFound);
            }

            var own = comment.AuthorId is not null && comment.AuthorId == access!.UserId;
            if (!own && !access!.Manager)
            {
                return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.Forbidden);
            }

            db.LogisticsCardComments.Remove(comment);
            if (!own)
            {
                Log(db, card!.Id, access!, "moderation", $"Comentário de {Person(comment.Author)?.DisplayName ?? "membro removido"} removido");
            }

            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return EventResult<LogisticsCardDetailDto>.Ok(await DetailAsync(db, card!.Id, user));
        }
    }

    private async Task<int?> CommentCardAsync(int commentId)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        return await db.LogisticsCardComments.Where(c => c.Id == commentId).Select(c => (int?)c.CardId).FirstOrDefaultAsync();
    }

    // ---------------------------------------------------------------- the card's own files and links

    /// <summary>Owner, collaborators and managers attach: the board files' types and 10 MB limit; a stable key per card.</summary>
    public async Task<EventResult<LogisticsCardDetailDto>> UploadAttachmentAsync(int cardId, LogisticsFileUpload upload, ClaimsPrincipal user)
    {
        var name = SafeFileName(upload.FileName);
        if (upload.Length <= 0 || upload.Length > MaxFileBytes)
        {
            return EventResult<LogisticsCardDetailDto>.Invalid("file", "O ficheiro é demasiado grande. Tamanho máximo: 10MB");
        }

        if (name is null || !FileTypes.Contains(upload.ContentType))
        {
            return EventResult<LogisticsCardDetailDto>.Invalid("file", "Tipo de ficheiro não suportado. Formatos permitidos: PDF, Word, Excel, TXT, PNG, JPG");
        }

        // Checked first (no upload for a refused caller), then the row is written in the locked change; the object is
        // removed again if that change is refused meanwhile.
        var (refused, db, card, access) = await OpenCardAsync<LogisticsCardDetailDto>(cardId, user);
        if (refused is not null)
        {
            return refused;
        }

        await db!.DisposeAsync();
        if (!access!.CanWork)
        {
            return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.Forbidden);
        }

        if (access.ReadOnly)
        {
            return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.Closed);
        }

        _audit.SetUser(user.Identity?.Name, access.UserId);
        var path = await _storage.UploadDocumentAsync(CardFolder(cardId), name, upload.Content, upload.ContentType);
        var result = await ChangeCardAsync(cardId, user, assignedMayChange: true, async (db, c, a) =>
        {
            if (await db.LogisticsCardAttachments.CountAsync(x => x.CardId == c.Id) >= MaxAttachments)
            {
                return EventResult<LogisticsCardDetailDto>.Invalid("file", $"No máximo {MaxAttachments} anexos por cartão.");
            }

            db.LogisticsCardAttachments.Add(new LogisticsCardAttachment
            {
                CardId = c.Id, Kind = CardAttachmentKind.File, Name = name, StoragePath = path, ContentType = upload.ContentType,
                SizeBytes = upload.Length, UploadedById = a.UserId,
            });
            Log(db, c.Id, a, "attachment", $"Ficheiro anexado: {Cut(name)}");
            return null;
        });

        if (result.Status != EventResultStatus.Ok)
        {
            await DeleteStoredAsync(new List<string> { path });
        }

        return result;
    }

    public Task<EventResult<LogisticsCardDetailDto>> AddLinkAsync(int cardId, LogisticsLinkInput input, ClaimsPrincipal user) =>
        ChangeCardAsync(cardId, user, assignedMayChange: true, async (db, card, access) =>
        {
            var raw = input.Url?.Trim() ?? "";
            if (raw.Length > 2000 || raw.Any(char.IsWhiteSpace) || !Uri.TryCreate(raw, UriKind.Absolute, out var url)
                || (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp) || string.IsNullOrEmpty(url.Host))
            {
                return EventResult<LogisticsCardDetailDto>.Invalid("url", "Indique um endereço http:// ou https:// válido.");
            }

            var name = input.Name?.Trim() is { Length: > 0 } given ? given : url.Host;
            if (name.Length > 200)
            {
                return EventResult<LogisticsCardDetailDto>.Invalid("name", "O nome não pode exceder 200 caracteres.");
            }

            if (await db.LogisticsCardAttachments.CountAsync(x => x.CardId == card.Id) >= MaxAttachments)
            {
                return EventResult<LogisticsCardDetailDto>.Invalid("url", $"No máximo {MaxAttachments} anexos por cartão.");
            }

            db.LogisticsCardAttachments.Add(new LogisticsCardAttachment
            {
                CardId = card.Id, Kind = CardAttachmentKind.Link, Name = name, Url = url.AbsoluteUri, UploadedById = access.UserId,
            });
            Log(db, card.Id, access, "attachment", $"Ligação adicionada: {Cut(name)}");
            return null;
        });

    /// <summary>Any viewer: a file's pre-signed download, or the link's address. The attachment must belong to this card.</summary>
    public async Task<EventResult<DocumentLinkDto>> GetAttachmentAsync(int cardId, int attachmentId, ClaimsPrincipal user)
    {
        var (refused, db, _, _) = await OpenCardAsync<DocumentLinkDto>(cardId, user);
        if (refused is not null)
        {
            return refused;
        }

        await using (db)
        {
            if (await db!.LogisticsCardAttachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == attachmentId && a.CardId == cardId) is not { } attachment)
            {
                return EventResult<DocumentLinkDto>.Fail(EventResultStatus.NotFound);
            }

            if (attachment.Kind == CardAttachmentKind.Link)
            {
                return EventResult<DocumentLinkDto>.Ok(new DocumentLinkDto(attachment.Url!));
            }

            return await _storage.GetDocumentUrlAsync(attachment.StoragePath!, forceDownload: true) is { } url
                ? EventResult<DocumentLinkDto>.Ok(new DocumentLinkDto(url))
                : EventResult<DocumentLinkDto>.Fail(EventResultStatus.NotFound);
        }
    }

    /// <summary>
    /// A manager, or the uploader while they still work on the card (owner or collaborator): a former collaborator only
    /// reads. Checked in the same locked change as the delete (<see cref="ChangeCardAsync"/>), so an assignment removed
    /// meanwhile refuses it. The stored file goes after the row.
    /// </summary>
    public async Task<EventResult<LogisticsCardDetailDto>> DeleteAttachmentAsync(int cardId, int attachmentId, ClaimsPrincipal user)
    {
        string? stored = null;
        var result = await ChangeCardAsync(cardId, user, assignedMayChange: true, async (db, card, access) =>
        {
            if (await db.LogisticsCardAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.CardId == card.Id) is not { } attachment)
            {
                return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.NotFound);
            }

            if (!access.Manager && (attachment.UploadedById is null || attachment.UploadedById != access.UserId))
            {
                return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.Forbidden);
            }

            stored = attachment.StoragePath;
            db.LogisticsCardAttachments.Remove(attachment);
            Log(db, card.Id, access, "attachment", $"Anexo removido: {Cut(attachment.Name)}");
            return null;
        });

        if (result.Status == EventResultStatus.Ok && stored is not null)
        {
            _audit.SetUser(user.Identity?.Name, LogisticsAuthorization.UserId(user));
            await DeleteStoredAsync(new List<string> { stored });
        }

        return result;
    }

    /// <summary>
    /// A card's files: a stable key outside the fiscal-year Documentação tree (logistics/{env}/cards/{id}/{random}/name),
    /// so they never move with the year and never show in Documentação; the random segment keeps same-named files apart.
    /// </summary>
    private string CardFolder(int cardId) => $"logistics/{_environment.EnvironmentName}/cards/{cardId}/{Guid.NewGuid():N}/";

    private static async Task<List<string>> AttachmentPathsAsync(ApplicationDbContext db, Expression<Func<LogisticsCard, bool>> cards)
    {
        var ids = db.LogisticsCards.Where(cards).Select(c => c.Id);
        return await db.LogisticsCardAttachments.Where(a => ids.Contains(a.CardId) && a.StoragePath != null).Select(a => a.StoragePath!).ToListAsync();
    }

    /// <summary>Best-effort, after the rows are gone: a leftover object is only storage, never a broken card.</summary>
    private async Task DeleteStoredAsync(List<string> paths)
    {
        foreach (var path in paths)
        {
            try
            {
                await _storage.DeleteDocumentAsync(path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete the logistics card file {Path}", path);
            }
        }
    }

    // ---------------------------------------------------------------- the details

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
            access.ReadOnly);
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
