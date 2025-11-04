using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a rehearsal session
/// Only created when attendance is tracked or notes are added
/// </summary>
public class Rehearsal : BaseEntity
{
    [Required]
    public DateTime Date { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Location { get; set; } = "Centro Académico";
        
    [MaxLength(500)]
    public string? Theme { get; set; } // e.g., "Fado practice", "Christmas repertoire"
    
    [MaxLength(1000)]
    public string? Notes { get; set; }
    
    public TimeSpan StartTime { get; set; } = new TimeSpan(21, 0, 0);
    public TimeSpan EndTime { get; set; } = new TimeSpan(0, 0, 0); // Midnight
    
    public bool IsCanceled { get; set; } = false;
    
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
    public void UpdateDetails(string location, string? theme, string? notes)
    {
        if (string.IsNullOrWhiteSpace(location))
            throw new ArgumentException("A localização não pode estar vazia", nameof(location));

        Location = location;
        Theme = theme;
        Notes = notes;
    }

    public void Cancel()
    {
        IsCanceled = true;
    }

    public void Reactivate()
    {
        IsCanceled = false;
    }
}
