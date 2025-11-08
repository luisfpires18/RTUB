using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

/// <summary>
/// Unit tests for ProductReservation entity
/// </summary>
public class ProductReservationTests
{
    [Fact]
    public void Create_WithValidData_CreatesReservation()
    {
        // Arrange
        var productId = 1;
        var userId = "user123";
        var userNickname = "TestUser";
        var hasSizes = true;
        var size = "M";
        var displayName = "Test Display Name";

        // Act
        var result = ProductReservation.Create(productId, userId, userNickname, hasSizes, size, displayName);

        // Assert
        result.Should().NotBeNull();
        result.ProductId.Should().Be(productId);
        result.UserId.Should().Be(userId);
        result.UserNickname.Should().Be(userNickname);
        result.HasSizes.Should().BeTrue();
        result.Size.Should().Be(size);
        result.DisplayName.Should().Be(displayName);
    }

    [Fact]
    public void Create_WithoutSize_CreatesReservation()
    {
        // Arrange
        var productId = 1;
        var userId = "user123";
        var userNickname = "TestUser";
        var hasSizes = false;

        // Act
        var result = ProductReservation.Create(productId, userId, userNickname, hasSizes);

        // Assert
        result.Should().NotBeNull();
        result.ProductId.Should().Be(productId);
        result.UserId.Should().Be(userId);
        result.UserNickname.Should().Be(userNickname);
        result.HasSizes.Should().BeFalse();
        result.Size.Should().BeNull();
    }

    [Fact]
    public void Create_WithInvalidProductId_ThrowsArgumentException()
    {
        // Arrange
        var productId = 0;
        var userId = "user123";
        var userNickname = "TestUser";

        // Act & Assert
        var act = () => ProductReservation.Create(productId, userId, userNickname, false);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*produto*");
    }

    [Fact]
    public void Create_WithEmptyUserId_ThrowsArgumentException()
    {
        // Arrange
        var productId = 1;
        var userId = "";
        var userNickname = "TestUser";

        // Act & Assert
        var act = () => ProductReservation.Create(productId, userId, userNickname, false);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*utilizador*");
    }

    [Fact]
    public void Create_WithEmptyUserNickname_ThrowsArgumentException()
    {
        // Arrange
        var productId = 1;
        var userId = "user123";
        var userNickname = "";

        // Act & Assert
        var act = () => ProductReservation.Create(productId, userId, userNickname, false);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*nickname*");
    }

    [Fact]
    public void Create_WithHasSizesButNoSize_ThrowsArgumentException()
    {
        // Arrange
        var productId = 1;
        var userId = "user123";
        var userNickname = "TestUser";
        var hasSizes = true;
        string? size = null;

        // Act & Assert
        var act = () => ProductReservation.Create(productId, userId, userNickname, hasSizes, size);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*tamanho*");
    }
}
