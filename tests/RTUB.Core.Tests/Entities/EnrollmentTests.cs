using FluentAssertions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Core.Tests.Entities;

/// <summary>
/// Unit tests for Enrollment entity
/// </summary>
public class EnrollmentTests
{
    [Fact]
    public void Create_WithValidData_CreatesEnrollment()
    {
        // Arrange
        var userId = "user123";
        var eventId = 1;
        var attended = true;

        // Act
        var result = Enrollment.Create(userId, eventId, attended);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(userId);
        result.EventId.Should().Be(eventId);
        result.Attended.Should().Be(attended);
        result.EnrolledAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_WithEmptyUserId_ThrowsArgumentException()
    {
        // Arrange
        var userId = "";
        var eventId = 1;

        // Act & Assert
        var act = () => Enrollment.Create(userId, eventId);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*utilizador*");
    }

    [Fact]
    public void MarkAttendance_UpdatesAttendance()
    {
        // Arrange
        var enrollment = Enrollment.Create("user123", 1, false);

        // Act
        enrollment.MarkAttendance(true);

        // Assert
        enrollment.Attended.Should().BeTrue();
    }

    [Fact]
    public void Create_DefaultAttendance_IsFalse()
    {
        // Arrange & Act
        var enrollment = Enrollment.Create("user123", 1);

        // Assert
        enrollment.Attended.Should().BeFalse();
    }

    [Fact]
    public void Enrollment_CanHaveInstrument()
    {
        // Arrange
        var enrollment = Enrollment.Create("user123", 1);

        // Act
        enrollment.Instrument = InstrumentType.Guitarra;

        // Assert
        enrollment.Instrument.Should().Be(InstrumentType.Guitarra);
    }

    [Fact]
    public void Enrollment_CanHaveNotes()
    {
        // Arrange
        var enrollment = Enrollment.Create("user123", 1);
        var notes = "Special requirements";

        // Act
        enrollment.Notes = notes;

        // Assert
        enrollment.Notes.Should().Be(notes);
    }
}
