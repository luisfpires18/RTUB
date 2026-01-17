using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents configuration for an instrument type in the Naipes feature
/// Allows admins to configure picture, visibility, and order for each instrument type
/// </summary>
public class NaipeTypeConfig : BaseEntity
{
    [Required]
    public InstrumentType InstrumentType { get; set; }

    /// <summary>
    /// Optional picture URL for the instrument type card
    /// </summary>
    [MaxLength(2048)]
    public string? PictureUrl { get; set; }

    /// <summary>
    /// Whether this instrument type is visible in the Naipes page
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Sort order for display (lower numbers appear first)
    /// </summary>
    public int SortOrder { get; set; }

    // Private constructor for EF Core
    private NaipeTypeConfig() { }

    /// <summary>
    /// Factory method for creating a new NaipeTypeConfig
    /// </summary>
    public static NaipeTypeConfig Create(InstrumentType instrumentType, int sortOrder, bool isVisible = true, string? pictureUrl = null)
    {
        return new NaipeTypeConfig
        {
            InstrumentType = instrumentType,
            PictureUrl = pictureUrl,
            IsVisible = isVisible,
            SortOrder = sortOrder,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Update the configuration
    /// </summary>
    public void Update(string? pictureUrl, bool isVisible, int sortOrder)
    {
        PictureUrl = pictureUrl;
        IsVisible = isVisible;
        SortOrder = sortOrder;
        UpdatedAt = DateTime.UtcNow;
    }
}
