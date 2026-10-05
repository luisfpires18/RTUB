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
/// - reminders: any member who sees the board, as before; stored only - nothing in the app sends them (unchanged).
/// No notifications, no schema change.
/// </summary>
public sealed class LogisticsKanbanService : ILogisticsKanbanService
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
    private readonly IHostEnvironment _environment;
    private readonly AuditContext _audit;
    private readonly ILogger<LogisticsKanbanService> _logger;

    public LogisticsKanbanService(IDbContextFactory<ApplicationDbContext> contexts, IDocumentStorageService storage, IHostEnvironment environment,
        AuditContext audit, ILogger<LogisticsKanbanService> logger)
    {
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

    public async Task<EventResult<LogisticsBoardsDto>> GetBoardsAsync(string? search, ClaimsPrincipal user)
    {
        if (await RefusalAsync<LogisticsBoardsDto>(user, manage: false) is { } refused)
        {
            return refused;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        var q = search?.Trim() ?? "";
        var boards = await db.LogisticsBoards.AsNoTracking().Include(b => b.Event)
            .Where(b => q == "" || b.Name.Contains(q) || b.Description.Contains(q))
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        return EventResult<LogisticsBoardsDto>.Ok(new LogisticsBoardsDto(
            boards.Where(b => !b.IsCompleted).Select(Summary).ToList(),
            boards.Where(b => b.IsCompleted).Select(Summary).ToList(),
            LogisticsAuthorization.CanManage(user)));
    }

    public async Task<EventResult<LogisticsBoardSummaryDto>> CreateBoardAsync(LogisticsBoardInput input, ClaimsPrincipal user)
    {
        if (await RefusalAsync<LogisticsBoardSummaryDto>(user, manage: true) is { } refused)
        {
            return refused;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await BoardErrorsAsync(db, input) is { } invalid)
        {
            return invalid;
        }

        var board = LogisticsBoard.Create(input.Name!.Trim(), input.Description?.Trim() ?? "");
        board.AssociateWithEvent(input.EventId);
        db.LogisticsBoards.Add(board);
        await db.SaveChangesAsync();
        return EventResult<LogisticsBoardSummaryDto>.Ok(await BoardSummaryAsync(db, board.Id));
    }

    public async Task<EventResult<LogisticsBoardSummaryDto>> UpdateBoardAsync(int id, LogisticsBoardInput input, ClaimsPrincipal user)
    {
        if (await RefusalAsync<LogisticsBoardSummaryDto>(user, manage: true) is { } refused)
        {
            return refused;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await db.LogisticsBoards.FindAsync(id) is not { } board)
        {
            return EventResult<LogisticsBoardSummaryDto>.Fail(EventResultStatus.NotFound);
        }

        if (await BoardErrorsAsync(db, input) is { } invalid)
        {
            return invalid;
        }

        board.UpdateDetails(input.Name!.Trim(), input.Description?.Trim() ?? "");
        board.AssociateWithEvent(input.EventId);
        await db.SaveChangesAsync();
        return EventResult<LogisticsBoardSummaryDto>.Ok(await BoardSummaryAsync(db, id));
    }

    public async Task<EventResult<LogisticsBoardSummaryDto>> SetBoardStateAsync(int id, LogisticsBoardStateInput input, ClaimsPrincipal user)
    {
        if (await RefusalAsync<LogisticsBoardSummaryDto>(user, manage: true) is { } refused)
        {
            return refused;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await db.LogisticsBoards.FindAsync(id) is not { } board)
        {
            return EventResult<LogisticsBoardSummaryDto>.Fail(EventResultStatus.NotFound);
        }

        if (input.Completed)
        {
            board.MarkAsCompleted();
        }
        else
        {
            board.MarkAsNotCompleted();
        }

        await db.SaveChangesAsync();
        return EventResult<LogisticsBoardSummaryDto>.Ok(await BoardSummaryAsync(db, id));
    }

    public async Task<EventResult<bool>> DeleteBoardAsync(int id, ClaimsPrincipal user)
    {
        if (await RefusalAsync<bool>(user, manage: true) is { } refused)
        {
            return refused;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await db.LogisticsBoards.Include(b => b.Lists).ThenInclude(l => l.Cards).FirstOrDefaultAsync(b => b.Id == id) is not { } board)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        // Its lists and cards go with it, as before (their assignments and reminders by cascade). Board files stay in
        // storage, as before.
        db.LogisticsBoards.Remove(board);
        await db.SaveChangesAsync();
        return EventResult<bool>.Ok(true);
    }

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

    public async Task<EventResult<IReadOnlyList<LogisticsMemberOptionDto>>> SearchMembersAsync(string? search, ClaimsPrincipal user)
    {
        if (await RefusalAsync<IReadOnlyList<LogisticsMemberOptionDto>>(user, manage: false) is { } refused)
        {
            return refused;
        }

        var words = EventParticipantsAdminService.Fold(search).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return EventResult<IReadOnlyList<LogisticsMemberOptionDto>>.Ok(Array.Empty<LogisticsMemberOptionDto>());
        }

        await using var db = await _contexts.CreateDbContextAsync();
        // ponytail: every member in memory (~100), as the old pages loaded every user; page it if it grows.
        var members = await db.Users.AsNoTracking().Where(u => !u.IsExpelled).ToListAsync();
        return EventResult<IReadOnlyList<LogisticsMemberOptionDto>>.Ok(members
            .Select(u => (u, who: GovernanceService.ToMember(u.Nickname, u.FirstName, u.LastName, u.ImageUrl),
                text: EventParticipantsAdminService.Fold($"{u.UserName} {u.Nickname} {u.FirstName} {u.LastName}")))
            .Where(x => words.All(w => x.text.Contains(w)))
            .OrderBy(x => x.who.DisplayName, StringComparer.Create(CultureInfo.GetCultureInfo("pt-PT"), true))
            .Take(SearchLimit)
            .Select(x => new LogisticsMemberOptionDto(x.u.Id, x.who.DisplayName, x.who.FullName, x.who.AvatarUrl))
            .ToList());
    }

    // ---------------------------------------------------------------- one board

    public async Task<EventResult<LogisticsBoardDto>> GetBoardAsync(int id, ClaimsPrincipal user)
    {
        if (await RefusalAsync<LogisticsBoardDto>(user, manage: false) is { } refused)
        {
            return refused;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await db.LogisticsBoards.AsNoTracking().Include(b => b.Event).FirstOrDefaultAsync(b => b.Id == id) is not { } board)
        {
            return EventResult<LogisticsBoardDto>.Fail(EventResultStatus.NotFound);
        }

        var lists = await db.LogisticsLists.AsNoTracking().Where(l => l.BoardId == id).OrderBy(l => l.Position).ThenBy(l => l.Id).ToListAsync();
        var listIds = lists.Select(l => l.Id).ToList();
        var cards = await db.LogisticsCards.AsNoTracking().Include(c => c.Event).Include(c => c.AssignedToUser)
            .Where(c => listIds.Contains(c.ListId)).ToListAsync();
        var assignments = await AssignmentCountsAsync(db, cards.Select(c => c.Id).ToList());
        var faces = cards.ToDictionary(c => c.Id, c => Face(c, assignments.GetValueOrDefault(c.Id)));

        return EventResult<LogisticsBoardDto>.Ok(new LogisticsBoardDto(
            board.Id, board.Name, board.Description, Event(board.Event), board.IsCompleted,
            lists.Select(l => new LogisticsListDto(l.Id, l.Name, cards.Where(c => c.ListId == l.Id).OrderBy(c => c.Position).ThenBy(c => c.Id)
                .Select(c => faces[c.Id]).ToList())).ToList(),
            faces.Values.SelectMany(c => c.Labels).Select(l => l.Text).Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList(),
            (await FilesAsync(board.Name)).Select(d => new LogisticsFileDto(d.FileName, d.Extension, d.SizeBytes)).ToList(),
            LogisticsAuthorization.CanManage(user)));
    }

    public async Task<EventResult<LogisticsListDto>> CreateListAsync(int boardId, LogisticsListInput input, ClaimsPrincipal user)
    {
        if (await RefusalAsync<LogisticsListDto>(user, manage: true) is { } refused)
        {
            return refused;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (!await db.LogisticsBoards.AnyAsync(b => b.Id == boardId))
        {
            return EventResult<LogisticsListDto>.Fail(EventResultStatus.NotFound);
        }

        if (ListName(input) is not { } name)
        {
            return EventResult<LogisticsListDto>.Invalid("name", "O nome da lista é obrigatório (máx. 100 caracteres).");
        }

        // Last, as before.
        var last = await db.LogisticsLists.Where(l => l.BoardId == boardId).Select(l => (int?)l.Position).MaxAsync() ?? -1;
        var list = LogisticsList.Create(name, boardId, last + 1);
        db.LogisticsLists.Add(list);
        await db.SaveChangesAsync();
        return EventResult<LogisticsListDto>.Ok(new LogisticsListDto(list.Id, list.Name, Array.Empty<LogisticsCardDto>()));
    }

    public async Task<EventResult<LogisticsListDto>> RenameListAsync(int id, LogisticsListInput input, ClaimsPrincipal user)
    {
        if (await RefusalAsync<LogisticsListDto>(user, manage: true) is { } refused)
        {
            return refused;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await db.LogisticsLists.FindAsync(id) is not { } list)
        {
            return EventResult<LogisticsListDto>.Fail(EventResultStatus.NotFound);
        }

        if (ListName(input) is not { } name)
        {
            return EventResult<LogisticsListDto>.Invalid("name", "O nome da lista é obrigatório (máx. 100 caracteres).");
        }

        list.UpdateName(name);
        await db.SaveChangesAsync();
        return EventResult<LogisticsListDto>.Ok(new LogisticsListDto(list.Id, list.Name, Array.Empty<LogisticsCardDto>()));
    }

    public async Task<EventResult<bool>> MoveListAsync(int id, LogisticsPositionInput input, ClaimsPrincipal user)
    {
        if (await RefusalAsync<bool>(user, manage: true) is { } refused)
        {
            return refused;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await db.LogisticsLists.FindAsync(id) is not { } list)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        var others = await db.LogisticsLists.Where(l => l.BoardId == list.BoardId && l.Id != id).OrderBy(l => l.Position).ThenBy(l => l.Id).ToListAsync();
        others.Insert(Math.Clamp(input.Position, 0, others.Count), list);
        for (var i = 0; i < others.Count; i++)
        {
            others[i].UpdatePosition(i);
        }

        await db.SaveChangesAsync();
        return EventResult<bool>.Ok(true);
    }

    public async Task<EventResult<bool>> DeleteListAsync(int id, ClaimsPrincipal user)
    {
        if (await RefusalAsync<bool>(user, manage: true) is { } refused)
        {
            return refused;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await db.LogisticsLists.Include(l => l.Cards).FirstOrDefaultAsync(l => l.Id == id) is not { } list)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        db.LogisticsLists.Remove(list);
        await db.SaveChangesAsync();
        return EventResult<bool>.Ok(true);
    }

    // ---------------------------------------------------------------- cards

    public async Task<EventResult<LogisticsCardDetailDto>> CreateCardAsync(int listId, LogisticsCardCreateInput input, ClaimsPrincipal user)
    {
        if (await RefusalAsync<LogisticsCardDetailDto>(user, manage: true) is { } refused)
        {
            return refused;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (!await db.LogisticsLists.AnyAsync(l => l.Id == listId))
        {
            return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.NotFound);
        }

        var errors = CardErrors(input.Title, input.Description);
        if (!string.IsNullOrEmpty(input.AssignedToUserId) && !await db.Users.AnyAsync(u => u.Id == input.AssignedToUserId))
        {
            errors["assignedToUserId"] = new[] { "Membro não encontrado." };
        }

        if (errors.Count > 0)
        {
            return new EventResult<LogisticsCardDetailDto>(EventResultStatus.Invalid, Errors: errors);
        }

        // Last in its list, as before.
        var last = await db.LogisticsCards.Where(c => c.ListId == listId).Select(c => (int?)c.Position).MaxAsync() ?? -1;
        var card = LogisticsCard.Create(input.Title!.Trim(), listId, last + 1, input.Description?.Trim() ?? "");
        card.AssignToUser(string.IsNullOrEmpty(input.AssignedToUserId) ? null : input.AssignedToUserId);
        db.LogisticsCards.Add(card);
        await db.SaveChangesAsync();
        return EventResult<LogisticsCardDetailDto>.Ok(await DetailAsync(db, card.Id));
    }

    public async Task<EventResult<LogisticsCardDetailDto>> GetCardAsync(int id, ClaimsPrincipal user)
    {
        if (await RefusalAsync<LogisticsCardDetailDto>(user, manage: true) is { } refused)
        {
            return refused;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        return await db.LogisticsCards.AnyAsync(c => c.Id == id)
            ? EventResult<LogisticsCardDetailDto>.Ok(await DetailAsync(db, id))
            : EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.NotFound);
    }

    public Task<EventResult<LogisticsCardDetailDto>> UpdateCardAsync(int id, LogisticsCardInput input, ClaimsPrincipal user) =>
        ChangeCardAsync(id, user, async (db, card) =>
        {
            var errors = CardErrors(input.Title, input.Description);
            var start = Day(input.StartDate, "startDate", errors);
            var due = Day(input.DueDate, "dueDate", errors);
            if (!string.IsNullOrEmpty(input.AssignedToUserId) && !await db.Users.AnyAsync(u => u.Id == input.AssignedToUserId))
            {
                errors["assignedToUserId"] = new[] { "Membro não encontrado." };
            }

            if (errors.Count > 0)
            {
                return errors;
            }

            // A description that still carries the old base64 file block keeps it, unseen, as the old page did.
            var stored = card.Description ?? "";
            var marker = stored.IndexOf(LegacyFilesMarker, StringComparison.Ordinal);
            var description = input.Description?.Trim() ?? "";
            card.UpdateContent(input.Title!.Trim(), marker < 0 ? description : $"{description}\n\n{stored[marker..]}");
            card.SetDates(start, due, card.ReminderDate);
            card.AssignToUser(string.IsNullOrEmpty(input.AssignedToUserId) ? null : input.AssignedToUserId);
            return null;
        });

    public Task<EventResult<LogisticsCardDetailDto>> SetStatusAsync(int id, LogisticsStatusInput input, ClaimsPrincipal user) =>
        ChangeCardAsync(id, user, (db, card) =>
        {
            if (!Enum.TryParse<CardStatus>(input.Status, out var status) || !Enum.IsDefined(status) || int.TryParse(input.Status, out _))
            {
                return Task.FromResult<Dictionary<string, string[]>?>(Error("status", "Estado inválido."));
            }

            card.SetStatus(status);
            return Task.FromResult<Dictionary<string, string[]>?>(null);
        });

    public async Task<EventResult<bool>> MoveCardAsync(int id, LogisticsMoveInput input, ClaimsPrincipal user)
    {
        if (await RefusalAsync<bool>(user, manage: true) is { } refused)
        {
            return refused;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await db.LogisticsCards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == id) is not { } card)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        if (await db.LogisticsLists.FindAsync(input.ListId) is not { } target || target.BoardId != card.List.BoardId)
        {
            return EventResult<bool>.Invalid("listId", "Escolha uma lista deste quadro.");
        }

        var from = card.ListId;
        var entering = await db.LogisticsCards.Where(c => c.ListId == target.Id && c.Id != id).OrderBy(c => c.Position).ThenBy(c => c.Id).ToListAsync();
        entering.Insert(Math.Clamp(input.Position, 0, entering.Count), card);
        card.MoveToList(target.Id, 0);
        Renumber(entering);
        if (from != target.Id)
        {
            Renumber(await db.LogisticsCards.Where(c => c.ListId == from && c.Id != id).OrderBy(c => c.Position).ThenBy(c => c.Id).ToListAsync());
        }

        await db.SaveChangesAsync();
        return EventResult<bool>.Ok(true);
    }

    public Task<EventResult<LogisticsCardDetailDto>> SetLabelsAsync(int id, LogisticsLabelsInput input, ClaimsPrincipal user) =>
        ChangeCardAsync(id, user, (db, card) =>
        {
            var labels = input.Labels ?? Array.Empty<LogisticsLabelDto>();
            if (labels.Count > 20 || labels.Any(l => string.IsNullOrWhiteSpace(l.Text) || l.Text.Trim().Length > 50 || !Colour.IsMatch(l.Color ?? "")))
            {
                return Task.FromResult<Dictionary<string, string[]>?>(Error("labels", "Cada etiqueta precisa de um nome (máx. 50) e de uma cor; no máximo 20."));
            }

            card.SetLabels(JsonSerializer.Serialize(labels.Select(l => new StoredLabel(0, l.Text.Trim(), l.Color.ToLowerInvariant()))));
            return Task.FromResult<Dictionary<string, string[]>?>(null);
        });

    public Task<EventResult<LogisticsCardDetailDto>> SetChecklistAsync(int id, LogisticsChecklistInput input, ClaimsPrincipal user) =>
        ChangeCardAsync(id, user, (db, card) =>
        {
            var items = input.Items ?? Array.Empty<LogisticsChecklistItemDto>();
            if (items.Count > 100 || items.Any(i => string.IsNullOrWhiteSpace(i.Task) || i.Task.Trim().Length > 200))
            {
                return Task.FromResult<Dictionary<string, string[]>?>(Error("items", "Cada tarefa precisa de texto (máx. 200); no máximo 100."));
            }

            card.SetChecklist(JsonSerializer.Serialize(items.Select(i => new StoredTask(0, i.Task.Trim(), i.Done))));
            return Task.FromResult<Dictionary<string, string[]>?>(null);
        });

    public Task<EventResult<LogisticsCardDetailDto>> SetLinksAsync(int id, LogisticsLinksInput input, ClaimsPrincipal user) =>
        ChangeCardAsync(id, user, async (db, card) =>
        {
            var ids = (input.CardIds ?? Array.Empty<int>()).Distinct().ToList();
            var boardId = await db.LogisticsLists.Where(l => l.Id == card.ListId).Select(l => l.BoardId).SingleAsync();
            var linked = await db.LogisticsCards.AsNoTracking().Where(c => ids.Contains(c.Id) && c.Id != id && c.List.BoardId == boardId)
                .ToDictionaryAsync(c => c.Id, c => c.Title);
            if (ids.Count > 50 || linked.Count != ids.Count)
            {
                return Error("cardIds", "Escolha cartões deste quadro (outros que não este).");
            }

            // Each link keeps a copy of the linked card's title, as before; saving the links refreshes those copies.
            card.SetAttachments(JsonSerializer.Serialize(ids.Select(c => new StoredLink(0, linked[c], c))));
            return null;
        });

    public async Task<EventResult<LogisticsCardDetailDto>> AddAssignmentAsync(int id, LogisticsAssignmentInput input, ClaimsPrincipal user)
    {
        if (await RefusalAsync<LogisticsCardDetailDto>(user, manage: true) is { } refused)
        {
            return refused;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (!await db.LogisticsCards.AnyAsync(c => c.Id == id))
        {
            return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.NotFound);
        }

        if (string.IsNullOrEmpty(input.UserId) || !await db.Users.AnyAsync(u => u.Id == input.UserId))
        {
            return EventResult<LogisticsCardDetailDto>.Invalid("userId", "Membro não encontrado.");
        }

        if (await db.LogisticsCardAssignments.AnyAsync(a => a.CardId == id && a.UserId == input.UserId))
        {
            return EventResult<LogisticsCardDetailDto>.Invalid("userId", "Utilizador já está atribuído a este cartão");
        }

        db.LogisticsCardAssignments.Add(LogisticsCardAssignment.Create(id, input.UserId));
        await db.SaveChangesAsync();
        return EventResult<LogisticsCardDetailDto>.Ok(await DetailAsync(db, id));
    }

    public async Task<EventResult<LogisticsCardDetailDto>> RemoveAssignmentAsync(int id, string userId, ClaimsPrincipal user)
    {
        if (await RefusalAsync<LogisticsCardDetailDto>(user, manage: true) is { } refused)
        {
            return refused;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await db.LogisticsCardAssignments.FirstOrDefaultAsync(a => a.CardId == id && a.UserId == userId) is not { } assignment)
        {
            return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.NotFound);
        }

        db.LogisticsCardAssignments.Remove(assignment);
        await db.SaveChangesAsync();
        return EventResult<LogisticsCardDetailDto>.Ok(await DetailAsync(db, id));
    }

    public async Task<EventResult<bool>> DeleteCardAsync(int id, ClaimsPrincipal user)
    {
        if (await RefusalAsync<bool>(user, manage: true) is { } refused)
        {
            return refused;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await db.LogisticsCards.FindAsync(id) is not { } card)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        db.LogisticsCards.Remove(card);
        await db.SaveChangesAsync();
        return EventResult<bool>.Ok(true);
    }

    // ---------------------------------------------------------------- reminders

    public async Task<EventResult<LogisticsReminderDto>> CreateReminderAsync(int boardId, LogisticsReminderInput input, ClaimsPrincipal user)
    {
        // Every member who sees the board, as before (not only managers).
        if (await RefusalAsync<LogisticsReminderDto>(user, manage: false) is { } refused)
        {
            return refused;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (!await db.LogisticsBoards.AnyAsync(b => b.Id == boardId))
        {
            return EventResult<LogisticsReminderDto>.Fail(EventResultStatus.NotFound);
        }

        var errors = new Dictionary<string, string[]>();
        if (!await db.LogisticsCards.AnyAsync(c => c.Id == input.CardId && c.List.BoardId == boardId))
        {
            errors["cardId"] = new[] { "Selecione um cartão deste quadro." };
        }

        if (!Enum.TryParse<ReminderFrequency>(input.Frequency, out var frequency) || !Enum.IsDefined(frequency) || int.TryParse(input.Frequency, out _))
        {
            errors["frequency"] = new[] { "Frequência inválida." };
        }

        if (input.NextReminderDate is not { } next)
        {
            errors["nextReminderDate"] = new[] { "Indique a primeira data do lembrete." };
        }

        var userIds = (input.UserIds ?? Array.Empty<string>()).Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().ToList();
        if (userIds.Count == 0 || userIds.Count > 100 || await db.Users.CountAsync(u => userIds.Contains(u.Id)) != userIds.Count)
        {
            errors["userIds"] = new[] { "Pelo menos um utilizador deve ser selecionado" };
        }

        if (errors.Count > 0)
        {
            return new EventResult<LogisticsReminderDto>(EventResultStatus.Invalid, Errors: errors);
        }

        var reminder = LogisticsCardReminder.Create(input.CardId, frequency, string.Join(",", userIds), input.NextReminderDate!.Value);
        db.LogisticsCardReminders.Add(reminder);
        await db.SaveChangesAsync();
        return EventResult<LogisticsReminderDto>.Ok(new LogisticsReminderDto(reminder.Id, reminder.CardId, reminder.Frequency.ToString(),
            reminder.NextReminderDate, userIds.Count));
    }

    // ---------------------------------------------------------------- board files

    public async Task<EventResult<LogisticsFileDto>> UploadFileAsync(int boardId, LogisticsFileUpload upload, ClaimsPrincipal user)
    {
        if (await RefusalAsync<LogisticsFileDto>(user, manage: true) is { } refused)
        {
            return refused;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await db.LogisticsBoards.AsNoTracking().FirstOrDefaultAsync(b => b.Id == boardId) is not { } board)
        {
            return EventResult<LogisticsFileDto>.Fail(EventResultStatus.NotFound);
        }

        // The browser's name without any path: a file never lands outside the board's folder.
        var name = upload.FileName.Split('/', '\\')[^1].Trim();
        if (upload.Length <= 0 || upload.Length > MaxFileBytes)
        {
            return EventResult<LogisticsFileDto>.Invalid("file", "O ficheiro é demasiado grande. Tamanho máximo: 10MB");
        }

        if (name == "" || name.StartsWith('.') || !FileTypes.Contains(upload.ContentType))
        {
            return EventResult<LogisticsFileDto>.Invalid("file", "Tipo de ficheiro não suportado. Formatos permitidos: PDF, Word, Excel, TXT, PNG, JPG");
        }

        _audit.SetUser(user.Identity?.Name, LogisticsAuthorization.UserId(user));
        await _storage.UploadDocumentAsync(Folder(board.Name), name, upload.Content, upload.ContentType);
        return EventResult<LogisticsFileDto>.Ok(new LogisticsFileDto(name, Path.GetExtension(name), upload.Length));
    }

    public async Task<EventResult<DocumentLinkDto>> GetFileAsync(int boardId, string? name, ClaimsPrincipal user)
    {
        if (await RefusalAsync<DocumentLinkDto>(user, manage: false) is { } refused)
        {
            return refused;
        }

        if (await FilePathAsync(boardId, name) is not { } path)
        {
            return EventResult<DocumentLinkDto>.Fail(EventResultStatus.NotFound);
        }

        return await _storage.GetDocumentUrlAsync(path, forceDownload: true) is { } url
            ? EventResult<DocumentLinkDto>.Ok(new DocumentLinkDto(url))
            : EventResult<DocumentLinkDto>.Fail(EventResultStatus.NotFound);
    }

    public async Task<EventResult<bool>> DeleteFileAsync(int boardId, string? name, ClaimsPrincipal user)
    {
        if (await RefusalAsync<bool>(user, manage: true) is { } refused)
        {
            return refused;
        }

        if (await FilePathAsync(boardId, name) is not { } path)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        _audit.SetUser(user.Identity?.Name, LogisticsAuthorization.UserId(user));
        await _storage.DeleteDocumentAsync(path);
        return EventResult<bool>.Ok(true);
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>401 for visitors (or a deleted account), 403 for Leitões and, on management, for non-managers.</summary>
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

    private async Task<EventResult<LogisticsCardDetailDto>> ChangeCardAsync(int id, ClaimsPrincipal user,
        Func<ApplicationDbContext, LogisticsCard, Task<Dictionary<string, string[]>?>> change)
    {
        if (await RefusalAsync<LogisticsCardDetailDto>(user, manage: true) is { } refused)
        {
            return refused;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await db.LogisticsCards.FindAsync(id) is not { } card)
        {
            return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.NotFound);
        }

        if (await change(db, card) is { } errors)
        {
            return new EventResult<LogisticsCardDetailDto>(EventResultStatus.Invalid, Errors: errors);
        }

        await db.SaveChangesAsync();
        return EventResult<LogisticsCardDetailDto>.Ok(await DetailAsync(db, id));
    }

    private static async Task<EventResult<LogisticsBoardSummaryDto>?> BoardErrorsAsync(ApplicationDbContext db, LogisticsBoardInput input)
    {
        var errors = new Dictionary<string, string[]>();
        var name = input.Name?.Trim() ?? "";
        if (name == "" || name.Length > 200)
        {
            errors["name"] = new[] { "O nome do quadro é obrigatório (máx. 200 caracteres)." };
        }

        if ((input.Description?.Trim().Length ?? 0) > 2000)
        {
            errors["description"] = new[] { "A descrição não pode exceder 2000 caracteres" };
        }

        if (input.EventId is { } eventId && !await db.Events.AnyAsync(e => e.Id == eventId))
        {
            errors["eventId"] = new[] { "Evento não encontrado." };
        }

        return errors.Count > 0 ? new EventResult<LogisticsBoardSummaryDto>(EventResultStatus.Invalid, Errors: errors) : null;
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

    private static LogisticsBoardSummaryDto Summary(LogisticsBoard b) =>
        new(b.Id, b.Name, b.Description, Event(b.Event), b.IsCompleted, b.CompletedAt);

    private static async Task<LogisticsBoardSummaryDto> BoardSummaryAsync(ApplicationDbContext db, int id) =>
        Summary(await db.LogisticsBoards.AsNoTracking().Include(b => b.Event).SingleAsync(b => b.Id == id));

    private static async Task<Dictionary<int, int>> AssignmentCountsAsync(ApplicationDbContext db, List<int> cardIds) =>
        await db.LogisticsCardAssignments.AsNoTracking().Where(a => cardIds.Contains(a.CardId))
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

    private static LogisticsAssigneeDto Assignee(ApplicationUser u)
    {
        var who = GovernanceService.ToMember(u.Nickname, u.FirstName, u.LastName, u.ImageUrl);
        return new LogisticsAssigneeDto(u.Id, who.DisplayName, who.FullName, who.AvatarUrl);
    }

    private static LogisticsCardDto Face(LogisticsCard c, int assignments)
    {
        var checklist = Tasks(c.ChecklistJson);
        var description = c.Description ?? "";
        var marker = description.IndexOf(LegacyFilesMarker, StringComparison.Ordinal);
        return new LogisticsCardDto(c.Id, c.Title, marker < 0 ? description : description[..marker].TrimEnd(), c.Status.ToString(), Labels(c.Labels),
            c.StartDate, c.DueDate, c.Event?.Name, Person(c.AssignedToUser), assignments, checklist.Count(t => t.Done), checklist.Count,
            Links(c.AttachmentsJson).Count);
    }

    private static async Task<LogisticsCardDetailDto> DetailAsync(ApplicationDbContext db, int id)
    {
        var card = await db.LogisticsCards.AsNoTracking().Include(c => c.Event).Include(c => c.AssignedToUser).SingleAsync(c => c.Id == id);
        var assignees = await db.LogisticsCardAssignments.AsNoTracking().Include(a => a.User).Where(a => a.CardId == id).OrderBy(a => a.Id)
            .Select(a => a.User).ToListAsync();
        return new LogisticsCardDetailDto(
            Face(card, assignees.Count),
            card.ListId,
            card.AssignedToUser is null ? null : Assignee(card.AssignedToUser),
            Tasks(card.ChecklistJson).Select(t => new LogisticsChecklistItemDto(t.Task, t.Done)).ToList(),
            Links(card.AttachmentsJson).Select(l => new LogisticsLinkDto(l.CardId, l.Name)).ToList(),
            assignees.Select(Assignee).ToList());
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

    private string Folder(string boardName) =>
        $"docs/{_environment.EnvironmentName}/{FiscalYearHelper.GetCurrentFiscalYearString()}/Logistics/{LogisticsCardService.SanitizePathComponent(boardName)}/";

    private async Task<List<DocumentMetadata>> FilesAsync(string boardName)
    {
        try
        {
            return await _storage.ListDocumentsInFolderAsync(Folder(boardName));
        }
        catch (Exception ex)
        {
            // The old page treated the files as "not critical" too: the board still opens.
            _logger.LogWarning(ex, "Could not list the files of logistics board {Board}", boardName);
            return new();
        }
    }

    private async Task<string?> FilePathAsync(int boardId, string? name)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        if (await db.LogisticsBoards.AsNoTracking().FirstOrDefaultAsync(b => b.Id == boardId) is not { } board)
        {
            return null;
        }

        return (await FilesAsync(board.Name)).FirstOrDefault(d => d.FileName == name)?.FilePath;
    }
}
