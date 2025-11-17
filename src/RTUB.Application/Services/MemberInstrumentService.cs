using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing member instruments
/// Implements business logic and data access for MemberInstrument entities
/// </summary>
public class MemberInstrumentService : IMemberInstrumentService
{
    private readonly ApplicationDbContext _context;

    public MemberInstrumentService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<MemberInstrument>> GetMemberInstrumentsAsync(string memberId)
    {
        return await _context.Set<MemberInstrument>()
            .Where(mi => mi.MemberId == memberId)
            .OrderByDescending(mi => mi.IsPrimary)
            .ThenBy(mi => mi.InstrumentType)
            .ToListAsync();
    }

    public async Task<MemberInstrument?> GetPrimaryInstrumentAsync(string memberId)
    {
        return await _context.Set<MemberInstrument>()
            .FirstOrDefaultAsync(mi => mi.MemberId == memberId && mi.IsPrimary);
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

        _context.Set<MemberInstrument>().Add(instrument);
        await _context.SaveChangesAsync();

        return instrument;
    }

    public async Task RemoveInstrumentAsync(int instrumentId)
    {
        var instrument = await _context.Set<MemberInstrument>()
            .FindAsync(instrumentId);

        if (instrument == null)
        {
            throw new InvalidOperationException("Instrumento não encontrado.");
        }

        _context.Set<MemberInstrument>().Remove(instrument);
        await _context.SaveChangesAsync();
    }

    public async Task SetPrimaryInstrumentAsync(int instrumentId, string memberId)
    {
        var instrument = await _context.Set<MemberInstrument>()
            .FirstOrDefaultAsync(mi => mi.Id == instrumentId && mi.MemberId == memberId);

        if (instrument == null)
        {
            throw new InvalidOperationException("Instrumento não encontrado.");
        }

        // Unmark all other instruments as primary for this member
        await UnmarkAllPrimaryInstrumentsAsync(memberId);

        // Mark this one as primary
        instrument.MarkAsPrimary();
        await _context.SaveChangesAsync();
    }

    public async Task<bool> HasInstrumentAsync(string memberId, InstrumentType instrumentType)
    {
        return await _context.Set<MemberInstrument>()
            .AnyAsync(mi => mi.MemberId == memberId && mi.InstrumentType == instrumentType);
    }

    public async Task<MemberInstrument?> GetByIdAsync(int id)
    {
        return await _context.Set<MemberInstrument>()
            .FirstOrDefaultAsync(mi => mi.Id == id);
    }

    /// <summary>
    /// Helper method to unmark all primary instruments for a member
    /// </summary>
    private async Task UnmarkAllPrimaryInstrumentsAsync(string memberId)
    {
        var primaryInstruments = await _context.Set<MemberInstrument>()
            .Where(mi => mi.MemberId == memberId && mi.IsPrimary)
            .ToListAsync();

        foreach (var instrument in primaryInstruments)
        {
            instrument.UnmarkAsPrimary();
        }
    }

    public async Task<Dictionary<string, List<MemberInstrument>>> GetMemberInstrumentsByUserIdsAsync(IEnumerable<string> userIds)
    {
        var instruments = await _context.Set<MemberInstrument>()
            .Where(mi => userIds.Contains(mi.MemberId))
            .ToListAsync();

        return instruments
            .GroupBy(mi => mi.MemberId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }
}
