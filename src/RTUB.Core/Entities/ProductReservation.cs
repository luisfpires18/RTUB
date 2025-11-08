using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a reservation for a non-public product
/// </summary>
public class ProductReservation : BaseEntity
{
    [Required]
    public int ProductId { get; set; }
    
    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(200)]
    public string UserNickname { get; set; } = string.Empty;
    
    [MaxLength(200)]
    public string? DisplayName { get; set; }
    
    public bool HasSizes { get; set; }
    
    [MaxLength(10)]
    public string? Size { get; set; }
    
    // Navigation properties
    public Product? Product { get; set; }
    public ApplicationUser? User { get; set; }
    
    // Private constructor for EF Core
    private ProductReservation() { }
    
    public static ProductReservation Create(int productId, string userId, string userNickname, bool hasSizes, string? size = null, string? displayName = null)
    {
        if (productId <= 0)
            throw new ArgumentException("O ID do produto é inválido", nameof(productId));
        
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("O ID do utilizador não pode estar vazio", nameof(userId));
        
        if (string.IsNullOrWhiteSpace(userNickname))
            throw new ArgumentException("O nickname do utilizador não pode estar vazio", nameof(userNickname));
        
        if (hasSizes && string.IsNullOrWhiteSpace(size))
            throw new ArgumentException("O tamanho é obrigatório quando o produto tem tamanhos", nameof(size));
        
        return new ProductReservation
        {
            ProductId = productId,
            UserId = userId,
            UserNickname = userNickname,
            HasSizes = hasSizes,
            Size = size,
            DisplayName = displayName
        };
    }
}
