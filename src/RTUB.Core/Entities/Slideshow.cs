using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a slideshow image for the homepage
/// </summary>
public class Slideshow : BaseEntity, IValidatableObject
{
    public string ImageUrl { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Slideshow title is required")]
    [MaxLength(200, ErrorMessage = "Title must not exceed 200 characters")]
    public string Title { get; set; } = string.Empty;
    
    [MaxLength(1000, ErrorMessage = "Description must not exceed 1000 characters")]
    public string Description { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Order is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Order must be greater than 0")]
    public int Order { get; set; }
    
    [Required(ErrorMessage = "Interval is required")]
    [Range(1000, 10000, ErrorMessage = "Interval must be between 1000ms and 10000ms")]
    public int IntervalMs { get; set; } = 5000;
    
    public bool IsActive { get; set; } = true;

    // Private constructor for EF Core
    public Slideshow() { }

    public static Slideshow Create(string title, int order, string description = "", int intervalMs = 5000)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty", nameof(title));
        
        if (order < 1)
            throw new ArgumentException("Order must be positive", nameof(order));
        
        if (intervalMs < 1000 || intervalMs > 10000)
            throw new ArgumentException("Interval must be between 1000ms and 10000ms", nameof(intervalMs));

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
            throw new ArgumentException("Title cannot be empty", nameof(title));
        
        if (order < 1)
            throw new ArgumentException("Order must be positive", nameof(order));
        
        if (intervalMs < 1000 || intervalMs > 10000)
            throw new ArgumentException("Interval must be between 1000ms and 10000ms", nameof(intervalMs));

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
    /// Custom validation logic. Image URL is optional to allow creating slideshows without images.
    /// Images should be uploaded after slideshow creation via the admin interface.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // No validation errors - image URL is optional
        // Images can be uploaded after slideshow creation
        yield break;
    }

    // Property alias for backward compatibility
    public string ImageSrc => GetImageSource();
}
