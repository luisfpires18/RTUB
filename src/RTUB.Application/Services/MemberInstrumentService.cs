using Microsoft.EntityFrameworkCore;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing member instruments using Repository pattern
/// Implements business logic and data access for MemberInstrument entities
/// Now depends on IMemberInstrumentRepository abstraction instead of concrete DbContext
/// </summary>
public class MemberInstrumentService : IMemberInstrumentService
{
    private readonly IMemberInstrumentRepository _memberInstrumentRepository;

    public MemberInstrumentService(IMemberInstrumentRepository memberInstrumentRepository)
    {
        _memberInstrumentRepository = memberInstrumentRepository;
    }

    public async Task<IEnumerable<MemberInstrument>> GetMemberInstrumentsAsync(string memberId)
    {
        return await _memberInstrumentRepository.GetByMemberIdAsync(memberId);
    }

    public async Task<MemberInstrument?> GetPrimaryInstrumentAsync(string memberId)
    {
        return await _memberInstrumentRepository.GetPrimaryInstrumentAsync(memberId);
    }

    public async Task<MemberInstrument> AddInstrumentAsync(string memberId, InstrumentType instrumentType, bool isPrimary = false)
    {
        // Check if instrument already exists for this member
        var exists = await HasInstrumentAsync(memberId, instrumentType);
        if (exists)
        {
            throw new InvalidOperationException("O membro já possui este instrumento.");
        }

        var instrument = MemberInstrument.Create(memberId, instrumentType, isPrimary);

        // If marking as primary, unmark others
        if (isPrimary)
        {
            await UnmarkAllPrimaryInstrumentsAsync(memberId);
        }

        return await _memberInstrumentRepository.AddAsync(instrument);
    }

    public async Task RemoveInstrumentAsync(int instrumentId)
    {
        var instrument = await _memberInstrumentRepository.GetByIdAsync(instrumentId);

        if (instrument == null)
        {
            throw new InvalidOperationException("Instrumento não encontrado.");
        }

        await _memberInstrumentRepository.DeleteAsync(instrument);
    }

    public async Task SetPrimaryInstrumentAsync(int instrumentId, string memberId)
    {
        var instrument = await _memberInstrumentRepository.QueryAsync(q => q
            .FirstOrDefaultAsync(mi => mi.Id == instrumentId && mi.MemberId == memberId));

        if (instrument == null)
        {
            throw new InvalidOperationException("Instrumento não encontrado.");
        }

        // Unmark all other instruments as primary for this member
        await UnmarkAllPrimaryInstrumentsAsync(memberId);

        // Mark this one as primary
        instrument.MarkAsPrimary();
        await _memberInstrumentRepository.UpdateAsync(instrument);
    }

    public async Task<bool> HasInstrumentAsync(string memberId, InstrumentType instrumentType)
    {
        return await _memberInstrumentRepository.AnyAsync(mi => mi.MemberId == memberId && mi.InstrumentType == instrumentType);
    }

    public async Task<MemberInstrument?> GetByIdAsync(int id)
    {
        return await _memberInstrumentRepository.GetByIdAsync(id);
    }

    /// <summary>
    /// Helper method to unmark all primary instruments for a member
    /// </summary>
    private async Task UnmarkAllPrimaryInstrumentsAsync(string memberId)
    {
        var primaryInstruments = await _memberInstrumentRepository.QueryAsync(q => q
            .Where(mi => mi.MemberId == memberId && mi.IsPrimary)
            .ToListAsync());

        // Note: Typically only 0-1 primary instruments per member, so minimal overhead
        foreach (var instrument in primaryInstruments)
        {
            instrument.UnmarkAsPrimary();
            await _memberInstrumentRepository.UpdateAsync(instrument);
        }
    }

    public async Task<Dictionary<string, List<MemberInstrument>>> GetMemberInstrumentsByUserIdsAsync(IEnumerable<string> userIds)
    {
        var instruments = await _memberInstrumentRepository.QueryAsync(q => q
            .Where(mi => userIds.Contains(mi.MemberId))
            .ToListAsync());

        return instruments
            .GroupBy(mi => mi.MemberId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }
}
