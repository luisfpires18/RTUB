using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a product in the shop (albums, pins, t-shirts, etc.)
/// </summary>
public class Product : BaseEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "O preço deve ser maior que 0")]
    public decimal Price { get; set; }
    
    [Required]
    [Range(0, int.MaxValue, ErrorMessage = "O stock não pode ser negativo")]
    public int Stock { get; set; }
    
    public bool IsAvailable { get; set; } = true;
    
    public bool IsPublic { get; set; } = true;
    
    // Image
    public string? ImageUrl { get; set; }

    // Helper property
    public string ImageSrc => !string.IsNullOrEmpty(ImageUrl) ? RTUB.Core.Helpers.ImageUrlHelper.ToProxiedUrl(ImageUrl) : "";

    // Private constructor for EF Core
    private Product() { }

    /// <summary>
    /// Creates an empty Product instance for form initialization.
    /// Properties must be filled before saving.
    /// </summary>
    public static Product CreateEmpty()
    {
        return new Product
        {
            Name = string.Empty,
            Type = string.Empty,
            Price = 0,
            Stock = 0,
            IsAvailable = true,
            IsPublic = true
        };
    }

    public static Product Create(string name, string type, decimal price, int stock = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome do produto não pode estar vazio", nameof(name));

        if (name.Length > 200)
            throw new ArgumentException("O nome do produto não pode exceder 200 caracteres", nameof(name));

        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("O tipo de produto não pode estar vazio", nameof(type));

        if (type.Length > 50)
            throw new ArgumentException("O tipo de produto não pode exceder 50 caracteres", nameof(type));

        if (price < 0)
            throw new ArgumentException("O preço do produto não pode ser negativo", nameof(price));

        if (price == 0)
            throw new ArgumentException("O preço do produto deve ser maior que 0", nameof(price));

        if (stock < 0)
            throw new ArgumentException("O stock do produto não pode ser negativo", nameof(stock));

        return new Product
        {
            Name = name,
            Type = type,
            Price = price,
            Stock = stock,
            IsAvailable = true
        };
    }

    public void Update(string name, string type, decimal price, int stock, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome do produto não pode estar vazio", nameof(name));

        if (name.Length > 200)
            throw new ArgumentException("O nome do produto não pode exceder 200 caracteres", nameof(name));

        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("O tipo de produto não pode estar vazio", nameof(type));

        if (type.Length > 50)
            throw new ArgumentException("O tipo de produto não pode exceder 50 caracteres", nameof(type));

        if (price < 0)
            throw new ArgumentException("O preço do produto não pode ser negativo", nameof(price));

        if (price == 0)
            throw new ArgumentException("O preço do produto deve ser maior que 0", nameof(price));

        if (stock < 0)
            throw new ArgumentException("O stock do produto não pode ser negativo", nameof(stock));

        Name = name;
        Type = type;
        Price = price;
        Stock = stock;
        Description = description;
    }

    public void UpdateStock(int newStock)
    {
        if (newStock < 0)
            throw new ArgumentException("O stock do produto não pode ser negativo", nameof(newStock));

        Stock = newStock;
    }

    public void SetAvailability(bool isAvailable)
    {
        IsAvailable = isAvailable;
    }

    public void SetPublicVisibility(bool isPublic)
    {
        IsPublic = isPublic;
    }
}
