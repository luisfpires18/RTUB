using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

/// <summary>
/// Unit tests for Trophy entity
/// </summary>
public class TrophyTests
{
    [Fact]
    public void Create_WithValidData_CreatesTrophy()
    {
        // Arrange
        var name = "1º Lugar";
        var eventId = 1;

        // Act
        var result = Trophy.Create(name, eventId);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be(name);
        result.EventId.Should().Be(eventId);
    }

    [Fact]
    public void Create_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var name = "";
        var eventId = 1;

        // Act & Assert
        var act = () => Trophy.Create(name, eventId);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*nome*");
    }

    [Fact]
    public void Create_WithWhitespaceName_ThrowsArgumentException()
    {
        // Arrange
        var name = "   ";
        var eventId = 1;

        // Act & Assert
        var act = () => Trophy.Create(name, eventId);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*nome*");
    }

    [Fact]
    public void Create_WithNullName_ThrowsArgumentException()
    {
        // Arrange
        string? name = null;
        var eventId = 1;

        // Act & Assert
        var act = () => Trophy.Create(name!, eventId);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*nome*");
    }

    [Fact]
    public void Create_WithNameTooLong_ThrowsArgumentException()
    {
        // Arrange
        var name = new string('A', 201);
        var eventId = 1;

        // Act & Assert
        var act = () => Trophy.Create(name, eventId);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*200 caracteres*");
    }

    [Fact]
    public void Create_WithInvalidEventId_ThrowsArgumentException()
    {
        // Arrange
        var name = "Melhor Apresentação";
        var eventId = 0;

        // Act & Assert
        var act = () => Trophy.Create(name, eventId);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*evento*");
    }

    [Fact]
    public void Create_WithNegativeEventId_ThrowsArgumentException()
    {
        // Arrange
        var name = "Melhor Apresentação";
        var eventId = -1;

        // Act & Assert
        var act = () => Trophy.Create(name, eventId);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*evento*");
    }

    [Fact]
    public void Update_WithValidName_UpdatesName()
    {
        // Arrange
        var trophy = Trophy.Create("1º Lugar", 1);
        var newName = "2º Lugar";

        // Act
        trophy.Update(newName);

        // Assert
        trophy.Name.Should().Be(newName);
    }

    [Fact]
    public void Update_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var trophy = Trophy.Create("1º Lugar", 1);

        // Act & Assert
        var act = () => trophy.Update("");
        act.Should().Throw<ArgumentException>()
            .WithMessage("*nome*");
    }

    [Fact]
    public void Update_WithWhitespaceName_ThrowsArgumentException()
    {
        // Arrange
        var trophy = Trophy.Create("1º Lugar", 1);

        // Act & Assert
        var act = () => trophy.Update("   ");
        act.Should().Throw<ArgumentException>()
            .WithMessage("*nome*");
    }

    [Fact]
    public void Update_WithNameTooLong_ThrowsArgumentException()
    {
        // Arrange
        var trophy = Trophy.Create("1º Lugar", 1);
        var tooLongName = new string('A', 201);

        // Act & Assert
        var act = () => trophy.Update(tooLongName);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*200 caracteres*");
    }

    [Fact]
    public void Constructor_WithEventId_CreatesTrophy()
    {
        // Arrange
        var eventId = 5;

        // Act
        var trophy = new Trophy(eventId);

        // Assert
        trophy.Should().NotBeNull();
        trophy.EventId.Should().Be(eventId);
        trophy.Name.Should().BeEmpty();
    }
}
