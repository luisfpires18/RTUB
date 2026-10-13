namespace RTUB.Application.DTOs;

// The React /meetings (task 034, docs/react-meetings.md; was the Blazor page): meetings, participations, atas and meeting
// requests. Built per caller by MeetingBoardService: a member only receives meetings they may see and fields the old page
// showed them. No email address, EF entity or audit field leaves the server. Dates are the stored local (Portugal)
// values as plain "yyyy-MM-ddTHH:mm" text, so no time zone can shift them. Meeting types travel as the MeetingType name.

/// <summary>
/// The page. <c>Upcoming</c>: today and later, soonest first. <c>Past</c>: before today, newest first. <c>FiscalYear</c>
/// is the year filter applied (null = every year); <c>FiscalYears</c> the choices, newest first.
/// </summary>
public sealed record MeetingBoardDto(
    IReadOnlyList<MeetingCardDto> Upcoming,
    IReadOnlyList<MeetingCardDto> Past,
    IReadOnlyList<string> FiscalYears,
    string CurrentFiscalYear,
    string? FiscalYear,
    MeetingBoardAccessDto Access);

/// <summary>
/// What the page offers this member. <c>MeetingTypes</c>: the types the create / edit form offers (empty unless
/// <c>CanCreate</c>). <c>CanAddParticipants</c>: "Adicionar Membro" in a participants list.
/// </summary>
public sealed record MeetingBoardAccessDto(
    bool CanCreate,
    IReadOnlyList<MeetingTypeOptionDto> MeetingTypes,
    bool CanProposeCv,
    bool CanProposeDirecao,
    bool CanProposeAg,
    bool CanSeeRequests,
    bool CanAddParticipants);

public sealed record MeetingTypeOptionDto(string Value, string Label);

/// <summary>
/// One meeting card. <c>Completed</c>: not cancelled and on or before today (no more "Vou / Não vou"); <c>Past</c>: before
/// today ("Realizada"). <c>AtaStatus</c> ("draft" | "published") only when this member may open that ata (a draft only for
/// whoever may write it). <c>TunoRepresentativeId</c> only for those who may edit the meeting.
/// </summary>
public sealed record MeetingCardDto(
    int Id,
    string Type,
    string TypeLabel,
    string Title,
    string Date,
    string? Location,
    string Statement,
    string? OrganizerName,
    string? OrganizerPosition,
    string? TunoRepresentative,
    string? TunoRepresentativeId,
    bool Cancelled,
    bool Completed,
    bool Past,
    int GoingCount,
    MeetingMyParticipationDto? MyParticipation,
    string? AtaStatus,
    MeetingCardAccessDto Can);

/// <summary>The member's own answer: "Vou" (true) or "Não vou" (false), with their note.</summary>
public sealed record MeetingMyParticipationDto(int Id, bool WillAttend, string? Notes);

/// <summary>
/// The card's buttons, decided by the server: <c>Manage</c> edit / delete; <c>Notify</c> email, push and cancel (upcoming,
/// not cancelled); <c>Uncancel</c>; <c>Respond</c> "Vou / Não vou"; <c>RemoveOwn</c> remove your answer after the day;
/// <c>Participants</c> the participants list; <c>WriteAta</c> create / edit the ata; <c>ViewAta</c> read it.
/// </summary>
public sealed record MeetingCardAccessDto(
    bool Manage,
    bool Notify,
    bool Uncancel,
    bool Respond,
    bool RemoveOwn,
    bool Participants,
    bool WriteAta,
    bool ViewAta);

/// <summary>Create / edit form choices: the types this member may give a meeting, and the Tunos who may represent the Tunos at a CV.</summary>
public sealed record MeetingFormDto(IReadOnlyList<MeetingTypeOptionDto> Types, IReadOnlyList<MeetingPersonOptionDto> TunoRepresentatives);

/// <summary>A person to pick (id + what the old select showed).</summary>
public sealed record MeetingPersonOptionDto(string Id, string Label);

/// <summary>Create / edit. <c>Date</c> "yyyy-MM-ddTHH:mm"; <c>TunoRepresentativeId</c> only for a CV meeting.</summary>
public sealed record MeetingInput(string? Type, string? Title, string? Date, string? Location, string? Statement, string? TunoRepresentativeId);

/// <summary>A meeting was saved. <c>Card</c> is null when the member who saved it cannot see it (a type they do not see).</summary>
public sealed record MeetingSavedDto(int Id, MeetingCardDto? Card);

// ---------- notices (managers) ----------

/// <summary>A person in a "who receives" list, as the old preview showed them: name, picture and, for a CV, years as Tuno.</summary>
public sealed record MeetingPersonDto(string Name, string AvatarUrl, string? Detail);

/// <summary>The email form: the old prefill (subject and the statement), who receives it and who does not, and for a CV the Tunos who may be added.</summary>
public sealed record MeetingEmailDraftDto(
    string Subject,
    string Body,
    IReadOnlyList<MeetingPersonDto> Recipients,
    IReadOnlyList<MeetingPersonDto> NotReceiving,
    IReadOnlyList<MeetingPersonOptionDto> TunoOptions);

public sealed record MeetingEmailInput(string? Subject, string? Body, string? TunoId);

public sealed record MeetingEmailPreviewInput(string? Body);

/// <summary>The rendered email, for a sandboxed preview frame.</summary>
public sealed record MeetingEmailPreviewDto(string Html);

/// <summary>How many were sent; <c>Warning</c> when the meeting changed but the emails did not all go.</summary>
public sealed record MeetingNoticeResultDto(int Sent, string? Warning);

/// <summary>The cancel form: who would get the email if "Notificar todos os membros por email" is ticked.</summary>
public sealed record MeetingCancelDraftDto(IReadOnlyList<MeetingPersonDto> Recipients, IReadOnlyList<MeetingPersonDto> NotReceiving, int Total);

public sealed record MeetingCancelInput(string? Reason, bool NotifyByEmail);

/// <summary>The push form: who has push on, who does not, and for a CV the Tunos who may be added.</summary>
public sealed record MeetingPushDraftDto(
    IReadOnlyList<MeetingPersonDto> Recipients,
    IReadOnlyList<MeetingPersonDto> NotSubscribed,
    IReadOnlyList<MeetingPersonOptionDto> TunoOptions);

public sealed record MeetingPushInput(string? Message, string? TunoId);

// ---------- participations ----------

/// <summary>The participants list ("Vão participar" / "Não vão participar"), newest answer first.</summary>
public sealed record MeetingParticipantsDto(
    int MeetingId,
    string Title,
    IReadOnlyList<MeetingParticipantDto> Going,
    IReadOnlyList<MeetingParticipantDto> NotGoing,
    bool CanAdd);

/// <summary>One answer. <c>Badge</c>: "Magister", "Tuno", "Caloiro" or null, as the old card showed.</summary>
public sealed record MeetingParticipantDto(int Id, string Nickname, string FullName, string AvatarUrl, string? Notes, string? Badge, bool CanRemove);

public sealed record MeetingParticipationInput(bool WillAttend, string? Notes);

/// <summary>A member "Adicionar Membro" may add.</summary>
public sealed record MeetingCandidateDto(string Id, string Name, string AvatarUrl);

public sealed record MeetingAddParticipantInput(string? UserId);

// ---------- atas ----------

/// <summary>An agenda point (Ordem de Trabalhos), numbered from 1.</summary>
public sealed record MeetingAgendaPointDto(
    int Number,
    string Title,
    string? Discussion,
    string? Decision,
    int? VotesFor,
    int? VotesAgainst,
    int? VotesAbstain,
    string? Result);

/// <summary>
/// The ata editor. <c>Id</c> null = not saved yet (the old defaults). <c>Kind</c>: "cv", "ag" or "direcao" (which
/// secretariat block the form shows). <c>PresidentName</c>: who will be saved as president.
/// </summary>
public sealed record MeetingAtaEditorDto(
    int? Id,
    string Status,
    string Kind,
    string MeetingTypeLabel,
    string MeetingTitle,
    string MeetingDate,
    string? AtaNumber,
    string ActualStartTime,
    string? ActualEndTime,
    string Location,
    string QuorumBasis,
    string? PresidentName,
    string? FirstSecretaryId,
    string? SecondSecretaryId,
    IReadOnlyList<MeetingPersonOptionDto> FirstSecretaryOptions,
    IReadOnlyList<MeetingPersonOptionDto> SecondSecretaryOptions,
    IReadOnlyList<string> Present,
    IReadOnlyList<MeetingAgendaPointDto> AgendaPoints,
    string? ClosingText,
    string? GeneratedAt);

public sealed record MeetingAgendaPointInput(
    string? Title,
    string? Discussion,
    string? Decision,
    int? VotesFor,
    int? VotesAgainst,
    int? VotesAbstain,
    string? Result);

/// <summary>Saving the ata. Times "yyyy-MM-ddTHH:mm"; <c>QuorumBasis</c> only for AG ("HoraAgendada" | "HoraAgendadaMais30Minutos").</summary>
public sealed record MeetingAtaInput(
    string? AtaNumber,
    string? ActualStartTime,
    string? ActualEndTime,
    string? Location,
    string? QuorumBasis,
    string? FirstSecretaryId,
    string? SecondSecretaryId,
    IReadOnlyList<MeetingAgendaPointInput>? AgendaPoints,
    string? ClosingText);

/// <summary>
/// Reading an ata ("Ver Ata"). <c>Confirmed</c>: who confirmed a published ata. <c>Mine</c>: only for someone who attended a
/// published ata; <c>Mine.Confirmed</c> null = not answered yet, true = confirmed, false = refused.
/// </summary>
public sealed record MeetingAtaViewDto(
    int MeetingId,
    string Status,
    string Kind,
    string MeetingTypeLabel,
    string? AtaNumber,
    string MeetingDate,
    string Location,
    string? PresidentName,
    string? FirstSecretaryName,
    string? SecondSecretaryName,
    IReadOnlyList<string> Present,
    IReadOnlyList<string> Confirmed,
    MeetingAtaMyConfirmationDto? Mine,
    IReadOnlyList<MeetingAgendaPointDto> AgendaPoints,
    string? ClosingText);

public sealed record MeetingAtaMyConfirmationDto(bool? Confirmed, string? Notes);

public sealed record MeetingAtaConfirmationInput(bool Confirm);

/// <summary>A generated ata PDF.</summary>
public sealed record MeetingAtaPdf(byte[] Content, string FileName);

// ---------- meeting requests ----------

/// <summary>One page of the "Pedidos de Reuniões" section, newest proposed date first.</summary>
public sealed record MeetingRequestPageDto(IReadOnlyList<MeetingRequestDto> Items, int Total, int Page, int PageSize);

/// <summary>
/// A request. <c>Status</c>: "Pending", "Confirmed" or "Rejected". <c>CanDelete</c> / <c>CanRemind</c>: its author or Owner
/// (remind only while pending); <c>CanDecide</c>: accept / reject while pending.
/// </summary>
public sealed record MeetingRequestDto(
    int Id,
    string Type,
    string TypeLabel,
    string Title,
    string AuthorName,
    string ProposedDate,
    string? Location,
    string Description,
    string Status,
    bool CanDelete,
    bool CanRemind,
    bool CanDecide);

/// <summary>A proposal. <c>Type</c>: ConselhoVeteranos, ReuniaoDirecao or AssembleiaGeralExtraordinaria.</summary>
public sealed record MeetingRequestInput(string? Type, string? Title, string? ProposedDate, string? Location, string? Description);

/// <summary>An accepted request, as the create form opens prefilled with it.</summary>
public sealed record MeetingDraftDto(string Type, string Title, string Date, string? Location, string Statement);
