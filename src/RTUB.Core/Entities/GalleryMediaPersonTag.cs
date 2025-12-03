using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a person tagged in a gallery media item
/// Join entity for many-to-many relationship between GalleryMedia and ApplicationUser
/// </summary>
public class GalleryMediaPersonTag : BaseEntity
{
    [Required(ErrorMessage = "O ID da media é obrigatório")]
    public int GalleryMediaId { get; set; }

    [Required(ErrorMessage = "O ID do utilizador é obrigatório")]
    public string UserId { get; set; } = string.Empty;

    // Navigation properties
    public virtual GalleryMedia GalleryMedia { get; set; } = null!;
    public virtual ApplicationUser User { get; set; } = null!;

    // Private constructor for EF Core
    private GalleryMediaPersonTag() { }

    /// <summary>
    /// Factory method to create a new person tag
    /// </summary>
    public static GalleryMediaPersonTag Create(int galleryMediaId, string userId)
    {
        if (galleryMediaId <= 0)
            throw new ArgumentException("O ID da media deve ser maior que 0", nameof(galleryMediaId));

        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("O ID do utilizador não pode estar vazio", nameof(userId));

        return new GalleryMediaPersonTag
        {
            GalleryMediaId = galleryMediaId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
