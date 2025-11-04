using FluentAssertions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Core.Tests.Entities;

public class RoleAssignmentTests
{
    [Fact]
    public void Create_WithValidData_CreatesRoleAssignment()
    {
        // Arrange
        var userId = "user123";
        var position = Position.Magister;
        var startYear = 2023;
        var endYear = 2024;

        // Act
        var roleAssignment = RoleAssignment.Create(userId, position, startYear, endYear);

        // Assert
        roleAssignment.Should().NotBeNull();
        roleAssignment.UserId.Should().Be(userId);
        roleAssignment.Position.Should().Be(position);
        roleAssignment.StartYear.Should().Be(startYear);
        roleAssignment.EndYear.Should().Be(endYear);
        roleAssignment.Notes.Should().BeNull();
    }

    [Fact]
    public void Create_WithNotesAndCreatedBy_AssignsAllFields()
    {
        // Arrange
        var userId = "user123";
        var position = Position.Secretario;
        var notes = "Special assignment";
        var createdBy = "admin1";

        // Act
        var roleAssignment = RoleAssignment.Create(userId, position, 2023, 2024, notes, createdBy);

        // Assert
        roleAssignment.Notes.Should().Be(notes);
        roleAssignment.CreatedBy.Should().Be(createdBy);
    }

    [Theory]
    [InlineData(Position.Magister)]
    [InlineData(Position.ViceMagister)]
    [InlineData(Position.Secretario)]
    [InlineData(Position.PrimeiroTesoureiro)]
    [InlineData(Position.SegundoTesoureiro)]
    public void Create_WithDifferentPositions_CreatesRoleAssignment(Position position)
    {
        // Act
        var roleAssignment = RoleAssignment.Create("user123", position, 2023, 2024);

        // Assert
        roleAssignment.Position.Should().Be(position);
    }

    [Fact]
    public void Create_WithEmptyUserId_ThrowsArgumentException()
    {
        // Act
        var act = () => RoleAssignment.Create("", Position.Magister, 2023, 2024);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*User ID cannot be empty*");
    }

    [Fact]
    public void Create_WithWhitespaceUserId_ThrowsArgumentException()
    {
        // Act
        var act = () => RoleAssignment.Create("   ", Position.Magister, 2023, 2024);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*User ID cannot be empty*");
    }

    [Fact]
    public void Create_WithSameYears_ThrowsArgumentException()
    {
        // Act
        var act = () => RoleAssignment.Create("user123", Position.Magister, 2023, 2023);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*End year must be after start year*");
    }

    [Fact]
    public void Create_WithEndYearBeforeStartYear_ThrowsArgumentException()
    {
        // Act
        var act = () => RoleAssignment.Create("user123", Position.Magister, 2024, 2023);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*End year must be after start year*");
    }

    [Fact]
    public void UpdateDetails_WithValidData_UpdatesRoleAssignment()
    {
        // Arrange
        var roleAssignment = RoleAssignment.Create("user123", Position.Magister, 2023, 2024);
        var newPosition = Position.Secretario;
        var newNotes = "Updated notes";

        // Act
        roleAssignment.UpdateDetails(newPosition, 2024, 2025, newNotes);

        // Assert
        roleAssignment.Position.Should().Be(newPosition);
        roleAssignment.StartYear.Should().Be(2024);
        roleAssignment.EndYear.Should().Be(2025);
        roleAssignment.Notes.Should().Be(newNotes);
    }

    [Fact]
    public void UpdateDetails_WithInvalidYears_ThrowsArgumentException()
    {
        // Arrange
        var roleAssignment = RoleAssignment.Create("user123", Position.Magister, 2023, 2024);

        // Act
        var act = () => roleAssignment.UpdateDetails(Position.Secretario, 2024, 2024, null);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*End year must be after start year*");
    }

    [Fact]
    public void GetFiscalYear_ReturnsCorrectFormat()
    {
        // Arrange
        var roleAssignment = RoleAssignment.Create("user123", Position.Magister, 2023, 2024);

        // Act
        var result = roleAssignment.GetFiscalYear();

        // Assert
        result.Should().Be("2023-2024");
    }

    [Fact]
    public void FiscalYear_Property_ReturnsCorrectFormat()
    {
        // Arrange
        var roleAssignment = RoleAssignment.Create("user123", Position.Magister, 2023, 2024);

        // Act
        var result = roleAssignment.FiscalYear;

        // Assert
        result.Should().Be("2023-2024");
    }

    [Theory]
    [InlineData(2020, 2021, "2020-2021")]
    [InlineData(2021, 2022, "2021-2022")]
    [InlineData(2024, 2025, "2024-2025")]
    public void GetFiscalYear_WithDifferentYears_ReturnsCorrectFormat(int start, int end, string expected)
    {
        // Arrange
        var roleAssignment = RoleAssignment.Create("user123", Position.Magister, start, end);

        // Act
        var result = roleAssignment.GetFiscalYear();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void RoleAssignment_Properties_CanBeSet()
    {
        // Arrange & Act
        var roleAssignment = new RoleAssignment
        {
            UserId = "user123",
            Position = Position.Magister,
            StartYear = 2023,
            EndYear = 2024,
            Notes = "Test notes"
        };

        // Assert
        roleAssignment.UserId.Should().Be("user123");
        roleAssignment.Notes.Should().Be("Test notes");
    }

    [Fact]
    public void RoleAssignment_InheritsFromBaseEntity()
    {
        // Arrange & Act
        var roleAssignment = RoleAssignment.Create("user123", Position.Magister, 2023, 2024);

        // Assert
        roleAssignment.Should().BeAssignableTo<BaseEntity>();
    }
}
