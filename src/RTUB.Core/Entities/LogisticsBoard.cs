using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a logistics board (collection of lists)
/// Can optionally be associated with an event
/// </summary>
public class LogisticsBoard : BaseEntity
{
    [Required(ErrorMessage = "O nome do quadro é obrigatório")]
    [MaxLength(200, ErrorMessage = "O nome do quadro não pode exceder 200 caracteres")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000, ErrorMessage = "A descrição não pode exceder 2000 caracteres")]
    public string Description { get; set; } = string.Empty;

    // Optional event association (1:1 or 1:0)
    public int? EventId { get; set; }

    // Navigation properties
    public virtual Event? Event { get; set; }
    public virtual ICollection<LogisticsList> Lists { get; set; } = new List<LogisticsList>();

    // Private constructor for EF Core
    public LogisticsBoard() { }

    // Factory method
    public static LogisticsBoard Create(string name, string description = "")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome do quadro não pode estar vazio", nameof(name));

        return new LogisticsBoard
        {
            Name = name,
            Description = description
        };
    }

    // Business methods
    public void UpdateDetails(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome do quadro não pode estar vazio", nameof(name));

        Name = name;
        Description = description;
    }

    public void AssociateWithEvent(int? eventId)
    {
        EventId = eventId;
    }
}
