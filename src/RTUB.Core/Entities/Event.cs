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
    
    // S3 Storage - filename in IDrive S3 bucket (WebP format)
    public string? S3ImageFilename { get; set; }
    
    // Legacy ImageUrl field - kept for traceability of original photo source
    public string ImageUrl { get; set; } = string.Empty;
    
    // Navigation properties
    public virtual ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public virtual ICollection<EventRepertoire> RepertoireSongs { get; set; } = new List<EventRepertoire>();
    public virtual ICollection<Trophy> Trophies { get; set; } = new List<Trophy>();

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

    public void SetS3Image(string? s3Filename)
    {
        S3ImageFilename = s3Filename;
    }

    public string GetImageSource()
    {
        // Return S3 image marker if filename exists
        if (!string.IsNullOrEmpty(S3ImageFilename))
            return $"s3:{S3ImageFilename}";
        
        return string.Empty;
    }
    
    // Property alias for backward compatibility
    public string ImageSrc => GetImageSource();
}
