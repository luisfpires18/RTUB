using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for MemberInstrument entity
/// Provides data access operations for member instrument assignments
/// </summary>
public interface IMemberInstrumentRepository : IRepository<MemberInstrument>
{
    /// <summary>
    /// Gets member instruments by member ID
    /// </summary>
    Task<IEnumerable<MemberInstrument>> GetByMemberIdAsync(string memberId);
    
    /// <summary>
    /// Gets primary instrument for a member
    /// </summary>
    Task<MemberInstrument?> GetPrimaryInstrumentAsync(string memberId);
}
