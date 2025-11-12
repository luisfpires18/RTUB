using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

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

    // Status and tracking
    [Required]
    public CardStatus Status { get; set; } = CardStatus.Todo;

    // Labels for organization (stored as comma-separated string)
    public string? Labels { get; set; }

    // Dates
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? ReminderDate { get; set; }

    // Checklist items (stored as JSON)
    public string? ChecklistJson { get; set; }

    // Attachments (stored as JSON - links, pages, etc.)
    public string? AttachmentsJson { get; set; }

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

    public void SetStatus(CardStatus status)
    {
        Status = status;
    }

    public void SetLabels(string? labels)
    {
        Labels = labels;
    }

    public void SetDates(DateTime? startDate, DateTime? dueDate, DateTime? reminderDate)
    {
        StartDate = startDate;
        DueDate = dueDate;
        ReminderDate = reminderDate;
    }

    public void SetChecklist(string? checklistJson)
    {
        ChecklistJson = checklistJson;
    }

    public void SetAttachments(string? attachmentsJson)
    {
        AttachmentsJson = attachmentsJson;
    }
}
