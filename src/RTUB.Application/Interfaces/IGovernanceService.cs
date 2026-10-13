using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>Read side of the public Órgãos Sociais page (React track 008).</summary>
public interface IGovernanceService
{
    /// <summary>
    /// The mandate <paramref name="fiscalYear"/> ("2024-2025") when it has holders; otherwise the
    /// current fiscal year when it has holders, else the most recent one that does.
    /// </summary>
    Task<PublicGovernanceDto> GetPublicGovernanceAsync(string? fiscalYear);
}
