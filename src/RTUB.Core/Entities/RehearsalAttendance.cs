using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// Tracks member attendance at rehearsals
/// Similar to Enrollment but for rehearsals
/// </summary>
public class RehearsalAttendance : BaseEntity
{
    [Required]
    public int RehearsalId { get; set; }
    
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    public bool Attended { get; set; } = false; // Defaults to false (pending approval) when created
    
    public InstrumentType? Instrument { get; set; }
    
    [MaxLength(500)]
    public string? Notes { get; set; }
    
    public DateTime CheckedInAt { get; set; }
    
    // Navigation
    public virtual Rehearsal? Rehearsal { get; set; }
    public virtual ApplicationUser? User { get; set; }

    // Private constructor for EF Core
    public RehearsalAttendance() { }

    // Factory method - ensures valid entity creation
    public static RehearsalAttendance Create(int rehearsalId, string userId, InstrumentType? instrument = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("O ID do utilizador não pode estar vazio", nameof(userId));
        
        if (rehearsalId <= 0)
            throw new ArgumentException("O ID do ensaio deve ser maior que 0", nameof(rehearsalId));

        return new RehearsalAttendance
        {
            RehearsalId = rehearsalId,
            UserId = userId,
            Instrument = instrument,
            CheckedInAt = DateTime.UtcNow
        };
    }

    // Business methods
    public void MarkAttendance(bool attended)
    {
        Attended = attended;
    }

    public void UpdateInstrument(InstrumentType? instrument)
    {
        Instrument = instrument;
    }
}
