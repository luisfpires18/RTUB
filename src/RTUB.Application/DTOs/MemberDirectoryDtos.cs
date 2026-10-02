namespace RTUB.Application.DTOs;

// Contracts of the React members area (React track 017; was the Blazor /members and /hierarchy). Signed-in members
// only, as before. What the old page showed every member and nothing more: the cards carry no contact field; the
// details carry email, phone, city, birth date and degree, which the old "Detalhes do Membro" modal showed to any
// signed-in member. No EF entity, password, role, address, note field of the user or audit field leaves the server.

/// <summary>Query of the directory, as the old filters: category (Leitao, Caloiro, Tuno, TunoHonorario), Tuno
/// sub-category (Tuno, Veterano, Tunossauro, Fundador), instrument (enum name) and "Mostrar só ativos".</summary>
public sealed record MemberDirectoryQuery(string? Search, string? Category, string? SubCategory, string? Instrument, bool ActiveOnly);

/// <summary>Regular members (by nickname) and Leitões (by activity), plus the instrument options.</summary>
public sealed record MemberDirectoryDto(
    IReadOnlyList<MemberCardDto> Members,
    IReadOnlyList<MemberCardDto> Leitoes,
    IReadOnlyList<MemberOptionDto> Instruments,
    bool CanManage);

/// <summary>
/// One card. <c>Badges</c> are display labels ("TUNO", "LEITÃO"); <c>Position</c> the current fiscal year's;
/// <c>Online</c> = signed in within the last hour (the old "Online / Offline"); <c>Inactive</c> = a Leitão with no
/// activity this month ("Não tem participado"); <c>Expelled</c> greys a Leitão's card, as the old grid did (regular
/// cards never carried it).
/// </summary>
public sealed record MemberCardDto(
    string Id,
    string DisplayName,
    string? FullName,
    string? AvatarUrl,
    IReadOnlyList<MemberBadgeDto> Badges,
    string? Position,
    bool NoRoles,
    string? Instrument,
    bool Online,
    bool Inactive,
    bool Expelled);

/// <summary><c>Kind</c> is the enum name in lower case ("tuno", "veterano"...), for the badge colour.</summary>
public sealed record MemberBadgeDto(string Label, string Kind);

public sealed record MemberOptionDto(string Value, string Label);

/// <summary>The old "Detalhes do Membro" modal.</summary>
public sealed record MemberDetailDto(
    string Id,
    string DisplayName,
    string? FullName,
    string? AvatarUrl,
    IReadOnlyList<MemberBadgeDto> Badges,
    IReadOnlyList<string> Positions,
    string? Email,
    string? Phone,
    string? City,
    string? BirthDate,
    int? Age,
    string? Degree,
    bool ShowInstruments,
    IReadOnlyList<string> Instruments,
    bool ShowMentor,
    string? Mentor,
    IReadOnlyList<MemberTimelineItemDto> Timeline,
    MemberStateDto? State);

/// <summary>A step of "Percurso na Tuna": <c>Kind</c> membership / subcategory / role; <c>State</c> active / completed / "";
/// <c>Accent</c> leitao, caloiro, tuno, veterano, tunossauro, fundador, honorario, magister or role.</summary>
public sealed record MemberTimelineItemDto(string Label, string Years, string Kind, string State, string Accent, string? Notes);

/// <summary>"Estado na Tuna", only for active categories with some activity. <c>Retired</c> null hides "Estado atual"
/// (Leitões).</summary>
public sealed record MemberStateDto(
    bool? Retired,
    string? Progress,
    int? ProgressMonths,
    int? ProgressTotalMonths,
    bool Encourage,
    DateTime? LastRehearsal,
    DateTime? LastEvent,
    IReadOnlyList<MemberActivityDto> Activities);

public sealed record MemberActivityDto(DateTime Date, string Name, string Type, bool IsRehearsal);

/// <summary>The old "Gestão de Membros Ativos" list (read-only here; making active and push reminders stay on
/// /members/manage).</summary>
public sealed record ActiveMemberDto(
    string Id,
    string DisplayName,
    string? FullName,
    string? AvatarUrl,
    string? Instrument,
    bool Retired,
    DateTime? LastRehearsal,
    DateTime? LastEvent,
    string? Progress,
    bool Encourage);

/// <summary>The old "Aniversários": the birthdays still to come this year. <c>Birthday</c> is "dd/MM", <c>Date</c> this
/// year's date (yyyy-MM-dd); no birth year is sent, only the age it turns.</summary>
public sealed record MemberBirthdayDto(
    string Id,
    string DisplayName,
    string? FullName,
    string? AvatarUrl,
    int Age,
    string? City,
    IReadOnlyList<MemberBadgeDto> Badges,
    string Birthday,
    string Date);

/// <summary>A node of the Padrinho → Afilhado tree.</summary>
public sealed record MemberTreeNodeDto(string Id, string DisplayName, string? FullName, string? AvatarUrl, IReadOnlyList<MemberTreeNodeDto> Children);
