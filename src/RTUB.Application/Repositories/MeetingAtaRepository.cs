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
        // Fetch the existing entity from the database to avoid tracking conflicts
        // with navigation properties (like ApplicationUser)
        var existingAta = await _context.MeetingAtas.FindAsync(ata.Id);
        if (existingAta == null)
            throw new EntityNotFoundException(nameof(MeetingAta), ata.Id);

        // Update only the scalar properties, not navigation properties
        existingAta.AtaNumber = ata.AtaNumber;
        existingAta.ActualStartTime = ata.ActualStartTime;
        existingAta.ActualEndTime = ata.ActualEndTime;
        existingAta.Location = ata.Location;
        existingAta.PresidentUserId = ata.PresidentUserId;
        existingAta.FirstSecretaryUserId = ata.FirstSecretaryUserId;
        existingAta.SecondSecretaryUserId = ata.SecondSecretaryUserId;
        existingAta.QuorumBasis = ata.QuorumBasis;
        existingAta.AttendeesPresent = ata.AttendeesPresent;
        existingAta.AttendeesAbsent = ata.AttendeesAbsent;
        existingAta.ClosingText = ata.ClosingText;
        existingAta.Status = ata.Status;
        existingAta.GeneratedAt = ata.GeneratedAt;
        existingAta.PdfStorageUrl = ata.PdfStorageUrl;
        existingAta.UpdatedAt = ata.UpdatedAt;
        existingAta.UpdatedBy = ata.UpdatedBy;

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
