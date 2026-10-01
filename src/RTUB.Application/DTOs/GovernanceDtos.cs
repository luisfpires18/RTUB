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
