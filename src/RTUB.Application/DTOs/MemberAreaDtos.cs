namespace RTUB.Application.DTOs;

// The member-area pages React took over in task 032 (docs/react-member-area.md): the Hall of Fame (/hall-of-fame), the
// members map (/members/map, was /member/map) and the profile editor (/profile, was the Blazor /member/profile).

// ---------- Hall of Fame (GET /api/hall-of-fame) ----------

/// <summary>The twelve records, in the old page's order. Every winner of a tie is listed; no winner is "Sem dados".</summary>
public sealed record HallOfFameDto(IReadOnlyList<HallOfFameCategoryDto> Categories);

/// <summary>
/// One record: a stable key, the old title, the winners and the value they share ("3 afilhados", "1 ano 2 meses",
/// "outubro 2024"); <c>Value</c> is null when nobody qualifies.
/// </summary>
public sealed record HallOfFameCategoryDto(string Key, string Title, IReadOnlyList<GovernanceMemberDto> Winners, string? Value);

// ---------- members map (GET /api/members/map) ----------

/// <summary>
/// City-level only: a city's coordinates come from the geocoding cache, never from a member. No email, phone or
/// address. <c>Total</c> counts every account the old page counted.
/// </summary>
public sealed record MemberMapDto(
    int Total,
    IReadOnlyList<MemberMapCityDto> Cities,
    IReadOnlyList<GovernanceMemberDto> WithoutCity,
    IReadOnlyList<string> Pending);

/// <summary>A geocoded city and who lives there, most members first.</summary>
public sealed record MemberMapCityDto(string Name, double Latitude, double Longitude, IReadOnlyList<GovernanceMemberDto> Members);

// ---------- my profile (/api/me/...) ----------

/// <summary>
/// Everything /profile shows and edits for the signed-in member: the same read-only view the members directory shows
/// (<see cref="MemberDetailDto"/>), plus the editable values and the rules that lock them.
/// </summary>
public sealed record MyProfileDto(
    MemberDetailDto Member,
    MyPersonalDto Personal,
    MyTunaDto Tuna,
    IReadOnlyList<MemberInstrumentDto> Instruments,
    IReadOnlyList<MemberOptionDto> InstrumentOptions,
    bool Subscribed,
    bool RequirePasswordChange,
    MyRankDto Rank);

/// <summary>The "Pessoal" values. <c>NicknameLocked</c>: a member who is only a Leitão gets a nickname at Caloiro.</summary>
public sealed record MyPersonalDto(
    string? FirstName,
    string? LastName,
    string? Nickname,
    bool NicknameLocked,
    string? Email,
    string? PhoneNumber,
    string? DateOfBirth,
    string? City,
    string? Degree);

/// <summary>
/// The "Tuna" values: the padrinho (shown when the member may have one) and the three joining dates (none for a
/// Fundador; Caloiro once past Leitão; Tuno once Tuno). A complete date pair is locked for the member.
/// </summary>
public sealed record MyTunaDto(
    bool ShowMentor,
    string? MentorId,
    string? MentorName,
    bool ShowLeitao,
    bool ShowCaloiro,
    bool ShowTuno,
    int? YearLeitao,
    int? MonthLeitao,
    int? YearCaloiro,
    int? MonthCaloiro,
    int? YearTuno,
    int? MonthTuno,
    MemberLockedDatesDto LockedDates);

/// <summary>The old rank card: level, its name, XP and the way to the next level (none at the top).</summary>
public sealed record MyRankDto(
    int Level,
    string Name,
    int Xp,
    bool MaxLevel,
    int XpToNext,
    int XpInLevel,
    int XpForLevel,
    double Percent,
    string? NextName);

public sealed record MyPersonalInput(
    string? FirstName,
    string? LastName,
    string? Nickname,
    string? Email,
    string? PhoneNumber,
    DateOnly? DateOfBirth,
    string? City,
    string? Degree);

public sealed record MyTunaInput(
    string? MentorId,
    int? YearLeitao,
    int? MonthLeitao,
    int? YearCaloiro,
    int? MonthCaloiro,
    int? YearTuno,
    int? MonthTuno);

public sealed record MySubscriptionInput(bool Subscribed);

public sealed record MyPasswordInput(string? CurrentPassword, string? NewPassword, string? ConfirmPassword);

/// <summary>A profile photo as uploaded (already cropped square in the browser).</summary>
public sealed record MyPhotoUpload(Stream Content, string FileName, string ContentType, long Length);

/// <summary>The member's new avatar URL after an upload.</summary>
public sealed record MyPhotoDto(string AvatarUrl);
