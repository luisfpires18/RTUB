using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services;

/// <summary>
/// Meeting Ata service implementation
/// Contains business logic for meeting ata operations including authorization
/// Uses IDbContextFactory to create fresh DbContext per operation to avoid EF tracking conflicts in Blazor Server.
/// </summary>
public class MeetingAtaService : IMeetingAtaService
{
    private readonly IMeetingAtaRepository _ataRepository;
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public MeetingAtaService(
        IMeetingAtaRepository ataRepository,
        IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _ataRepository = ataRepository;
        _contextFactory = contextFactory;
    }

    public async Task<MeetingAta?> GetByIdAsync(int id)
    {
        return await _ataRepository.GetByIdAsync(id);
    }

    public async Task<MeetingAta?> GetByMeetingIdAsync(int meetingId)
    {
        return await _ataRepository.GetByMeetingIdAsync(meetingId);
    }

    public async Task<MeetingAta?> GetByIdWithDetailsAsync(int id)
    {
        return await _ataRepository.GetByIdWithDetailsAsync(id);
    }

    public async Task<MeetingAta> CreateAtaAsync(MeetingAta ata)
    {
        // Verify that an ata doesn't already exist for this meeting
        var existingAta = await _ataRepository.GetByMeetingIdAsync(ata.MeetingId);
        if (existingAta != null)
        {
            throw new InvalidOperationException($"Uma ata já existe para a reunião com ID {ata.MeetingId}.");
        }

        return await _ataRepository.CreateAsync(ata);
    }

    public async Task UpdateAtaAsync(MeetingAta ata)
    {
        var existingAta = await _ataRepository.GetByIdAsync(ata.Id);
        if (existingAta == null)
            throw new EntityNotFoundException(nameof(MeetingAta), ata.Id);

        // Only allow updates if the ata is still in Draft status
        if (existingAta.Status == MeetingAtaStatus.Published)
        {
            throw new InvalidOperationException("Não é possível editar uma ata publicada.");
        }

        await _ataRepository.UpdateAsync(ata);
    }

    public async Task DeleteAtaAsync(int id)
    {
        var ata = await _ataRepository.GetByIdAsync(id);
        if (ata == null)
            throw new EntityNotFoundException(nameof(MeetingAta), id);

        // Only allow deletion if the ata is still in Draft status
        if (ata.Status == MeetingAtaStatus.Published)
        {
            throw new InvalidOperationException("Não é possível eliminar uma ata publicada.");
        }

        await _ataRepository.DeleteAsync(id);
    }

    public async Task<bool> ExistsForMeetingAsync(int meetingId)
    {
        return await _ataRepository.ExistsForMeetingAsync(meetingId);
    }

    public bool CanCreateOrEditAta(string userId, Meeting meeting, IEnumerable<string> userRoles, IEnumerable<Position> userPositions)
    {
        // Owner role can create/edit any ata
        if (userRoles.Contains("Owner"))
            return true;

        // For CV meetings
        if (meeting.Type == MeetingType.ConselhoVeteranos)
        {
            // User has PresidenteConselhoVeteranos position
            if (userPositions.Contains(Position.PresidenteConselhoVeteranos))
                return true;

            // User is the delegated ata writer
            if (!string.IsNullOrEmpty(meeting.DelegatedAtaWriterMemberId) &&
                meeting.DelegatedAtaWriterMemberId == userId)
                return true;
        }

        // For AG meetings (Assembleia Geral Ordinária and Extraordinária)
        if (meeting.Type == MeetingType.AssembleiaGeralOrdinaria ||
            meeting.Type == MeetingType.AssembleiaGeralExtraordinaria)
        {
            // User has PresidenteMesaAssembleia position
            if (userPositions.Contains(Position.PresidenteMesaAssembleia))
                return true;
        }

        return false;
    }

    public bool CanViewAta(string userId, Meeting meeting, IEnumerable<string> userRoles)
    {
        // Owner role can view any ata
        if (userRoles.Contains("Owner"))
            return true;

        // The ata must be published for general viewing
        // (Draft atas are only visible to those who can create/edit)
        // This check should be performed by the caller who has access to the ata status

        // For CV meetings - only Veterans, Tunossauros, and Magister position holders can view
        if (meeting.Type == MeetingType.ConselhoVeteranos)
        {
            // This would need to be checked with the ApplicationUser
            // For now, return true - the caller should handle specific role checks
            return true;
        }

        // For AG meetings - anyone except Leitão can view
        if (meeting.Type == MeetingType.AssembleiaGeralOrdinaria ||
            meeting.Type == MeetingType.AssembleiaGeralExtraordinaria)
        {
            // This would need to check if user is Leitão
            // For now, return true - the caller should handle specific category checks
            return true;
        }

        // For other meeting types, all users can view
        return true;
    }

    public async Task AddAgendaPointAsync(int ataId, MeetingAtaAgendaPoint point)
    {
        var ata = await _ataRepository.GetByIdAsync(ataId);
        if (ata == null)
            throw new EntityNotFoundException(nameof(MeetingAta), ataId);

        if (ata.Status == MeetingAtaStatus.Published)
        {
            throw new InvalidOperationException("Não é possível adicionar pontos de agenda a uma ata publicada.");
        }

        await using var context = await _contextFactory.CreateDbContextAsync();
        point.MeetingAtaId = ataId;
        context.MeetingAtaAgendaPoints.Add(point);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAgendaPointAsync(MeetingAtaAgendaPoint point)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var existingPoint = await context.MeetingAtaAgendaPoints
            .Include(p => p.MeetingAta)
            .FirstOrDefaultAsync(p => p.Id == point.Id);

        if (existingPoint == null)
            throw new EntityNotFoundException(nameof(MeetingAtaAgendaPoint), point.Id);

        if (existingPoint.MeetingAta.Status == MeetingAtaStatus.Published)
        {
            throw new InvalidOperationException("Não é possível editar pontos de agenda de uma ata publicada.");
        }

        existingPoint.PointNumber = point.PointNumber;
        existingPoint.Title = point.Title;
        existingPoint.DiscussionSummary = point.DiscussionSummary;
        existingPoint.DecisionText = point.DecisionText;
        existingPoint.VotesFor = point.VotesFor;
        existingPoint.VotesAgainst = point.VotesAgainst;
        existingPoint.VotesAbstain = point.VotesAbstain;
        existingPoint.VoteResult = point.VoteResult;

        await context.SaveChangesAsync();
    }

    public async Task DeleteAgendaPointAsync(int pointId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var point = await context.MeetingAtaAgendaPoints
            .Include(p => p.MeetingAta)
            .FirstOrDefaultAsync(p => p.Id == pointId);

        if (point == null)
            throw new EntityNotFoundException(nameof(MeetingAtaAgendaPoint), pointId);

        if (point.MeetingAta.Status == MeetingAtaStatus.Published)
        {
            throw new InvalidOperationException("Não é possível eliminar pontos de agenda de uma ata publicada.");
        }

        context.MeetingAtaAgendaPoints.Remove(point);
        await context.SaveChangesAsync();
    }

    public async Task ReorderAgendaPointsAsync(int ataId, List<int> pointIds)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var ata = await context.MeetingAtas
            .Include(a => a.AgendaPoints)
            .FirstOrDefaultAsync(a => a.Id == ataId);

        if (ata == null)
            throw new EntityNotFoundException(nameof(MeetingAta), ataId);

        if (ata.Status == MeetingAtaStatus.Published)
        {
            throw new InvalidOperationException("Não é possível reordenar pontos de agenda de uma ata publicada.");
        }

        // Update point numbers based on the order of IDs
        for (int i = 0; i < pointIds.Count; i++)
        {
            var point = ata.AgendaPoints.FirstOrDefault(p => p.Id == pointIds[i]);
            if (point != null)
            {
                point.PointNumber = i + 1;
            }
        }

        await context.SaveChangesAsync();
    }

    public async Task AddAttachmentAsync(int ataId, MeetingAtaAttachment attachment)
    {
        var ata = await _ataRepository.GetByIdAsync(ataId);
        if (ata == null)
            throw new EntityNotFoundException(nameof(MeetingAta), ataId);

        if (ata.Status == MeetingAtaStatus.Published)
        {
            throw new InvalidOperationException("Não é possível adicionar anexos a uma ata publicada.");
        }

        await using var context = await _contextFactory.CreateDbContextAsync();
        attachment.MeetingAtaId = ataId;
        context.MeetingAtaAttachments.Add(attachment);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAttachmentAsync(MeetingAtaAttachment attachment)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var existingAttachment = await context.MeetingAtaAttachments
            .Include(a => a.MeetingAta)
            .FirstOrDefaultAsync(a => a.Id == attachment.Id);

        if (existingAttachment == null)
            throw new EntityNotFoundException(nameof(MeetingAtaAttachment), attachment.Id);

        if (existingAttachment.MeetingAta.Status == MeetingAtaStatus.Published)
        {
            throw new InvalidOperationException("Não é possível editar anexos de uma ata publicada.");
        }

        existingAttachment.AttachmentType = attachment.AttachmentType;
        existingAttachment.Name = attachment.Name;
        existingAttachment.Description = attachment.Description;
        existingAttachment.FileUrl = attachment.FileUrl;
        existingAttachment.IncludeInPdf = attachment.IncludeInPdf;

        await context.SaveChangesAsync();
    }

    public async Task DeleteAttachmentAsync(int attachmentId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var attachment = await context.MeetingAtaAttachments
            .Include(a => a.MeetingAta)
            .FirstOrDefaultAsync(a => a.Id == attachmentId);

        if (attachment == null)
            throw new EntityNotFoundException(nameof(MeetingAtaAttachment), attachmentId);

        if (attachment.MeetingAta.Status == MeetingAtaStatus.Published)
        {
            throw new InvalidOperationException("Não é possível eliminar anexos de uma ata publicada.");
        }

        context.MeetingAtaAttachments.Remove(attachment);
        await context.SaveChangesAsync();
    }
}
