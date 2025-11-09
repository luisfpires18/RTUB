using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a trophy earned at a Festival event
/// </summary>
public class Trophy : BaseEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    public int EventId { get; set; }
    
    // Navigation property
    public virtual Event? Event { get; set; }

    // Private constructor for EF Core
    private Trophy() { }

    // Public constructor for UI initialization (validation will happen on save)
    public Trophy(int eventId)
    {
        EventId = eventId;
    }

    public static Trophy Create(string name, int eventId)
    {
        ValidateName(name);

        if (eventId <= 0)
            throw new ArgumentException("O ID do evento deve ser maior que 0", nameof(eventId));

        return new Trophy
        {
            Name = name,
            EventId = eventId
        };
    }

    public void Update(string name)
    {
        ValidateName(name);
        Name = name;
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome do prémio não pode estar vazio", nameof(name));

        if (name.Length > 200)
            throw new ArgumentException("O nome do prémio não pode exceder 200 caracteres", nameof(name));
    }
}
