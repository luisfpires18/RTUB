using Microsoft.EntityFrameworkCore;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing meeting ATA confirmations
/// </summary>
public class MeetingAtaConfirmationService : IMeetingAtaConfirmationService
{
    private readonly IRepository<MeetingAtaConfirmation> _confirmationRepository;
    private readonly IRepository<MeetingAta> _ataRepository;
    private readonly IRepository<MeetingParticipation> _participationRepository;

    public MeetingAtaConfirmationService(
        IRepository<MeetingAtaConfirmation> confirmationRepository,
        IRepository<MeetingAta> ataRepository,
        IRepository<MeetingParticipation> participationRepository)
    {
        _confirmationRepository = confirmationRepository;
        _ataRepository = ataRepository;
        _participationRepository = participationRepository;
    }

    /// <summary>
    /// Gets a user's confirmation for an ATA
    /// </summary>
    public async Task<MeetingAtaConfirmation?> GetUserConfirmationAsync(int ataId, string userId)
    {
        return await _confirmationRepository.Query()
            .FirstOrDefaultAsync(c => c.MeetingAtaId == ataId && c.UserId == userId);
    }

    /// <summary>
    /// Confirms an ATA for a user
    /// </summary>
    public async Task ConfirmAtaAsync(int ataId, string userId, string? notes = null)
    {
        // Verify ATA exists and is published
        var ata = await _ataRepository.GetByIdOrThrowAsync(ataId);
        if (ata.Status != Core.Enums.MeetingAtaStatus.Published)
        {
            throw new InvalidOperationException("Só é possível confirmar atas publicadas.");
        }

        // Verify user participated (WillAttend = true)
        var participation = await _participationRepository.Query()
            .FirstOrDefaultAsync(p => p.MeetingId == ata.MeetingId && p.UserId == userId && p.WillAttend);

        if (participation == null)
        {
            throw new InvalidOperationException("Só é possível confirmar atas de reuniões em que participou.");
        }

        // Get or create confirmation
        var confirmation = await GetUserConfirmationAsync(ataId, userId);
        if (confirmation == null)
        {
            confirmation = MeetingAtaConfirmation.Create(ataId, userId);
            confirmation.Confirm(notes);
            await _confirmationRepository.AddAsync(confirmation);
        }
        else
        {
            confirmation.Confirm(notes);
            await _confirmationRepository.UpdateAsync(confirmation);
        }
    }

    /// <summary>
    /// Refuses an ATA for a user
    /// </summary>
    public async Task RefuseAtaAsync(int ataId, string userId, string? notes = null)
    {
        // Verify ATA exists and is published
        var ata = await _ataRepository.GetByIdOrThrowAsync(ataId);
        if (ata.Status != Core.Enums.MeetingAtaStatus.Published)
        {
            throw new InvalidOperationException("Só é possível recusar atas publicadas.");
        }

        // Verify user participated (WillAttend = true)
        var participation = await _participationRepository.Query()
            .FirstOrDefaultAsync(p => p.MeetingId == ata.MeetingId && p.UserId == userId && p.WillAttend);

        if (participation == null)
        {
            throw new InvalidOperationException("Só é possível recusar atas de reuniões em que participou.");
        }

        // Get or create confirmation
        var confirmation = await GetUserConfirmationAsync(ataId, userId);
        if (confirmation == null)
        {
            confirmation = MeetingAtaConfirmation.Create(ataId, userId);
            confirmation.Refuse(notes);
            await _confirmationRepository.AddAsync(confirmation);
        }
        else
        {
            confirmation.Refuse(notes);
            await _confirmationRepository.UpdateAsync(confirmation);
        }
    }

    /// <summary>
    /// Gets all confirmations for an ATA
    /// </summary>
    public async Task<IEnumerable<MeetingAtaConfirmation>> GetConfirmationsByAtaIdAsync(int ataId)
    {
        return await _confirmationRepository.Query()
            .Where(c => c.MeetingAtaId == ataId)
            .Include(c => c.User)
            .ToListAsync();
    }
}
