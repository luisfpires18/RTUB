using FluentAssertions;
using RTUB.Core.Attributes;
using RTUB.Core.Entities;
using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Tests.Attributes;

/// <summary>
/// Unit tests for DateGreaterThanAttribute
/// </summary>
public class DateGreaterThanAttributeTests
{
    private class TestModel
    {
        public DateTime StartDate { get; set; }

        [DateGreaterThan(nameof(StartDate), ErrorMessage = "End date must be after start date")]
        public DateTime? EndDate { get; set; }
    }

    [Fact]
    public void DateGreaterThan_WithEndDateAfterStartDate_IsValid()
    {
        // Arrange
        var model = new TestModel
        {
            StartDate = DateTime.Now,
            EndDate = DateTime.Now.AddDays(1)
        };
        var context = new ValidationContext(model) { MemberName = nameof(TestModel.EndDate) };
        var attribute = new DateGreaterThanAttribute(nameof(TestModel.StartDate));

        // Act
        var result = attribute.GetValidationResult(model.EndDate, context);

        // Assert
        result.Should().Be(ValidationResult.Success);
    }

    [Fact]
    public void DateGreaterThan_WithEndDateBeforeStartDate_IsInvalid()
    {
        // Arrange
        var model = new TestModel
        {
            StartDate = DateTime.Now,
            EndDate = DateTime.Now.AddDays(-1)
        };
        var context = new ValidationContext(model) { MemberName = nameof(TestModel.EndDate) };
        var attribute = new DateGreaterThanAttribute(nameof(TestModel.StartDate))
        {
            ErrorMessage = "End date must be after start date"
        };

        // Act
        var result = attribute.GetValidationResult(model.EndDate, context);

        // Assert
        result.Should().NotBe(ValidationResult.Success);
        result?.ErrorMessage.Should().Be("End date must be after start date");
    }

    [Fact]
    public void DateGreaterThan_WithEndDateEqualToStartDate_IsValid()
    {
        // Arrange
        var now = DateTime.Now;
        var model = new TestModel
        {
            StartDate = now,
            EndDate = now
        };
        var context = new ValidationContext(model) { MemberName = nameof(TestModel.EndDate) };
        var attribute = new DateGreaterThanAttribute(nameof(TestModel.StartDate));

        // Act
        var result = attribute.GetValidationResult(model.EndDate, context);

        // Assert
        result.Should().Be(ValidationResult.Success);
    }

    [Fact]
    public void DateGreaterThan_WithNullEndDate_IsValid()
    {
        // Arrange
        var model = new TestModel
        {
            StartDate = DateTime.Now,
            EndDate = null
        };
        var context = new ValidationContext(model) { MemberName = nameof(TestModel.EndDate) };
        var attribute = new DateGreaterThanAttribute(nameof(TestModel.StartDate));

        // Act
        var result = attribute.GetValidationResult(model.EndDate, context);

        // Assert
        result.Should().Be(ValidationResult.Success);
    }

    [Fact]
    public void DateGreaterThan_ValidatesEventEntity()
    {
        // Arrange
        var eventEntity = Event.Create("Test Event", DateTime.Now, "Location", Core.Enums.EventType.Festival);
        
        // Act
        eventEntity.SetEndDate(DateTime.Now.AddDays(2));

        // Assert - Should not throw
        eventEntity.EndDate.Should().NotBeNull();
    }
}
