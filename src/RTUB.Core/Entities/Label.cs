using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a content label/block for managing dynamic content on the site
/// </summary>
public class Label : BaseEntity
{
    [Required(ErrorMessage = "A referência da etiqueta é obrigatória")]
    [MaxLength(100, ErrorMessage = "A referência não pode exceder 100 caracteres")]
    [RegularExpression("^[a-z0-9_-]+$", ErrorMessage = "A referência deve conter apenas letras minúsculas, números, hífens e underscores")]
    public string Reference { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "O título da etiqueta é obrigatório")]
    [MaxLength(200, ErrorMessage = "O título não pode exceder 200 caracteres")]
    public string Title { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "O conteúdo da etiqueta é obrigatório")]
    [MaxLength(5000, ErrorMessage = "O conteúdo não pode exceder 5000 caracteres")]
    public string Content { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;

    // Private constructor for EF Core
    public Label() { }

    public static Label Create(string reference, string title, string content, bool isActive = true)
    {
        if (string.IsNullOrWhiteSpace(reference))
            throw new ArgumentException("A referência não pode estar vazia", nameof(reference));
        
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("O título não pode estar vazio", nameof(title));
        
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("O conteúdo não pode estar vazio", nameof(content));

        return new Label
        {
            Reference = reference,
            Title = title,
            Content = content,
            IsActive = isActive
        };
    }

    public void UpdateContent(string title, string content, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("O título não pode estar vazio", nameof(title));
        
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("O conteúdo não pode estar vazio", nameof(content));

        Title = title;
        Content = content;
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
