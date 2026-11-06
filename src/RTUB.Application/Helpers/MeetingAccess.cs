using RTUB.Application.Extensions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Helpers;

/// <summary>
/// Who may do what on /meetings (task 034, docs/react-meetings.md). A line-by-line port of the gates of the retired
/// Blazor page (<c>Meetings.razor</c>, <c>MeetingCard</c>, <c>MeetingRequestCard</c>), which only hid buttons; the API now
/// enforces them through <c>MeetingBoardService</c>. Which meetings a member sees at all stays
/// <c>MeetingService.GetAllMeetingsAsync</c>'s filter, unchanged.
///
/// The old page's words, kept on purpose:
/// - "Veterano" / "Tunossauro" is <see cref="ApplicationUser.CurrentRole"/> (computed from YearTuno / MonthTuno), not the
///   category; Leitão is the category. Tuno Honorário and Fundador have no rule of their own.
/// - A Leitão is refused the whole page, whatever their role (Owner and Admin included).
/// - Owner is not exempt from what a meeting type shows (CV / Direção) nor from the requests gate (decision A3 / A4).
/// - Admin alone never writes an ata: the page passed only "Owner" to <c>CanCreateOrEditAta</c> (decision A2).
/// - Task 075 (#256, owner's matrix): each body's CURRENT president creates and manages that body's meetings - Presidente
///   da Mesa: AG / AGE; Magister (the Direção's president: there is no separate "Presidente da Direção" position):
///   Direção; Presidente do CV: CV - and Owner every type. Someone with several presidencies gets the union (no
///   first-match). Admin alone gives none of this. (Before 075 only Owner, the Mesa and the CV president, Mesa first.)
/// </summary>
public sealed class MeetingAccess
{
    public const string Veterano = "VETERANO";
    public const string Tunossauro = "TUNOSSAURO";
    public const string Tuno = "TUNO";

    private static readonly Position[] Direcao =
    {
        Position.Magister, Position.ViceMagister, Position.Secretario, Position.PrimeiroTesoureiro, Position.SegundoTesoureiro,
    };

    /// <summary>The form's order of meeting types.</summary>
    private static readonly MeetingType[] TypeOrder =
    {
        MeetingType.AssembleiaGeralOrdinaria, MeetingType.AssembleiaGeralExtraordinaria, MeetingType.ConselhoVeteranos, MeetingType.ReuniaoDirecao,
    };

    public MeetingAccess(ApplicationUser user, bool isOwner, bool isAdmin)
    {
        User = user;
        IsOwner = isOwner;
        IsAdmin = isAdmin;
    }

    public ApplicationUser User { get; }

    public string UserId => User.Id;

    /// <summary>The Owner role (the old page's cached <c>isOwner</c>).</summary>
    public bool IsOwner { get; }

    /// <summary>The Admin role (the old page's cached <c>isAdmin</c>).</summary>
    public bool IsAdmin { get; }

    /// <summary>"Acesso Restrito": a Leitão gets nothing on /meetings, whatever their role.</summary>
    public bool IsBlocked => User.IsLeitao();

    private bool Has(Position position) => User.Positions?.Contains(position) == true;

    private bool IsVeteranoByTime => User.CurrentRole is Veterano or Tunossauro;

    public static bool IsAssembly(MeetingType type) =>
        type is MeetingType.AssembleiaGeralOrdinaria or MeetingType.AssembleiaGeralExtraordinaria;

    public static bool HoldsDirecaoPosition(ApplicationUser user) => user.Positions?.Any(p => Direcao.Contains(p)) == true;

    // ---------- meetings ----------

    /// <summary>The position presiding over a meeting type's body (task 075): Mesa for AG / AGE, Magister for Direção, CV president for CV.</summary>
    public static Position PresidentOf(MeetingType type) => type switch
    {
        MeetingType.ConselhoVeteranos => Position.PresidenteConselhoVeteranos,
        MeetingType.ReuniaoDirecao => Position.Magister,
        _ => Position.PresidenteMesaAssembleia,
    };

    /// <summary>"Criar Reunião": Owner, or the current president of at least one body.</summary>
    public bool CanCreateMeetings => IsOwner || TypeOrder.Any(t => Has(PresidentOf(t)));

    /// <summary>
    /// Edit, delete, cancel, reactivate, email and push one meeting: Owner any; otherwise the current president of the
    /// meeting's body (the union for someone presiding over several).
    /// </summary>
    public bool CanManage(Meeting meeting) => CanCreateType(meeting.Type);

    /// <summary>Create a meeting of <paramref name="type"/>: Owner, or that body's current president.</summary>
    public bool CanCreateType(MeetingType type) => IsOwner || Has(PresidentOf(type));

    /// <summary>The types the create / edit form offers: exactly <see cref="CanCreateType"/>.</summary>
    public IReadOnlyList<MeetingType> FormTypes => TypeOrder.Where(CanCreateType).ToList();

    // ---------- proposals (meeting requests) ----------

    /// <summary>"Propor Reunião de CV": Veterano / Tunossauro by time or Magister; never the Presidente do CV.</summary>
    public bool CanProposeCv => !Has(Position.PresidenteConselhoVeteranos) && (IsVeteranoByTime || Has(Position.Magister));

    /// <summary>"Propor Reunião Direção" (old <c>IsDirecaoMember</c>): Owner, Admin with the Tuno category, Direção positions.</summary>
    public bool CanProposeDirecao => IsOwner || (IsAdmin && User.IsTuno()) || HoldsDirecaoPosition(User);

    /// <summary>"Propor Assembleia Geral": Owner, Magister, Presidente do CV, Presidente do Conselho Fiscal.</summary>
    public bool CanProposeAg =>
        IsOwner || Has(Position.Magister) || Has(Position.PresidenteConselhoVeteranos) || Has(Position.PresidenteConselhoFiscal);

    /// <summary>The "Pedidos de Reuniões" section (old <c>CanViewCVRequests</c>): Veterano / Tunossauro by time or Magister.</summary>
    public bool CanSeeRequests => IsVeteranoByTime || Has(Position.Magister);

    /// <summary>Accept / reject (old <c>CanApproveRejectRequests</c>): Owner; CV the Presidente do CV; AG the Presidente da Mesa.</summary>
    public bool CanDecide(MeetingRequest request)
    {
        if (IsOwner)
        {
            return true;
        }

        if (request.RequestedMeetingType == MeetingType.ConselhoVeteranos)
        {
            return Has(Position.PresidenteConselhoVeteranos);
        }

        return IsAssembly(request.RequestedMeetingType) && Has(Position.PresidenteMesaAssembleia);
    }

    /// <summary>Delete a request, and remind about a pending one: its author, or Owner (any request).</summary>
    public bool OwnsRequest(MeetingRequest request) => IsOwner || request.AuthorUserId == UserId;

    // ---------- participations ----------

    /// <summary>"Adicionar Membro" in the participants list: the Admin or Owner role.</summary>
    public bool CanAddParticipants => IsAdmin || IsOwner;

    /// <summary>Remove a participation: your own, or anyone's for Owner (Admin adds but does not remove).</summary>
    public bool CanRemoveParticipation(MeetingParticipation participation) => IsOwner || participation.UserId == UserId;

    /// <summary>Who "Adicionar Membro" may add: anyone with categories who is neither Leitão nor Caloiro.</summary>
    public static bool CanBeAddedToMeeting(ApplicationUser user) =>
        user.Categories != null && !user.Categories.Contains(MemberCategory.Leitao) && !user.Categories.Contains(MemberCategory.Caloiro);

    // ---------- atas ----------

    /// <summary>
    /// The roles the old page passed to <c>MeetingAtaService.CanCreateOrEditAta</c>: "Owner" only, never "Admin"
    /// (decision A2: Admin alone does not write atas).
    /// </summary>
    public IReadOnlyList<string> AtaRoles => IsOwner ? new[] { "Owner" } : Array.Empty<string>();

    /// <summary>
    /// Old <c>CanViewMeetingAta</c>, on top of seeing the meeting: Owner; CV Veterano / Tunossauro by time, Magister or the
    /// meeting's Tuno representative; AG anyone but a Leitão; Direção anyone who sees it. Published atas only: a draft is
    /// for the people who may write it (decision A1).
    /// </summary>
    public bool CanReadPublishedAta(Meeting meeting)
    {
        if (IsOwner)
        {
            return true;
        }

        if (meeting.Type == MeetingType.ConselhoVeteranos)
        {
            return IsVeteranoByTime || Has(Position.Magister) || meeting.TunoRepresentativeUserId == UserId;
        }

        if (IsAssembly(meeting.Type))
        {
            return !User.IsLeitao();
        }

        return true;
    }
}
