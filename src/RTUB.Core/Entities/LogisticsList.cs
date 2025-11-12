using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a list (column) in the logistics Kanban board
/// </summary>
public class LogisticsList : BaseEntity
{
    [Required(ErrorMessage = "O nome da lista é obrigatório")]
    [MaxLength(100, ErrorMessage = "O nome da lista não pode exceder 100 caracteres")]
    public string Name { get; set; } = string.Empty;

    [Required]
    public int Position { get; set; }

    // Navigation properties
    public virtual ICollection<LogisticsCard> Cards { get; set; } = new List<LogisticsCard>();

    // Private constructor for EF Core
    public LogisticsList() { }

    // Factory method
    public static LogisticsList Create(string name, int position)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome da lista não pode estar vazio", nameof(name));

        return new LogisticsList
        {
            Name = name,
            Position = position
        };
    }

    // Business methods
    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome da lista não pode estar vazio", nameof(name));

        Name = name;
    }

    public void UpdatePosition(int position)
    {
        Position = position;
    }
}
