using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a folder for organizing documents
/// Domain entity - contains business logic for folder management
/// </summary>
public class Folder : BaseEntity
{
    [Required(ErrorMessage = "O nome da pasta é obrigatório")]
    [MaxLength(100, ErrorMessage = "O nome da pasta não pode exceder 100 caracteres")]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "A chave normalizada é obrigatória")]
    [MaxLength(100, ErrorMessage = "A chave normalizada não pode exceder 100 caracteres")]
    public string NormalizedKey { get; set; } = string.Empty;

    /// <summary>
    /// Indicates if this is a special folder with visibility restrictions
    /// </summary>
    public bool IsSpecial { get; set; } = false;

    /// <summary>
    /// Specifies the visibility level for special folders
    /// Only applicable when IsSpecial is true
    /// </summary>
    public SpecialVisibility? SpecialVisibility { get; set; }

    [MaxLength(256, ErrorMessage = "O ID do utilizador não pode exceder 256 caracteres")]
    public string? CreatedByUserId { get; set; }

    [MaxLength(256, ErrorMessage = "O nome do utilizador não pode exceder 256 caracteres")]
    public string? CreatedByUserName { get; set; }

    // Navigation properties
    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
    public virtual ICollection<FolderViewer> FolderViewers { get; set; } = new List<FolderViewer>();

    // Private constructor for EF Core
    public Folder() { }

    /// <summary>
    /// Factory method to create a standard folder
    /// </summary>
    public static Folder Create(string displayName, string normalizedKey, string? createdByUserId = null, string? createdByUserName = null)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("O nome da pasta não pode estar vazio", nameof(displayName));

        if (string.IsNullOrWhiteSpace(normalizedKey))
            throw new ArgumentException("A chave normalizada não pode estar vazia", nameof(normalizedKey));

        return new Folder
        {
            DisplayName = displayName,
            NormalizedKey = normalizedKey,
            IsSpecial = false,
            SpecialVisibility = null,
            CreatedByUserId = createdByUserId,
            CreatedByUserName = createdByUserName
        };
    }

    /// <summary>
    /// Factory method to create a special folder with visibility restrictions
    /// </summary>
    public static Folder CreateSpecial(
        string displayName,
        string normalizedKey,
        SpecialVisibility specialVisibility,
        string? createdByUserId = null,
        string? createdByUserName = null)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("O nome da pasta não pode estar vazio", nameof(displayName));

        if (string.IsNullOrWhiteSpace(normalizedKey))
            throw new ArgumentException("A chave normalizada não pode estar vazia", nameof(normalizedKey));

        if (specialVisibility == Enums.SpecialVisibility.None)
            throw new ArgumentException("Visibilidade especial não pode ser 'None' para pastas especiais", nameof(specialVisibility));

        return new Folder
        {
            DisplayName = displayName,
            NormalizedKey = normalizedKey,
            IsSpecial = true,
            SpecialVisibility = specialVisibility,
            CreatedByUserId = createdByUserId,
            CreatedByUserName = createdByUserName
        };
    }

    /// <summary>
    /// Updates the folder display name
    /// </summary>
    public void UpdateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("O nome da pasta não pode estar vazio", nameof(displayName));

        DisplayName = displayName;
    }

    /// <summary>
    /// Converts a folder to a special folder with visibility restrictions
    /// </summary>
    public void ConvertToSpecial(SpecialVisibility specialVisibility)
    {
        if (specialVisibility == Enums.SpecialVisibility.None)
            throw new ArgumentException("Visibilidade especial não pode ser 'None'", nameof(specialVisibility));

        IsSpecial = true;
        SpecialVisibility = specialVisibility;
    }

    /// <summary>
    /// Converts a special folder back to a standard folder
    /// </summary>
    public void ConvertToStandard()
    {
        IsSpecial = false;
        SpecialVisibility = null;
    }

    /// <summary>
    /// Updates the visibility level for special folders
    /// </summary>
    public void UpdateSpecialVisibility(SpecialVisibility specialVisibility)
    {
        if (!IsSpecial)
            throw new InvalidOperationException("Não é possível definir visibilidade especial numa pasta normal");

        if (specialVisibility == Enums.SpecialVisibility.None)
            throw new ArgumentException("Visibilidade especial não pode ser 'None'", nameof(specialVisibility));

        SpecialVisibility = specialVisibility;
    }
}
