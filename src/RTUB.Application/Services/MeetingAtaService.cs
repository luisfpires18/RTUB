using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Extensions;
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

    public async Task<Dictionary<int, MeetingAta>> GetByMeetingIdsAsync(IEnumerable<int> meetingIds)
    {
        return await _ataRepository.GetByMeetingIdsAsync(meetingIds);
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

    public async Task<bool> ExistsForMeetingAsync(int meetingId)
    {
        return await _ataRepository.ExistsForMeetingAsync(meetingId);
    }

    public bool CanCreateOrEditAta(string userId, Meeting meeting, IEnumerable<string> userRoles, IEnumerable<Position> userPositions, MeetingAta? existingAta = null)
    {
        // Owner and Admin roles can create/edit any ata
        if (userRoles.Contains("Owner") || userRoles.Contains("Admin"))
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

            // Tuno representative can create/edit ATA
            if (!string.IsNullOrEmpty(meeting.TunoRepresentativeUserId) &&
                meeting.TunoRepresentativeUserId == userId)
                return true;

            // If ATA exists, check if user is the secretary (FirstSecretaryUserId)
            if (existingAta != null && !string.IsNullOrEmpty(existingAta.FirstSecretaryUserId) &&
                existingAta.FirstSecretaryUserId == userId)
                return true;
        }

        // For AG meetings (Assembleia Geral Ordinária and Extraordinária)
        if (meeting.Type == MeetingType.AssembleiaGeralOrdinaria ||
            meeting.Type == MeetingType.AssembleiaGeralExtraordinaria)
        {
            // User has PresidenteMesaAssembleia position
            if (userPositions.Contains(Position.PresidenteMesaAssembleia))
                return true;

            // If ATA exists, check if user is one of the secretaries
            if (existingAta != null)
            {
                if (!string.IsNullOrEmpty(existingAta.FirstSecretaryUserId) &&
                    existingAta.FirstSecretaryUserId == userId)
                    return true;
                if (!string.IsNullOrEmpty(existingAta.SecondSecretaryUserId) &&
                    existingAta.SecondSecretaryUserId == userId)
                    return true;
            }
        }

        // For Direcao meetings
        if (meeting.Type == MeetingType.ReuniaoDirecao)
        {
            // Magister can create/edit ATAs (equivalent to PresidenteMesaAssembleia for AG)
            if (userPositions.Contains(Position.Magister))
                return true;

            // Secretario can edit existing ATAs
            if (existingAta != null && !string.IsNullOrEmpty(existingAta.FirstSecretaryUserId) &&
                existingAta.FirstSecretaryUserId == userId)
                return true;
        }

        return false;
    }

    public async Task PublishAtaAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var ata = await context.MeetingAtas.FindAsync(id);

        if (ata == null)
            throw new EntityNotFoundException(nameof(MeetingAta), id);

        if (ata.Status == MeetingAtaStatus.Published)
        {
            throw new InvalidOperationException("A ata já está publicada.");
        }

        ata.Status = MeetingAtaStatus.Published;
        await context.SaveChangesAsync();
    }
}
