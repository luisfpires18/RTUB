using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a slideshow image for the homepage
/// </summary>
public class Slideshow : BaseEntity, IValidatableObject
{
    public string ImageUrl { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "O título do slideshow é obrigatório")]
    [MaxLength(200, ErrorMessage = "O título não pode exceder 200 caracteres")]
    public string Title { get; set; } = string.Empty;
    
    [MaxLength(1000, ErrorMessage = "A descrição não pode exceder 1000 caracteres")]
    public string Description { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "A ordem é obrigatória")]
    [Range(1, int.MaxValue, ErrorMessage = "A ordem deve ser maior que 0")]
    public int Order { get; set; }
    
    [Required(ErrorMessage = "O intervalo é obrigatório")]
    [Range(1000, 10000, ErrorMessage = "O intervalo deve estar entre 1000ms e 10000ms")]
    public int IntervalMs { get; set; } = 5000;
    
    public bool IsActive { get; set; } = true;

    // Private constructor for EF Core
    public Slideshow() { }

    public static Slideshow Create(string title, int order, string description = "", int intervalMs = 5000)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("O título não pode estar vazio", nameof(title));
        
        if (order < 1)
            throw new ArgumentException("A ordem deve ser positiva", nameof(order));
        
        if (intervalMs < 1000 || intervalMs > 10000)
            throw new ArgumentException("O intervalo deve estar entre 1000ms e 10000ms", nameof(intervalMs));

        return new Slideshow
        {
            Title = title,
            Order = order,
            Description = description,
            IntervalMs = intervalMs
        };
    }

    public void UpdateDetails(string title, string description, int order, int intervalMs)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("O título não pode estar vazio", nameof(title));
        
        if (order < 1)
            throw new ArgumentException("A ordem deve ser positiva", nameof(order));
        
        if (intervalMs < 1000 || intervalMs > 10000)
            throw new ArgumentException("O intervalo deve estar entre 1000ms e 10000ms", nameof(intervalMs));

        Title = title;
        Description = description;
        Order = order;
        IntervalMs = intervalMs;
    }

    public void SetImage(string url)
    {
        ImageUrl = url;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public string GetImageSource()
    {
        return !string.IsNullOrEmpty(ImageUrl) ? ImageUrl : "";
    }

    /// <summary>
    /// Custom validation logic.
    /// Image validation is handled at the UI layer during create/edit operations.
    /// This allows proper handling of image uploads through the multi-step process:
    /// - Create: Entity is created first, then image is uploaded
    /// - Edit: Existing image is preserved unless a new one is uploaded
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // No validation errors - image handling is managed by the UI layer
        yield break;
    }

    // Property alias for backward compatibility
    public string ImageSrc => GetImageSource();
}
