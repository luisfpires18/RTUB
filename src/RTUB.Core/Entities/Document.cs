using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a document stored in Cloudflare R2
/// Domain entity - contains business logic for document management
/// </summary>
public class Document : BaseEntity
{
    [Required(ErrorMessage = "O ID da pasta é obrigatório")]
    public int FolderId { get; set; }

    [Required(ErrorMessage = "O nome do documento é obrigatório")]
    [MaxLength(255, ErrorMessage = "O nome do documento não pode exceder 255 caracteres")]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "O URL do Cloudflare é obrigatório")]
    [MaxLength(1000, ErrorMessage = "O URL do Cloudflare não pode exceder 1000 caracteres")]
    public string CloudflareUrl { get; set; } = string.Empty;

    [Required(ErrorMessage = "A chave do objeto é obrigatória")]
    [MaxLength(500, ErrorMessage = "A chave do objeto não pode exceder 500 caracteres")]
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long SizeBytes { get; set; }

    [MaxLength(100, ErrorMessage = "O tipo de conteúdo não pode exceder 100 caracteres")]
    public string? ContentType { get; set; }

    [MaxLength(256, ErrorMessage = "O ID do utilizador não pode exceder 256 caracteres")]
    public string? CreatedByUserId { get; set; }

    [MaxLength(256, ErrorMessage = "O nome do utilizador não pode exceder 256 caracteres")]
    public string? CreatedByUserName { get; set; }

    // Navigation properties
    public virtual Folder Folder { get; set; } = null!;

    // Private constructor for EF Core
    public Document() { }

    /// <summary>
    /// Factory method to create a new document
    /// </summary>
    public static Document Create(
        int folderId,
        string displayName,
        string cloudflareUrl,
        string objectKey,
        long sizeBytes,
        string? contentType = null,
        string? createdByUserId = null,
        string? createdByUserName = null)
    {
        if (folderId <= 0)
            throw new ArgumentException("O ID da pasta deve ser maior que zero", nameof(folderId));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("O nome do documento não pode estar vazio", nameof(displayName));

        if (string.IsNullOrWhiteSpace(cloudflareUrl))
            throw new ArgumentException("O URL do Cloudflare não pode estar vazio", nameof(cloudflareUrl));

        if (string.IsNullOrWhiteSpace(objectKey))
            throw new ArgumentException("A chave do objeto não pode estar vazia", nameof(objectKey));

        if (sizeBytes < 0)
            throw new ArgumentException("O tamanho do ficheiro não pode ser negativo", nameof(sizeBytes));

        return new Document
        {
            FolderId = folderId,
            DisplayName = displayName,
            CloudflareUrl = cloudflareUrl,
            ObjectKey = objectKey,
            SizeBytes = sizeBytes,
            ContentType = contentType,
            CreatedByUserId = createdByUserId,
            CreatedByUserName = createdByUserName
        };
    }

    /// <summary>
    /// Updates the document display name
    /// </summary>
    public void UpdateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("O nome do documento não pode estar vazio", nameof(displayName));

        DisplayName = displayName;
    }

    /// <summary>
    /// Moves the document to a different folder
    /// </summary>
    public void MoveToFolder(int newFolderId)
    {
        if (newFolderId <= 0)
            throw new ArgumentException("O ID da pasta deve ser maior que zero", nameof(newFolderId));

        FolderId = newFolderId;
    }

    /// <summary>
    /// Updates the Cloudflare URL and object key (e.g., after file replacement)
    /// </summary>
    public void UpdateCloudflareInfo(string cloudflareUrl, string objectKey, long sizeBytes, string? contentType = null)
    {
        if (string.IsNullOrWhiteSpace(cloudflareUrl))
            throw new ArgumentException("O URL do Cloudflare não pode estar vazio", nameof(cloudflareUrl));

        if (string.IsNullOrWhiteSpace(objectKey))
            throw new ArgumentException("A chave do objeto não pode estar vazia", nameof(objectKey));

        if (sizeBytes < 0)
            throw new ArgumentException("O tamanho do ficheiro não pode ser negativo", nameof(sizeBytes));

        CloudflareUrl = cloudflareUrl;
        ObjectKey = objectKey;
        SizeBytes = sizeBytes;
        ContentType = contentType;
    }

    /// <summary>
    /// Gets the file size in a human-readable format
    /// </summary>
    public string GetFormattedSize()
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = SizeBytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Checks if the document is an image based on content type
    /// </summary>
    public bool IsImage()
    {
        if (string.IsNullOrWhiteSpace(ContentType))
            return false;

        return ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if the document is a PDF
    /// </summary>
    public bool IsPdf()
    {
        return ContentType?.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) ?? false;
    }
}
