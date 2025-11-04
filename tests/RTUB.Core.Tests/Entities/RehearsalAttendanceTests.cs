using FluentAssertions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Core.Tests.Entities;

/// <summary>
/// Unit tests for RehearsalAttendance entity
/// Tests domain logic and entity behavior
/// </summary>
public class RehearsalAttendanceTests
{
    [Fact]
    public void Create_WithValidData_CreatesAttendance()
    {
        // Arrange
        var rehearsalId = 1;
        var userId = "user123";
        var instrument = InstrumentType.Guitarra;

        // Act
        var result = RehearsalAttendance.Create(rehearsalId, userId, instrument);

        // Assert
        result.Should().NotBeNull();
        result.RehearsalId.Should().Be(rehearsalId);
        result.UserId.Should().Be(userId);
        result.Instrument.Should().Be(instrument);
        result.Attended.Should().BeFalse(); // Default is false (pending approval)
    }

    [Fact]
    public void Create_WithEmptyUserId_ThrowsArgumentException()
    {
        // Arrange
        var rehearsalId = 1;
        var userId = "";

        // Act & Assert
        var act = () => RehearsalAttendance.Create(rehearsalId, userId);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*utilizador*");
    }

    [Fact]
    public void Create_WithNullUserId_ThrowsArgumentException()
    {
        // Arrange
        var rehearsalId = 1;
        string? userId = null;

        // Act & Assert
        var act = () => RehearsalAttendance.Create(rehearsalId, userId!);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*utilizador*");
    }

    [Fact]
    public void Create_WithZeroRehearsalId_ThrowsArgumentException()
    {
        // Arrange
        var rehearsalId = 0;
        var userId = "user123";

        // Act & Assert
        var act = () => RehearsalAttendance.Create(rehearsalId, userId);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*ensaio*");
    }

    [Fact]
    public void Create_WithNegativeRehearsalId_ThrowsArgumentException()
    {
        // Arrange
        var rehearsalId = -1;
        var userId = "user123";

        // Act & Assert
        var act = () => RehearsalAttendance.Create(rehearsalId, userId);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*ensaio*");
    }

    [Fact]
    public void Create_WithoutInstrument_SetsInstrumentToNull()
    {
        // Arrange
        var rehearsalId = 1;
        var userId = "user123";

        // Act
        var result = RehearsalAttendance.Create(rehearsalId, userId);

        // Assert
        result.Instrument.Should().BeNull();
    }

    [Fact]
    public void MarkAttendance_WithTrue_SetsAttendedToTrue()
    {
        // Arrange
        var attendance = RehearsalAttendance.Create(1, "user123");

        // Act
        attendance.MarkAttendance(true);

        // Assert
        attendance.Attended.Should().BeTrue();
    }

    [Fact]
    public void MarkAttendance_WithFalse_SetsAttendedToFalse()
    {
        // Arrange
        var attendance = RehearsalAttendance.Create(1, "user123");

        // Act
        attendance.MarkAttendance(false);

        // Assert
        attendance.Attended.Should().BeFalse();
    }

    [Fact]
    public void UpdateInstrument_ChangesInstrument()
    {
        // Arrange
        var attendance = RehearsalAttendance.Create(1, "user123", InstrumentType.Guitarra);

        // Act
        attendance.UpdateInstrument(InstrumentType.Bandolim);

        // Assert
        attendance.Instrument.Should().Be(InstrumentType.Bandolim);
    }

    [Fact]
    public void UpdateInstrument_WithNull_SetsInstrumentToNull()
    {
        // Arrange
        var attendance = RehearsalAttendance.Create(1, "user123", InstrumentType.Guitarra);

        // Act
        attendance.UpdateInstrument(null);

        // Assert
        attendance.Instrument.Should().BeNull();
    }

    [Fact]
    public void CheckedInAt_IsSetOnCreation()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var attendance = RehearsalAttendance.Create(1, "user123");

        // Assert
        var after = DateTime.UtcNow;
        attendance.CheckedInAt.Should().BeOnOrAfter(before);
        attendance.CheckedInAt.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void DefaultAttended_IsFalse()
    {
        // Arrange & Act
        var attendance = RehearsalAttendance.Create(1, "user123");

        // Assert
        attendance.Attended.Should().BeFalse();
    }
}
