using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a rehearsal session
/// Only created when attendance is tracked or notes are added
/// </summary>
public class Rehearsal : BaseEntity
{
    [Required(ErrorMessage = "A data é obrigatória")]
    public DateTime Date { get; set; }

    [Required(ErrorMessage = "A localização é obrigatória")]
    [MaxLength(200, ErrorMessage = "A localização não pode exceder 200 caracteres")]
    public string Location { get; set; } = "Centro Académico";

    [MaxLength(500, ErrorMessage = "O tema não pode exceder 500 caracteres")]
    public string? Theme { get; set; } // e.g., "Fado practice", "Christmas repertoire"

    [MaxLength(1000, ErrorMessage = "A descrição não pode exceder 1000 caracteres")]
    public string? Description { get; set; }

    [MaxLength(1000, ErrorMessage = "As notas não podem exceder 1000 caracteres")]
    public string? Notes { get; set; }

    public TimeSpan StartTime { get; set; } = new TimeSpan(21, 30, 0);
    public TimeSpan EndTime { get; set; } = new TimeSpan(0, 0, 0); // Midnight

    public bool IsCanceled { get; set; } = false;

    [MaxLength(1000, ErrorMessage = "O motivo de cancelamento não pode exceder 1000 caracteres")]
    public string? CancellationReason { get; set; }

    // Navigation
    public virtual ICollection<RehearsalAttendance> Attendances { get; set; } = new List<RehearsalAttendance>();

    // Private constructor for EF Core
    public Rehearsal() { }

    // Factory method - ensures valid entity creation
    public static Rehearsal Create(DateTime date, string location, string? theme = null)
    {
        if (string.IsNullOrWhiteSpace(location))
            throw new ArgumentException("A localização não pode estar vazia", nameof(location));

        return new Rehearsal
        {
            Date = date.Date, // Normalize to date only
            Location = location,
            Theme = theme
        };
    }

    // Business methods
    public void UpdateDetails(string location, string? theme, string? description, string? notes)
    {
        if (string.IsNullOrWhiteSpace(location))
            throw new ArgumentException("A localização não pode estar vazia", nameof(location));

        Location = location;
        Theme = theme;
        Description = description;
        Notes = notes;
    }

    public void Cancel(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("O motivo de cancelamento é obrigatório", nameof(reason));

        IsCanceled = true;
        CancellationReason = reason;
    }

    public void Uncancel()
    {
        IsCanceled = false;
        CancellationReason = null;
    }
}
