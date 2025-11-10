using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a music album
/// </summary>
public class Album : BaseEntity
{
    [Required(ErrorMessage = "O título do álbum é obrigatório")]
    [MaxLength(200, ErrorMessage = "O título do álbum não pode exceder 200 caracteres")]
    public string Title { get; set; } = string.Empty;
    
    [MaxLength(1000, ErrorMessage = "A descrição não pode exceder 1000 caracteres")]
    public string? Description { get; set; }
    
    [Range(1900, 2100, ErrorMessage = "O ano deve estar entre 1900 e 2100")]
    public int? Year { get; set; }
    
    // Image handling
    public string? ImageUrl { get; set; }
    
    // Navigation property
    public virtual ICollection<Song> Songs { get; set; } = new List<Song>();

    // Private constructor for EF Core
    public Album() { }

    public static Album Create(string title, int? year, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("O título do álbum não pode estar vazio", nameof(title));
        
        if (year.HasValue && (year < 1900 || year > DateTime.Now.Year))
            throw new ArgumentException("Ano inválido", nameof(year));

        return new Album
        {
            Title = title,
            Year = year,
            Description = description
        };
    }

    public void UpdateDetails(string title, int? year, string? description)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("O título do álbum não pode estar vazio", nameof(title));
        
        if (year.HasValue && (year < 1900 || year > DateTime.Now.Year))
            throw new ArgumentException("Ano inválido", nameof(year));

        Title = title;
        Year = year;
        Description = description;
    }

    public void SetCoverImage(string? url)
    {
        ImageUrl = url;
    }

    public string GetCoverImageSource()
    {
        return !string.IsNullOrEmpty(ImageUrl) ? RTUB.Core.Helpers.ImageUrlHelper.ToProxiedUrl(ImageUrl) : "";
    }
    
    // Property alias for backward compatibility
    public string CoverImageSrc => GetCoverImageSource();
}
