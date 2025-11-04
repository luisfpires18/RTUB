using FluentAssertions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Core.Tests.Entities;

/// <summary>
/// Unit tests for Request entity
/// </summary>
public class RequestTests
{
    [Fact]
    public void Create_WithValidData_CreatesRequest()
    {
        // Arrange
        var name = "John Doe";
        var email = "john@example.com";
        var phone = "123456789";
        var eventType = "Wedding";
        var preferredDate = DateTime.Now.AddDays(30);
        var location = "Test Venue";
        var message = "Looking for a performance";

        // Act
        var result = Request.Create(name, email, phone, eventType, preferredDate, location, message);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be(name);
        result.Email.Should().Be(email);
        result.Phone.Should().Be(phone);
        result.EventType.Should().Be(eventType);
        result.PreferredDate.Should().Be(preferredDate);
        result.Location.Should().Be(location);
        result.Message.Should().Be(message);
        result.Status.Should().Be(RequestStatus.Pending);
    }

    [Theory]
    [InlineData("", "email@test.com", "123456", "Wedding", "Test message")]
    [InlineData("John", "", "123456", "Wedding", "Test message")]
    [InlineData("John", "email@test.com", "", "Wedding", "Test message")]
    [InlineData("John", "email@test.com", "123456", "", "Test message")]
    public void Create_WithInvalidData_ThrowsArgumentException(string name, string email, string phone, string eventType, string message)
    {
        // Arrange
        var preferredDate = DateTime.Now.AddDays(30);
        var location = "Test Venue";

        // Act & Assert
        var act = () => Request.Create(name, email, phone, eventType, preferredDate, location, message);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetDateRange_WithValidEndDate_SetsDateRange()
    {
        // Arrange
        var request = Request.Create("John", "john@test.com", "123456", "Wedding", 
            DateTime.Now.AddDays(30), "Venue", "Message");
        var endDate = DateTime.Now.AddDays(32);

        // Act
        request.SetDateRange(endDate);

        // Assert
        request.PreferredEndDate.Should().Be(endDate);
        request.IsDateRange.Should().BeTrue();
    }

    [Fact]
    public void SetDateRange_WithEndDateBeforeStart_ThrowsArgumentException()
    {
        // Arrange
        var startDate = DateTime.Now.AddDays(30);
        var request = Request.Create("John", "john@test.com", "123456", "Wedding", 
            startDate, "Venue", "Message");
        var endDate = startDate.AddDays(-1);

        // Act & Assert
        var act = () => request.SetDateRange(endDate);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*fim não pode ser anterior*");
    }

    [Fact]
    public void UpdateStatus_ChangesStatus()
    {
        // Arrange
        var request = Request.Create("John", "john@test.com", "123456", "Wedding", 
            DateTime.Now.AddDays(30), "Venue", "Message");

        // Act
        request.UpdateStatus(RequestStatus.Analysing);

        // Assert
        request.Status.Should().Be(RequestStatus.Analysing);
    }

    [Fact]
    public void UpdateStatus_CanSetToConfirmed()
    {
        // Arrange
        var request = Request.Create("John", "john@test.com", "123456", "Wedding", 
            DateTime.Now.AddDays(30), "Venue", "Message");

        // Act
        request.UpdateStatus(RequestStatus.Confirmed);

        // Assert
        request.Status.Should().Be(RequestStatus.Confirmed);
    }

    [Fact]
    public void UpdateStatus_CanSetToRejected()
    {
        // Arrange
        var request = Request.Create("John", "john@test.com", "123456", "Wedding", 
            DateTime.Now.AddDays(30), "Venue", "Message");

        // Act
        request.UpdateStatus(RequestStatus.Rejected);

        // Assert
        request.Status.Should().Be(RequestStatus.Rejected);
    }
}
