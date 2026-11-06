namespace RTUB.Application.DTOs;

// Contracts of the React member admin (React track 018; was the Blazor /members/manage). Admin and Owner only. The
// edit form carries the fields the old "Editar Membro" modal edited, nothing else: no password, role, audit or
// Identity field leaves the server.

/// <summary>
/// Create or edit a member, as the old form. <c>Category</c>: Leitao, Caloiro or Tuno; <c>Fundador</c> and
/// <c>Honorario</c> only count for a Tuno; <c>NoNickname</c> ("Sem alcunha") only when creating a Leitão: the email's
/// local part becomes the nickname and the username. <c>Instruments</c> only when creating (an existing member's
/// instruments change one by one, as before).
/// </summary>
public sealed record MemberInput(
    string? FirstName,
    string? LastName,
    string? Nickname,
    string? PhoneNumber,
    string? Email,
    string? City,
    string? Degree,
    DateOnly? DateOfBirth,
    string? Category,
    bool Fundador,
    bool Honorario,
    bool NoNickname,
    int? YearLeitao,
    int? MonthLeitao,
    int? YearCaloiro,
    int? MonthCaloiro,
    int? YearTuno,
    int? MonthTuno,
    string? MentorId,
    IReadOnlyList<MemberInstrumentInput>? Instruments);

public sealed record MemberInstrumentInput(string Instrument, bool Primary);

public sealed record MemberNicknameInput(string? Nickname);

/// <summary>The old edit modal's values. <c>LockedDates</c>: the complete date pairs only Owner or the Magister may change.</summary>
public sealed record MemberEditDto(
    string Id,
    string? FirstName,
    string? LastName,
    string? Nickname,
    string? PhoneNumber,
    string? Email,
    string? City,
    string? Degree,
    string? DateOfBirth,
    string Category,
    bool Fundador,
    bool Honorario,
    int? YearLeitao,
    int? MonthLeitao,
    int? YearCaloiro,
    int? MonthCaloiro,
    int? YearTuno,
    int? MonthTuno,
    string? MentorId,
    string? MentorName,
    IReadOnlyList<MemberInstrumentDto> Instruments,
    MemberLockedDatesDto LockedDates);

public sealed record MemberLockedDatesDto(bool Leitao, bool Caloiro, bool Tuno);

/// <summary><c>Instrument</c> is the enum name, <c>Label</c> its display.</summary>
public sealed record MemberInstrumentDto(int Id, string Instrument, string Label, bool Primary);

/// <summary>A padrinho candidate: Tuno or above.</summary>
public sealed record MentorCandidateDto(string Id, string DisplayName, string? FullName, string? AvatarUrl);

public sealed record MemberCreatedDto(string Id);

/// <summary>
/// Task 076: the Mod's limited edit of one member (also Admin / Owner). <c>Fields</c>: the fields that apply to this member
/// (<c>MemberInfoRules</c>); <c>Missing</c>: those still empty; <c>LockedDates</c>: complete date pairs only Owner or the
/// Magister may change; <c>Stamp</c>: send it back unchanged - a save over a newer version is refused (409).
/// </summary>
public sealed record MemberInfoDto(
    string Id,
    string DisplayName,
    string? City,
    string? PhoneNumber,
    int? YearLeitao,
    int? MonthLeitao,
    int? YearCaloiro,
    int? MonthCaloiro,
    int? YearTuno,
    int? MonthTuno,
    string? MentorId,
    string? MentorName,
    IReadOnlyList<MemberInstrumentDto> Instruments,
    IReadOnlyList<string> Fields,
    IReadOnlyList<string> Missing,
    MemberLockedDatesDto LockedDates,
    string Stamp);

/// <summary>
/// Task 076: what the limited edit may change - and nothing else (no name, email, category, role, position, status).
/// A field that does not apply to the member must be empty.
/// </summary>
public sealed record MemberInfoInput(
    string? City,
    string? PhoneNumber,
    int? YearLeitao,
    int? MonthLeitao,
    int? YearCaloiro,
    int? MonthCaloiro,
    int? YearTuno,
    int? MonthTuno,
    string? MentorId,
    string? Stamp);
