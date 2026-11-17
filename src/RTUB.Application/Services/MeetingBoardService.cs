using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// The React /meetings (task 034, docs/react-meetings.md). The old page only hid buttons and its
/// services checked nothing, so every gate it had is enforced here, through <see cref="MeetingAccess"/>:
///
/// - Visitors: <c>SignInRequired</c>. A Leitão: <c>Forbidden</c> on every call, whatever their role.
/// - Which meetings a member sees is <see cref="IMeetingService.GetAllMeetingsAsync"/>'s filter, unchanged (a Tuno
///   representative sees their CV meeting, decision A5). A meeting the member does not see is <c>NotFound</c>, for reads
///   and writes alike, so nothing tells them it exists.
/// - Draft atas are for whoever may write them; published ones follow the old reading rule (decision A1, the one
///   security change). Everything else is the old page's effective behaviour (decisions A2-A7).
///
/// Side effects are the old page's, through the same services: push on a new meeting or request, on a rejected request
/// and on a reminder; the meeting email and the cancellation email; the custom push (audited); the ata PDF in
/// Documentation on publish. Lengths are enforced here (the old forms declared them but never ran them).
/// </summary>
public sealed partial class MeetingBoardService : IMeetingBoardService
{
    private readonly TimeProvider _time;

    public const int MaxTitleLength = 200;
    public const int MaxLocationLength = 200;
    public const int MaxStatementLength = 5000;
    public const int MaxDescriptionLength = 2000;
    public const int MaxNotesLength = 500;
    public const int MaxReasonLength = 1000;
    public const int MaxPushLength = 500;
    public const int MaxSubjectLength = 300;
    public const int MaxEmailBodyLength = 10000;
    public const int MaxAtaNumberLength = 50;
    public const int MaxClosingTextLength = 5000;
    public const int MaxAgendaTitleLength = 500;
    public const int MaxAgendaTextLength = 5000;
    public const int MaxAgendaPoints = 100;
    public const int MaxVotes = 100000;
    public const int MaxCandidates = 50;

    private const string DateFormat = "yyyy-MM-dd'T'HH:mm";
    private static readonly string[] DateFormats = { "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss" };
    private static readonly CultureInfo Portuguese = new("pt-PT");
    private static readonly int[] RequestPageSizes = { 4, 8, 12, 16, 20 };
    private static readonly string[] QuorumBases = { "HoraAgendada", "HoraAgendadaMais30Minutos" };
    private static readonly string[] VoteResults = { "Aprovado", "Rejeitado" };
    private static readonly RequestStatus[] RequestFilters = { RequestStatus.Pending, RequestStatus.Confirmed, RequestStatus.Rejected };

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly IMeetingService _meetings;
    private readonly IMeetingRequestService _requests;
    private readonly IMeetingParticipationService _participations;
    private readonly IMeetingAtaService _atas;
    private readonly IMeetingAtaConfirmationService _confirmations;
    private readonly IAtaPdfService _pdf;
    private readonly IDocumentStorageService _documents;
    private readonly IEmailNotificationService _email;
    private readonly IEmailTemplateRenderer _templates;
    private readonly IPushNotificationService _push;
    private readonly IPushNotificationFactory _pushFactory;
    private readonly IAuditLogService _audit;
    private readonly IFiscalYearService _fiscalYears;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<MeetingBoardService> _logger;
    private readonly DocumentTreeCache _trees;

    public MeetingBoardService(
        IDbContextFactory<ApplicationDbContext> contexts,
        IMeetingService meetings,
        IMeetingRequestService requests,
        IMeetingParticipationService participations,
        IMeetingAtaService atas,
        IMeetingAtaConfirmationService confirmations,
        IAtaPdfService pdf,
        IDocumentStorageService documents,
        IEmailNotificationService email,
        IEmailTemplateRenderer templates,
        IPushNotificationService push,
        IPushNotificationFactory pushFactory,
        IAuditLogService audit,
        IFiscalYearService fiscalYears,
        IHostEnvironment environment,
        ILogger<MeetingBoardService> logger,
        DocumentTreeCache trees,
        TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
        _trees = trees;
        _contexts = contexts;
        _meetings = meetings;
        _requests = requests;
        _participations = participations;
        _atas = atas;
        _confirmations = confirmations;
        _pdf = pdf;
        _documents = documents;
        _email = email;
        _templates = templates;
        _push = push;
        _pushFactory = pushFactory;
        _audit = audit;
        _fiscalYears = fiscalYears;
        _environment = environment;
        _logger = logger;
    }
}
