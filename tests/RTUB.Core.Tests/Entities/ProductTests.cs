using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

/// <summary>
/// Unit tests for Product entity
/// </summary>
public class ProductTests
{
    [Fact]
    public void Create_WithValidData_CreatesProduct()
    {
        // Arrange
        var name = "RTUB Album 2023";
        var type = "Album";
        var price = 10.00m;
        var stock = 50;

        // Act
        var result = Product.Create(name, type, price, stock);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be(name);
        result.Type.Should().Be(type);
        result.Price.Should().Be(price);
        result.Stock.Should().Be(stock);
        result.IsAvailable.Should().BeTrue();
        result.IsPublic.Should().BeTrue();
    }

    [Fact]
    public void Create_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var name = "";
        var type = "Album";
        var price = 10.00m;

        // Act & Assert
        var act = () => Product.Create(name, type, price);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*name*");
    }

    [Fact]
    public void Create_WithEmptyType_ThrowsArgumentException()
    {
        // Arrange
        var name = "Album";
        var type = "";
        var price = 10.00m;

        // Act & Assert
        var act = () => Product.Create(name, type, price);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*type*");
    }

    [Fact]
    public void Create_WithNegativePrice_ThrowsArgumentException()
    {
        // Arrange
        var name = "Album";
        var type = "Album";
        var price = -10.00m;

        // Act & Assert
        var act = () => Product.Create(name, type, price);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*price*");
    }

    [Fact]
    public void Create_WithNegativeStock_ThrowsArgumentException()
    {
        // Arrange
        var name = "Album";
        var type = "Album";
        var price = 10.00m;
        var stock = -5;

        // Act & Assert
        var act = () => Product.Create(name, type, price, stock);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*stock*");
    }

    [Fact]
    public void Create_WithNameTooLong_ThrowsArgumentException()
    {
        // Arrange
        var name = new string('A', 201);
        var type = "Album";
        var price = 10.00m;

        // Act & Assert
        var act = () => Product.Create(name, type, price);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*200 caracteres*");
    }

    [Fact]
    public void Update_WithValidData_UpdatesProduct()
    {
        // Arrange
        var product = Product.Create("Album 2023", "Album", 10.00m, 50);
        var newName = "Album 2024";
        var newType = "CD";
        var newPrice = 12.00m;
        var newStock = 30;
        var description = "New album description";

        // Act
        product.Update(newName, newType, newPrice, newStock, description);

        // Assert
        product.Name.Should().Be(newName);
        product.Type.Should().Be(newType);
        product.Price.Should().Be(newPrice);
        product.Stock.Should().Be(newStock);
        product.Description.Should().Be(description);
    }

    [Fact]
    public void Update_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var product = Product.Create("Album 2023", "Album", 10.00m);

        // Act & Assert
        var act = () => product.Update("", "Album", 10.00m, 0);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*name*");
    }

    [Fact]
    public void UpdateStock_WithValidStock_UpdatesStock()
    {
        // Arrange
        var product = Product.Create("Album 2023", "Album", 10.00m, 50);
        var newStock = 25;

        // Act
        product.UpdateStock(newStock);

        // Assert
        product.Stock.Should().Be(newStock);
    }

    [Fact]
    public void UpdateStock_WithNegativeStock_ThrowsArgumentException()
    {
        // Arrange
        var product = Product.Create("Album 2023", "Album", 10.00m, 50);

        // Act & Assert
        var act = () => product.UpdateStock(-10);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*stock*");
    }

    [Fact]
    public void SetAvailability_UpdatesAvailability()
    {
        // Arrange
        var product = Product.Create("Album 2023", "Album", 10.00m, 50);

        // Act
        product.SetAvailability(false);

        // Assert
        product.IsAvailable.Should().BeFalse();

        // Act
        product.SetAvailability(true);

        // Assert
        product.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public void SetPublicVisibility_UpdatesVisibility()
    {
        // Arrange
        var product = Product.Create("Album 2023", "Album", 10.00m, 50);

        // Act
        product.SetPublicVisibility(false);

        // Assert
        product.IsPublic.Should().BeFalse();

        // Act
        product.SetPublicVisibility(true);

        // Assert
        product.IsPublic.Should().BeTrue();
    }

    [Fact]
    public void ImageSrc_WithNoImageUrl_ReturnsEmptyString()
    {
        // Arrange
        var product = Product.Create("Album 2023", "Album", 10.00m);

        // Act
        var imageSrc = product.ImageSrc;

        // Assert - When no ImageUrl is set, ImageSrc should return empty string
        imageSrc.Should().BeEmpty();
    }

    [Fact]
    public void ImageSrc_WithImageUrl_ReturnsImageUrl()
    {
        // Arrange
        var product = Product.Create("Album 2023", "Album", 10.00m);
        var imageUrl = "https://pub-test.r2.dev/rtub/images/products/1.jpg";
        product.ImageUrl = imageUrl;

        // Act
        var imageSrc = product.ImageSrc;

        // Assert - When ImageUrl is set, ImageSrc should return the ImageUrl
        imageSrc.Should().Be(imageUrl);
    }

    [Fact]
    public void CreateEmpty_CreatesProductWithEmptyFields()
    {
        // Act
        var product = Product.CreateEmpty();

        // Assert
        product.Should().NotBeNull();
        product.Name.Should().Be(string.Empty);
        product.Type.Should().Be(string.Empty);
        product.Price.Should().Be(0);
        product.Stock.Should().Be(0);
        product.IsAvailable.Should().BeTrue();
        product.IsPublic.Should().BeTrue();
    }
}
