using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a card in the logistics Kanban board
/// Can optionally be associated with an event
/// </summary>
public class LogisticsCard : BaseEntity
{
    [Required(ErrorMessage = "O título do cartão é obrigatório")]
    [MaxLength(200, ErrorMessage = "O título não pode exceder 200 caracteres")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000, ErrorMessage = "A descrição não pode exceder 2000 caracteres")]
    public string Description { get; set; } = string.Empty;

    [Required]
    public int ListId { get; set; }

    [Required]
    public int Position { get; set; }

    // Optional event association
    public int? EventId { get; set; }

    // Optional assignee
    public string? AssignedToUserId { get; set; }

    // Navigation properties
    public virtual LogisticsList List { get; set; } = null!;
    public virtual Event? Event { get; set; }
    public virtual ApplicationUser? AssignedToUser { get; set; }

    // Private constructor for EF Core
    public LogisticsCard() { }

    // Factory method
    public static LogisticsCard Create(string title, int listId, int position, string description = "")
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("O título do cartão não pode estar vazio", nameof(title));

        return new LogisticsCard
        {
            Title = title,
            Description = description,
            ListId = listId,
            Position = position
        };
    }

    // Business methods
    public void UpdateContent(string title, string description)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("O título do cartão não pode estar vazio", nameof(title));

        Title = title;
        Description = description;
    }

    public void MoveToList(int newListId, int newPosition)
    {
        ListId = newListId;
        Position = newPosition;
    }

    public void UpdatePosition(int position)
    {
        Position = position;
    }

    public void AssociateWithEvent(int? eventId)
    {
        EventId = eventId;
    }

    public void AssignToUser(string? userId)
    {
        AssignedToUserId = userId;
    }
}
