using RTUB.Core.Attributes;
using RTUB.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents an event (Atuacao, Festival, etc.)
/// Domain entity - contains only business logic, no infrastructure concerns
/// </summary>
public class Event : BaseEntity
{
    [Required(ErrorMessage = "O nome do evento é obrigatório")]
    [MaxLength(200, ErrorMessage = "O nome do evento não pode exceder 200 caracteres")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "A data do evento é obrigatória")]
    public DateTime Date { get; set; }

    [DateGreaterThan(nameof(Date), ErrorMessage = "A data de fim não pode ser anterior à data de início")]
    public DateTime? EndDate { get; set; }

    [MaxLength(2000, ErrorMessage = "A descrição não pode exceder 2000 caracteres")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "A localização é obrigatória")]
    [MaxLength(200, ErrorMessage = "A localização não pode exceder 200 caracteres")]
    public string Location { get; set; } = string.Empty;

    [Required(ErrorMessage = "O tipo de evento é obrigatório")]
    public EventType Type { get; set; }

    // Image handling - store reference/path, actual storage handled by infrastructure
    public string? ImageUrl { get; set; }

    // Cancellation tracking
    public bool IsCancelled { get; set; }

    [MaxLength(1000, ErrorMessage = "O motivo de cancelamento não pode exceder 1000 caracteres")]
    public string? CancellationReason { get; set; }

    // Navigation properties
    public virtual ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public virtual ICollection<EventRepertoire> RepertoireSongs { get; set; } = new List<EventRepertoire>();
    public virtual ICollection<Trophy> Trophies { get; set; } = new List<Trophy>();
    public virtual ICollection<EventVideo> Videos { get; set; } = new List<EventVideo>();

    // Private constructor for EF Core
    public Event() { }

    // Factory method - ensures valid entity creation
    public static Event Create(string name, DateTime date, string location, EventType type, string description = "")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome do evento não pode estar vazio", nameof(name));

        if (string.IsNullOrWhiteSpace(location))
            throw new ArgumentException("A localização do evento não pode estar vazia", nameof(location));

        return new Event
        {
            Name = name,
            Date = date,
            Location = location,
            Type = type,
            Description = description
        };
    }

    // Business methods
    public void UpdateDetails(string name, DateTime date, string location, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome do evento não pode estar vazio", nameof(name));

        if (string.IsNullOrWhiteSpace(location))
            throw new ArgumentException("A localização do evento não pode estar vazia", nameof(location));

        Name = name;
        Date = date;
        Location = location;
        Description = description;
    }

    public void SetEndDate(DateTime? endDate)
    {
        if (endDate.HasValue && endDate.Value < Date)
            throw new ArgumentException("A data de fim não pode ser anterior à data de início");

        EndDate = endDate;
    }

    public void SetImage(string? url)
    {
        ImageUrl = url;
    }

    public string GetImageSource()
    {
        return !string.IsNullOrEmpty(ImageUrl) ? ImageUrl : "";
    }

    public void Cancel(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("O motivo de cancelamento é obrigatório", nameof(reason));

        IsCancelled = true;
        CancellationReason = reason;
    }

    public void Uncancel()
    {
        IsCancelled = false;
        CancellationReason = null;
    }

    // Property alias for backward compatibility
    public string ImageSrc => GetImageSource();

    /// <summary>
    /// Calculate instrument counts for the instruments members are actively playing in this event
    /// (the selected instrument, regardless of whether it's their primary or not)
    /// </summary>
    /// <param name="memberInstruments">Dictionary mapping userId to their list of instruments</param>
    /// <returns>Dictionary with counts per instrument type</returns>
    public Dictionary<InstrumentType, int> GetPrimaryInstrumentCounts(Dictionary<string, List<MemberInstrument>> memberInstruments)
    {
        var counts = new Dictionary<InstrumentType, int>();

        foreach (var enrollment in Enrollments.Where(e => e.WillAttend && e.Instrument.HasValue && !string.IsNullOrEmpty(e.UserId)))
        {
            var instrument = enrollment.Instrument!.Value;

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

        foreach (var enrollment in Enrollments.Where(e => e.WillAttend && !string.IsNullOrEmpty(e.UserId)))
        {
            // Only parse and count instruments from the OtherInstruments field
            // The selected instrument is already counted in GetPrimaryInstrumentCounts
            if (!string.IsNullOrWhiteSpace(enrollment.OtherInstruments))
            {
                var otherInstruments = enrollment.OtherInstruments.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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
