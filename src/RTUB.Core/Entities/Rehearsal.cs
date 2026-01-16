using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

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

    /// <summary>
    /// Calculate instrument counts for the instruments members are actively playing in this rehearsal
    /// (the selected instrument, regardless of whether it's their primary or not)
    /// </summary>
    /// <param name="memberInstruments">Dictionary mapping userId to their list of instruments</param>
    /// <returns>Dictionary with counts per instrument type</returns>
    public Dictionary<InstrumentType, int> GetPrimaryInstrumentCounts(Dictionary<string, List<MemberInstrument>> memberInstruments)
    {
        var counts = new Dictionary<InstrumentType, int>();

        foreach (var attendance in Attendances.Where(a => a.WillAttend && a.Instrument.HasValue && !string.IsNullOrEmpty(a.UserId)))
        {
            var instrument = attendance.Instrument!.Value;

            // Count the selected instrument (what the member is playing)
            counts[instrument] = counts.GetValueOrDefault(instrument) + 1;
        }

        return counts;
    }

    /// <summary>
    /// Calculate other instrument counts (where the member is using a non-primary instrument)
    /// </summary>
    /// <param name="memberInstruments">Dictionary mapping userId to their list of instruments</param>
    /// <returns>Dictionary with counts per instrument type</returns>
    public Dictionary<InstrumentType, int> GetOtherInstrumentCounts(Dictionary<string, List<MemberInstrument>> memberInstruments)
    {
        var counts = new Dictionary<InstrumentType, int>();

        foreach (var attendance in Attendances.Where(a => a.WillAttend && !string.IsNullOrEmpty(a.UserId)))
        {
            // Only parse and count instruments from the OtherInstruments field
            // The selected instrument is already counted in GetPrimaryInstrumentCounts
            if (!string.IsNullOrWhiteSpace(attendance.OtherInstruments))
            {
                var otherInstruments = attendance.OtherInstruments.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                foreach (var instrumentName in otherInstruments)
                {
                    // Convert display name back to enum using InstrumentTypeHelper
                    var instrumentType = RTUB.Core.Helpers.InstrumentTypeHelper.ParseDisplayName(instrumentName.Trim());
                    if (instrumentType.HasValue)
                    {
                        counts[instrumentType.Value] = counts.GetValueOrDefault(instrumentType.Value) + 1;
                    }
                }
            }
        }

        return counts;
    }
}
