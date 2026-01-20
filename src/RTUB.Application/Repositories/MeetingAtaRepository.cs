using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for MeetingAta entity.
/// Uses IDbContextFactory to create fresh DbContext per operation to avoid EF tracking conflicts in Blazor Server.
/// </summary>
public class MeetingAtaRepository : IMeetingAtaRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public MeetingAtaRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<MeetingAta?> GetByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.MeetingAtas
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<MeetingAta?> GetByMeetingIdAsync(int meetingId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.MeetingAtas
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.MeetingId == meetingId);
    }

    public async Task<MeetingAta?> GetByIdWithDetailsAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.MeetingAtas
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
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.MeetingAtas
            .AsNoTracking()
            .Include(a => a.Meeting)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<MeetingAta> CreateAsync(MeetingAta ata)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        
        // Convert empty string to null for optional SecondSecretaryUserId to avoid FK constraint violations
        if (string.IsNullOrEmpty(ata.SecondSecretaryUserId))
            ata.SecondSecretaryUserId = null;
            
        context.MeetingAtas.Add(ata);
        await context.SaveChangesAsync();
        return ata;
    }

    public async Task UpdateAsync(MeetingAta ata)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        
        // Fetch the existing entity with its agenda points
        var existingAta = await context.MeetingAtas
            .Include(a => a.AgendaPoints)
            .FirstOrDefaultAsync(a => a.Id == ata.Id);
        if (existingAta == null)
            throw new EntityNotFoundException(nameof(MeetingAta), ata.Id);

        // Update only the scalar properties, not navigation properties
        existingAta.AtaNumber = ata.AtaNumber;
        existingAta.ActualStartTime = ata.ActualStartTime;
        existingAta.ActualEndTime = ata.ActualEndTime;
        existingAta.Location = ata.Location;
        existingAta.PresidentUserId = ata.PresidentUserId;
        existingAta.FirstSecretaryUserId = ata.FirstSecretaryUserId;
        // Convert empty string to null for optional SecondSecretaryUserId to avoid FK constraint violations
        existingAta.SecondSecretaryUserId = string.IsNullOrEmpty(ata.SecondSecretaryUserId) ? null : ata.SecondSecretaryUserId;
        existingAta.QuorumBasis = ata.QuorumBasis;
        existingAta.AttendeesPresent = ata.AttendeesPresent;
        existingAta.AttendeesAbsent = ata.AttendeesAbsent;
        existingAta.ClosingText = ata.ClosingText;
        existingAta.Status = ata.Status;
        existingAta.GeneratedAt = ata.GeneratedAt;
        existingAta.PdfStorageUrl = ata.PdfStorageUrl;
        existingAta.UpdatedAt = ata.UpdatedAt;
        existingAta.UpdatedBy = ata.UpdatedBy;

        // Update agenda points
        // Remove existing agenda points
        if (existingAta.AgendaPoints != null && existingAta.AgendaPoints.Any())
        {
            context.MeetingAtaAgendaPoints.RemoveRange(existingAta.AgendaPoints);
        }

        // Add new agenda points
        if (ata.AgendaPoints != null && ata.AgendaPoints.Any())
        {
            foreach (var point in ata.AgendaPoints)
            {
                point.Id = 0; // Reset ID for new insert
                point.MeetingAtaId = existingAta.Id;
                point.MeetingAta = null!; // Clear navigation property to prevent EF tracking conflicts
                context.MeetingAtaAgendaPoints.Add(point);
            }
        }

        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var ata = await context.MeetingAtas.FindAsync(id);
        if (ata == null)
            throw new EntityNotFoundException(nameof(MeetingAta), id);

        context.MeetingAtas.Remove(ata);
        await context.SaveChangesAsync();
    }

    public async Task<bool> ExistsForMeetingAsync(int meetingId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.MeetingAtas
            .AnyAsync(a => a.MeetingId == meetingId);
    }
}
