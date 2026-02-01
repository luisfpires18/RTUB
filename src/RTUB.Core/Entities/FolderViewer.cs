using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a user with explicit viewing permissions for a specific folder
/// Used for granting access to folders beyond standard role-based permissions
/// </summary>
public class FolderViewer : BaseEntity
{
    [Required(ErrorMessage = "O ID da pasta é obrigatório")]
    public int FolderId { get; set; }

    [Required(ErrorMessage = "O ID do utilizador é obrigatório")]
    [MaxLength(450, ErrorMessage = "O ID do utilizador não pode exceder 450 caracteres")]
    public string UserId { get; set; } = string.Empty;

    [Required(ErrorMessage = "O nome do utilizador é obrigatório")]
    [MaxLength(256, ErrorMessage = "O nome do utilizador não pode exceder 256 caracteres")]
    public string UserName { get; set; } = string.Empty;

    [MaxLength(256, ErrorMessage = "O ID do utilizador que criou não pode exceder 256 caracteres")]
    public string? CreatedByUserId { get; set; }

    [MaxLength(256, ErrorMessage = "O nome do utilizador que criou não pode exceder 256 caracteres")]
    public string? CreatedByUserName { get; set; }

    // Navigation properties
    public virtual Folder Folder { get; set; } = null!;

    // Private constructor for EF Core
    public FolderViewer() { }

    /// <summary>
    /// Factory method to create a new folder viewer permission
    /// </summary>
    public static FolderViewer Create(
        int folderId,
        string userId,
        string userName,
        string? createdByUserId = null,
        string? createdByUserName = null)
    {
        if (folderId <= 0)
            throw new ArgumentException("O ID da pasta deve ser maior que zero", nameof(folderId));

        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("O ID do utilizador não pode estar vazio", nameof(userId));

        if (string.IsNullOrWhiteSpace(userName))
            throw new ArgumentException("O nome do utilizador não pode estar vazio", nameof(userName));

        return new FolderViewer
        {
            FolderId = folderId,
            UserId = userId,
            UserName = userName,
            CreatedByUserId = createdByUserId,
            CreatedByUserName = createdByUserName
        };
    }

    /// <summary>
    /// Updates the user name (e.g., when user profile is updated)
    /// </summary>
    public void UpdateUserName(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
            throw new ArgumentException("O nome do utilizador não pode estar vazio", nameof(userName));

        UserName = userName;
    }
}
