using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for MemberInstrument operations
/// Handles CRUD operations and business logic for member instruments
/// </summary>
public interface IMemberInstrumentService
{
    /// <summary>
    /// Get all instruments for a specific member
    /// </summary>
    Task<IEnumerable<MemberInstrument>> GetMemberInstrumentsAsync(string memberId);

    /// <summary>
    /// Get the primary instrument for a member
    /// </summary>
    Task<MemberInstrument?> GetPrimaryInstrumentAsync(string memberId);

    /// <summary>
    /// Add an instrument to a member
    /// </summary>
    Task<MemberInstrument> AddInstrumentAsync(string memberId, InstrumentType instrumentType, bool isPrimary = false);

    /// <summary>
    /// Remove an instrument from a member
    /// </summary>
    Task RemoveInstrumentAsync(int instrumentId);

    /// <summary>
    /// Set an instrument as primary for a member
    /// This will unmark any other instrument as primary
    /// </summary>
    Task SetPrimaryInstrumentAsync(int instrumentId, string memberId);

    /// <summary>
    /// Check if a member already has a specific instrument type
    /// </summary>
    Task<bool> HasInstrumentAsync(string memberId, InstrumentType instrumentType);

    /// <summary>
    /// Get a specific member instrument by ID
    /// </summary>
    Task<MemberInstrument?> GetByIdAsync(int id);

    /// <summary>
    /// Get all instruments for multiple members
    /// Returns a dictionary mapping userId to their list of instruments
    /// </summary>
    Task<Dictionary<string, List<MemberInstrument>>> GetMemberInstrumentsByUserIdsAsync(IEnumerable<string> userIds);
}
