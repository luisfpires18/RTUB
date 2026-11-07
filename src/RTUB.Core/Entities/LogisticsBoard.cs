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

    /// <summary>
    /// Indicates if the board has been completed/concluded
    /// </summary>
    public bool IsCompleted { get; set; } = false;

    /// <summary>
    /// The date when the board was completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Task 067: the member responsible for the board (accountability only: no staff rights, no access rights).</summary>
    public string? OwnerUserId { get; set; }

    /// <summary>Task 067: every member (default, as before) or only its access list.</summary>
    public RTUB.Core.Enums.BoardVisibility Visibility { get; set; } = RTUB.Core.Enums.BoardVisibility.Members;

    /// <summary>Task 067: archived boards are read-only until a manager restores them.</summary>
    public DateTime? ArchivedAt { get; set; }

    /// <summary>
    /// Task 067: the board's pre-067 file folder name under docs/{env}/{year}/Logistics/ (its sanitised name then), read
    /// from every year so old files stay on the board after a rename or the 1 September rollover. "" = none (a new board,
    /// or two old boards sharing one folder); null = not resolved yet (resolved at startup).
    /// </summary>
    public string? LegacyFolder { get; set; }

    // Navigation properties
    public virtual Event? Event { get; set; }
    public virtual ICollection<LogisticsList> Lists { get; set; } = new List<LogisticsList>();
    public virtual ICollection<LogisticsBoardMember> Members { get; set; } = new List<LogisticsBoardMember>();

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

    /// <summary>
    /// Marks the board as completed
    /// </summary>
    public void MarkAsCompleted()
    {
        IsCompleted = true;
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the board as not completed (reopen)
    /// </summary>
    public void MarkAsNotCompleted()
    {
        IsCompleted = false;
        CompletedAt = null;
    }
}
