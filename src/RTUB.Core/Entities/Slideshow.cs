using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a slideshow image for the homepage
/// </summary>
public class Slideshow : BaseEntity, IValidatableObject
{
    public string ImageUrl { get; set; } = string.Empty;
    public byte[]? ImageData { get; set; }
    public string? ImageContentType { get; set; }
    
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

    public void SetImage(byte[]? imageData, string? contentType, string url = "")
    {
        ImageData = imageData;
        ImageContentType = contentType;
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
        if (ImageData != null && !string.IsNullOrEmpty(ImageContentType))
            return $"/api/images/slideshow/{Id}";
        
        return !string.IsNullOrEmpty(ImageUrl) ? ImageUrl : "";
    }

    /// <summary>
    /// Custom validation logic to ensure an image is present.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Check if a valid URL is present
        bool hasUrl = !string.IsNullOrWhiteSpace(ImageUrl);

        // Check if valid image data is present (mirroring your GetImageSource logic)
        bool hasData = ImageData != null && ImageData.Length > 0 && !string.IsNullOrWhiteSpace(ImageContentType);

        // If NEITHER is present, return a validation error.
        if (!hasUrl && !hasData)
        {
            yield return new ValidationResult(
                "An image is required. Please provide either an Image URL or upload a new image.",
                // Associate the error with both fields so it can be displayed by either
                new[] { nameof(ImageUrl), nameof(ImageData) }
            );
        }
    }

    // Property alias for backward compatibility
    public string ImageSrc => GetImageSource();
}
