namespace RTUB.Application.DTOs;

// Contracts of the public Órgãos Sociais page (React track 008, docs/react-portal-pilot.md). Built
// for anonymous readers: what the Blazor /roles already showed publicly (nickname, first and last
// name, profile photo, position) and nothing else. No user id, email, phone, birth date, notes,
// audit field or EF entity leaves the server.

/// <summary>One mandate: the years that have holders (newest first) and the bodies of the one shown.</summary>
public sealed record PublicGovernanceDto(
    IReadOnlyList<string> FiscalYears,
    string? FiscalYear,
    IReadOnlyList<GovernanceBodyDto> Bodies);

/// <summary>A body (Direção, Mesa da Assembleia, ...) with every position, held or not.</summary>
public sealed record GovernanceBodyDto(string Name, IReadOnlyList<GovernancePositionDto> Positions);

/// <summary>A position and its holders for the mandate; empty when nobody was recorded.</summary>
public sealed record GovernancePositionDto(string Title, IReadOnlyList<GovernanceMemberDto> Holders);

/// <summary>
/// A holder as the public sees them. <c>FullName</c> only when a nickname is the display name;
/// <c>AvatarUrl</c> null when there is no usable photo (the page shows the default avatar).
/// </summary>
public sealed record GovernanceMemberDto(string DisplayName, string? FullName, string? AvatarUrl);

// Management of the Órgãos Sociais (React track 016; was the Blazor /member/roles). Mod, Admin and Owner only
// (GovernanceAuthorization); still no email, phone, birth date, note or audit field.

/// <summary>
/// Every created fiscal year (newest first, empty ones too), the one shown, the start years that can still be
/// created (1991 up to the current one), and the bodies with their positions and assignment ids.
/// </summary>
public sealed record GovernanceManageDto(
    IReadOnlyList<string> FiscalYears,
    string? FiscalYear,
    IReadOnlyList<int> AvailableStartYears,
    IReadOnlyList<GovernanceManageBodyDto> Bodies);

public sealed record GovernanceManageBodyDto(string Name, IReadOnlyList<GovernanceManagePositionDto> Positions);

/// <summary><c>Position</c> is the enum name ("Magister"), what an assignment posts back.</summary>
public sealed record GovernanceManagePositionDto(string Position, string Title, IReadOnlyList<GovernanceHolderDto> Holders);

public sealed record GovernanceHolderDto(int AssignmentId, string DisplayName, string? FullName, string? AvatarUrl);

/// <summary>A member who can be given a position.</summary>
public sealed record GovernanceCandidateDto(string Id, string DisplayName, string? FullName, string? AvatarUrl);

public sealed record GovernanceFiscalYearInput(int StartYear);

public sealed record GovernanceAssignmentInput(string? FiscalYear, string? Position, string? UserId);

/// <summary>A short-lived pre-signed URL of the RGI PDF; null when the document is not available.</summary>
public sealed record GovernanceRgiDto(string? Url);
