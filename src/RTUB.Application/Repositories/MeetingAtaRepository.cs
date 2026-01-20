using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for MeetingAta entity
/// </summary>
public class MeetingAtaRepository : IMeetingAtaRepository
{
    private readonly ApplicationDbContext _context;

    public MeetingAtaRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<MeetingAta?> GetByIdAsync(int id)
    {
        return await _context.MeetingAtas
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<MeetingAta?> GetByMeetingIdAsync(int meetingId)
    {
        return await _context.MeetingAtas
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.MeetingId == meetingId);
    }

    public async Task<MeetingAta?> GetByIdWithDetailsAsync(int id)
    {
        return await _context.MeetingAtas
            .AsNoTracking()
            .Include(a => a.Meeting)
            .Include(a => a.PresidentUser)
            .Include(a => a.FirstSecretaryUser)
            .Include(a => a.SecondSecretaryUser)
            .Include(a => a.AgendaPoints.OrderBy(p => p.PointNumber))
            .Include(a => a.Attachments)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<IEnumerable<MeetingAta>> GetAllAsync()
    {
        return await _context.MeetingAtas
            .AsNoTracking()
            .Include(a => a.Meeting)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<MeetingAta> CreateAsync(MeetingAta ata)
    {
        _context.MeetingAtas.Add(ata);
        await _context.SaveChangesAsync();
        return ata;
    }

    public async Task UpdateAsync(MeetingAta ata)
    {
        _context.MeetingAtas.Update(ata);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var ata = await _context.MeetingAtas.FindAsync(id);
        if (ata == null)
            throw new EntityNotFoundException(nameof(MeetingAta), id);

        _context.MeetingAtas.Remove(ata);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsForMeetingAsync(int meetingId)
    {
        return await _context.MeetingAtas
            .AnyAsync(a => a.MeetingId == meetingId);
    }
}
