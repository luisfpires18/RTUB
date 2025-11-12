using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a physical instrument asset in the inventory
/// </summary>
public class Instrument : BaseEntity
{
    [Required(ErrorMessage = "A categoria é obrigatória")]
    [MaxLength(50, ErrorMessage = "A categoria não pode exceder 50 caracteres")]
    public string Category { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "O nome é obrigatório")]
    [MaxLength(100, ErrorMessage = "O nome não pode exceder 100 caracteres")]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(100, ErrorMessage = "O número de série não pode exceder 100 caracteres")]
    public string? SerialNumber { get; set; }
    
    [MaxLength(100, ErrorMessage = "A marca não pode exceder 100 caracteres")]
    public string? Brand { get; set; }
    
    [Required(ErrorMessage = "A condição é obrigatória")]
    public InstrumentCondition Condition { get; set; }

    [MaxLength(200)]
    public string? Location { get; set; }
    
    [MaxLength(500)]
    public string? MaintenanceNotes { get; set; }
    
    public DateTime? LastMaintenanceDate { get; set; }
    
    // Images
    public string? ImageUrl { get; set; }
    public string? ThumbnailUrl { get; set; }

    // Helper properties
    public string ImageSrc => !string.IsNullOrEmpty(ImageUrl) ? ImageUrl : "";
    public string ThumbnailSrc => !string.IsNullOrEmpty(ThumbnailUrl) ? ThumbnailUrl : ImageSrc;

    // Private constructor for EF Core
    private Instrument() { }

    /// <summary>
    /// Creates an empty Instrument instance for form initialization.
    /// Properties must be filled before saving.
    /// </summary>
    public static Instrument CreateEmpty()
    {
        return new Instrument
        {
            Category = string.Empty,
            Name = string.Empty,
            Condition = InstrumentCondition.Good
        };
    }

    public static Instrument Create(string category, string name, InstrumentCondition condition)
    {
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("A categoria do instrumento não pode estar vazia", nameof(category));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome do instrumento não pode estar vazio", nameof(name));

        if (name.Length > 100)
            throw new ArgumentException("O nome do instrumento não pode exceder 100 caracteres", nameof(name));

        return new Instrument
        {
            Category = category,
            Name = name,
            Condition = condition
        };
    }

    public void Update(string name, InstrumentCondition condition, string? serialNumber = null, 
                      string? brand = null, string? location = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome do instrumento não pode estar vazio", nameof(name));

        if (name.Length > 100)
            throw new ArgumentException("O nome do instrumento não pode exceder 100 caracteres", nameof(name));

        Name = name;
        Condition = condition;
        SerialNumber = serialNumber;
        Brand = brand;
        Location = location;
    }

    public void UpdateMaintenance(string? notes, DateTime? date)
    {
        MaintenanceNotes = notes;
        LastMaintenanceDate = date;
    }
}
