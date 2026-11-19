using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// The React /logistics and /logistics/{id} pages (docs/react-logistics.md). Rules
/// enforced server-side (<see cref="LogisticsAuthorization"/>):
/// - boards: active and completed, newest first, search over name and description; name (≤ 200) required,
///   description ≤ 2000, optional event; finish / reopen; delete takes its lists and cards (cascade);
/// - lists by position (new ones last), name ≤ 100; delete takes its cards; cards by position;
/// - cards: title (≤ 200) required, description ≤ 2000, optional "Atribuir a" member, status TODO / WIP / DONE,
///   labels (text + colour), start and due dates, checklist, links to other cards of the board, assigned members;
///   the JSON columns keep their stored shape, so older rows (comma labels included) read;
/// - moving a card renumbers the positions of the lists it leaves and enters, so positions never collide;
/// - board files: one folder per board for the current fiscal year
///   (docs/{env}/{year}/Logistics/{board}/, shared by all its cards), ≤ 10 MB, the old file types;
/// - reminders: managers for board viewers, workers for themselves; sent by LogisticsReminderDelivery;
/// - every member who sees the board (Leitões included) reads the card details; the members a card is
///   assigned to ("Atribuir a" or "Membros atribuídos") may tick its checklist items and change its status, nothing
///   else, re-checked against the database on every write.
/// Assignment and mention pushes (<see cref="LogisticsNotifier"/>), sent after the commit.
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

    // The JSON columns, in their stored shape (Id is always 0).
    private sealed record StoredLabel(int Id, string Text, string Color);

    private sealed record StoredTask(int Id, string Task, bool Done);

    private sealed record StoredLink(int Id, string Name, int? CardId);

    // ---------------------------------------------------------------- boards
}
