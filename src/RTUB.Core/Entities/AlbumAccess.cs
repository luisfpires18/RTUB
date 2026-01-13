using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a user's access to an exclusive album.
/// This is a join table between Album and ApplicationUser.
/// </summary>
public class AlbumAccess : BaseEntity
{
    [Required(ErrorMessage = "O ID do álbum é obrigatório")]
    [Range(1, int.MaxValue, ErrorMessage = "O ID do álbum deve ser maior que 0")]
    public int AlbumId { get; set; }

    [Required(ErrorMessage = "O ID do utilizador é obrigatório")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the user was granted access to the album
    /// </summary>
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Album? Album { get; set; }
    public virtual ApplicationUser? User { get; set; }

    // Private constructor for EF Core
    public AlbumAccess() { }

    public static AlbumAccess Create(int albumId, string userId)
    {
        if (albumId <= 0)
            throw new ArgumentException("O ID do álbum deve ser maior que 0", nameof(albumId));

        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("O ID do utilizador não pode estar vazio", nameof(userId));

        return new AlbumAccess
        {
            AlbumId = albumId,
            UserId = userId
        };
    }
}
