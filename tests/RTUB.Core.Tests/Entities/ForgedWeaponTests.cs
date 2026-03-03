using FluentAssertions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Core.Tests.Entities;

public class ForgedWeaponTests
{
    #region Rename Tests

    [Fact]
    public void Rename_WithValidName_ShouldUpdateName()
    {
        // Arrange
        var weapon = ForgedWeapon.Create(
            "user-1", "Guitarra Original", WeaponType.SwordOneHand,
            InventoryItemType.GuitarraPart, InventoryItemType.Cerveja);

        // Act
        weapon.Rename("Guitarra Renomeada");

        // Assert
        weapon.Name.Should().Be("Guitarra Renomeada");
    }

    [Fact]
    public void Rename_WithWhitespacePadding_ShouldTrimName()
    {
        // Arrange
        var weapon = ForgedWeapon.Create(
            "user-1", "Original", WeaponType.SwordOneHand,
            InventoryItemType.GuitarraPart, InventoryItemType.Cerveja);

        // Act
        weapon.Rename("  Novo Nome  ");

        // Assert
        weapon.Name.Should().Be("Novo Nome");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Rename_WithEmptyOrWhitespace_ShouldThrowArgumentException(string? newName)
    {
        // Arrange
        var weapon = ForgedWeapon.Create(
            "user-1", "Original", WeaponType.SwordOneHand,
            InventoryItemType.GuitarraPart, InventoryItemType.Cerveja);

        // Act
        var act = () => weapon.Rename(newName!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("newName");
    }

    [Fact]
    public void Rename_WithNameExceeding100Characters_ShouldThrowArgumentException()
    {
        // Arrange
        var weapon = ForgedWeapon.Create(
            "user-1", "Original", WeaponType.SwordOneHand,
            InventoryItemType.GuitarraPart, InventoryItemType.Cerveja);

        var longName = new string('A', 101);

        // Act
        var act = () => weapon.Rename(longName);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("newName");
    }

    [Fact]
    public void Rename_WithExactly100Characters_ShouldSucceed()
    {
        // Arrange
        var weapon = ForgedWeapon.Create(
            "user-1", "Original", WeaponType.SwordOneHand,
            InventoryItemType.GuitarraPart, InventoryItemType.Cerveja);

        var exactName = new string('A', 100);

        // Act
        weapon.Rename(exactName);

        // Assert
        weapon.Name.Should().Be(exactName);
    }

    #endregion
}
