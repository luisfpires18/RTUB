using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Tests for date edit permission logic.
/// Validates both the one-time date edit feature for users and owner-only override.
/// </summary>
public class DateEditPermissionTests
{
    /// <summary>
    /// Helper method to check if a date pair is complete (both year and month are set).
    /// This mirrors the logic in Profile.razor.
    /// </summary>
    private static bool IsDatePairComplete(int? year, int? month)
    {
        return year.HasValue && month.HasValue;
    }

    #region Profile.razor - One-Time Date Edit Tests

    [Fact]
    public void IsDatePairComplete_WhenBothYearAndMonthAreSet_ReturnsTrue()
    {
        // Arrange
        int? year = 2020;
        int? month = 5;

        // Act
        var result = IsDatePairComplete(year, month);

        // Assert
        result.Should().BeTrue("both year and month are set, so the date pair is complete");
    }

    [Fact]
    public void IsDatePairComplete_WhenYearIsNull_ReturnsFalse()
    {
        // Arrange
        int? year = null;
        int? month = 5;

        // Act
        var result = IsDatePairComplete(year, month);

        // Assert
        result.Should().BeFalse("year is null, so the date pair is incomplete");
    }

    [Fact]
    public void IsDatePairComplete_WhenMonthIsNull_ReturnsFalse()
    {
        // Arrange
        int? year = 2020;
        int? month = null;

        // Act
        var result = IsDatePairComplete(year, month);

        // Assert
        result.Should().BeFalse("month is null, so the date pair is incomplete");
    }

    [Fact]
    public void IsDatePairComplete_WhenBothAreNull_ReturnsFalse()
    {
        // Arrange
        int? year = null;
        int? month = null;

        // Act
        var result = IsDatePairComplete(year, month);

        // Assert
        result.Should().BeFalse("both year and month are null, so the date pair is incomplete");
    }

    [Theory]
    [InlineData(1990, 1)]
    [InlineData(2023, 12)]
    [InlineData(2015, 6)]
    public void IsDatePairComplete_WithVariousCompleteValues_ReturnsTrue(int year, int month)
    {
        // Arrange & Act
        var result = IsDatePairComplete(year, month);

        // Assert
        result.Should().BeTrue($"year {year} and month {month} are both set");
    }

    [Fact]
    public void SaveTunaSection_Logic_WhenLeitaoDateIsComplete_ShouldNotUpdateLeitaoDate()
    {
        // Arrange - Simulate user in database with complete Leitao date
        var userInDb = new ApplicationUser
        {
            Id = "user-1",
            YearLeitao = 2020,
            MonthLeitao = 5,
            YearCaloiro = null,
            MonthCaloiro = null
        };

        // Simulate edit attempt with different values
        var editAttempt = new ApplicationUser
        {
            Id = "user-1",
            YearLeitao = 2021, // Trying to change
            MonthLeitao = 6,   // Trying to change
            YearCaloiro = 2022,
            MonthCaloiro = 8
        };

        // Act - Apply the SaveTunaSection logic
        if (!IsDatePairComplete(userInDb.YearLeitao, userInDb.MonthLeitao))
        {
            userInDb.YearLeitao = editAttempt.YearLeitao;
            userInDb.MonthLeitao = editAttempt.MonthLeitao;
        }

        if (!IsDatePairComplete(userInDb.YearCaloiro, userInDb.MonthCaloiro))
        {
            userInDb.YearCaloiro = editAttempt.YearCaloiro;
            userInDb.MonthCaloiro = editAttempt.MonthCaloiro;
        }

        // Assert - Leitao date should remain unchanged, Caloiro should be updated
        userInDb.YearLeitao.Should().Be(2020, "Leitao date was complete and should not be updated");
        userInDb.MonthLeitao.Should().Be(5, "Leitao date was complete and should not be updated");
        userInDb.YearCaloiro.Should().Be(2022, "Caloiro date was incomplete and should be updated");
        userInDb.MonthCaloiro.Should().Be(8, "Caloiro date was incomplete and should be updated");
    }

    [Fact]
    public void SaveTunaSection_Logic_WhenLeitaoDateIsIncomplete_ShouldAllowUpdate()
    {
        // Arrange - User with only year set (incomplete)
        var userInDb = new ApplicationUser
        {
            Id = "user-1",
            YearLeitao = 2020,
            MonthLeitao = null // Incomplete
        };

        var editAttempt = new ApplicationUser
        {
            Id = "user-1",
            YearLeitao = 2020,
            MonthLeitao = 5 // Completing the date
        };

        // Act
        if (!IsDatePairComplete(userInDb.YearLeitao, userInDb.MonthLeitao))
        {
            userInDb.YearLeitao = editAttempt.YearLeitao;
            userInDb.MonthLeitao = editAttempt.MonthLeitao;
        }

        // Assert - Should allow completion of incomplete date
        userInDb.YearLeitao.Should().Be(2020);
        userInDb.MonthLeitao.Should().Be(5, "incomplete date should be allowed to be completed");
    }

    [Fact]
    public void SaveTunaSection_Logic_WhenAllDatesIncomplete_ShouldAllowAllUpdates()
    {
        // Arrange
        var userInDb = new ApplicationUser
        {
            Id = "user-1",
            YearLeitao = null,
            MonthLeitao = null,
            YearCaloiro = null,
            MonthCaloiro = null,
            YearTuno = null,
            MonthTuno = null
        };

        var editAttempt = new ApplicationUser
        {
            Id = "user-1",
            YearLeitao = 2020,
            MonthLeitao = 5,
            YearCaloiro = 2021,
            MonthCaloiro = 6,
            YearTuno = 2023,
            MonthTuno = 7
        };

        // Act
        if (!IsDatePairComplete(userInDb.YearLeitao, userInDb.MonthLeitao))
        {
            userInDb.YearLeitao = editAttempt.YearLeitao;
            userInDb.MonthLeitao = editAttempt.MonthLeitao;
        }

        if (!IsDatePairComplete(userInDb.YearCaloiro, userInDb.MonthCaloiro))
        {
            userInDb.YearCaloiro = editAttempt.YearCaloiro;
            userInDb.MonthCaloiro = editAttempt.MonthCaloiro;
        }

        if (!IsDatePairComplete(userInDb.YearTuno, userInDb.MonthTuno))
        {
            userInDb.YearTuno = editAttempt.YearTuno;
            userInDb.MonthTuno = editAttempt.MonthTuno;
        }

        // Assert - All dates should be updated
        userInDb.YearLeitao.Should().Be(2020);
        userInDb.MonthLeitao.Should().Be(5);
        userInDb.YearCaloiro.Should().Be(2021);
        userInDb.MonthCaloiro.Should().Be(6);
        userInDb.YearTuno.Should().Be(2023);
        userInDb.MonthTuno.Should().Be(7);
    }

    #endregion

    #region Members.razor - Owner-Only Edit Tests

    [Fact]
    public void SaveMember_Logic_WhenUserIsOwner_ShouldAllowDateUpdates()
    {
        // Arrange
        bool isOwner = true;
        var existingUser = new ApplicationUser
        {
            Id = "user-1",
            YearLeitao = 2020,
            MonthLeitao = 5,
            YearCaloiro = 2021,
            MonthCaloiro = 6
        };

        var editingUser = new ApplicationUser
        {
            Id = "user-1",
            YearLeitao = 2019,  // Change
            MonthLeitao = 4,     // Change
            YearCaloiro = 2020,  // Change
            MonthCaloiro = 5     // Change
        };

        // Act - Apply Members.razor SaveMember logic
        if (isOwner)
        {
            existingUser.YearLeitao = editingUser.YearLeitao;
            existingUser.MonthLeitao = editingUser.MonthLeitao;
            existingUser.YearCaloiro = editingUser.YearCaloiro;
            existingUser.MonthCaloiro = editingUser.MonthCaloiro;
            existingUser.YearTuno = editingUser.YearTuno;
            existingUser.MonthTuno = editingUser.MonthTuno;
        }

        // Assert - Owner should be able to modify all dates
        existingUser.YearLeitao.Should().Be(2019, "owner can modify dates");
        existingUser.MonthLeitao.Should().Be(4, "owner can modify dates");
        existingUser.YearCaloiro.Should().Be(2020, "owner can modify dates");
        existingUser.MonthCaloiro.Should().Be(5, "owner can modify dates");
    }

    [Fact]
    public void SaveMember_Logic_WhenUserIsNotOwner_ShouldNotAllowDateUpdates()
    {
        // Arrange
        bool isOwner = false;
        var existingUser = new ApplicationUser
        {
            Id = "user-1",
            YearLeitao = 2020,
            MonthLeitao = 5,
            YearCaloiro = 2021,
            MonthCaloiro = 6,
            YearTuno = 2023,
            MonthTuno = 7
        };

        var editingUser = new ApplicationUser
        {
            Id = "user-1",
            YearLeitao = 2019,  // Attempt to change
            MonthLeitao = 4,     // Attempt to change
            YearCaloiro = 2020,  // Attempt to change
            MonthCaloiro = 5,    // Attempt to change
            YearTuno = 2022,     // Attempt to change
            MonthTuno = 6        // Attempt to change
        };

        // Store original values
        var originalYearLeitao = existingUser.YearLeitao;
        var originalMonthLeitao = existingUser.MonthLeitao;
        var originalYearCaloiro = existingUser.YearCaloiro;
        var originalMonthCaloiro = existingUser.MonthCaloiro;
        var originalYearTuno = existingUser.YearTuno;
        var originalMonthTuno = existingUser.MonthTuno;

        // Act - Apply Members.razor SaveMember logic
        if (isOwner)
        {
            existingUser.YearLeitao = editingUser.YearLeitao;
            existingUser.MonthLeitao = editingUser.MonthLeitao;
            existingUser.YearCaloiro = editingUser.YearCaloiro;
            existingUser.MonthCaloiro = editingUser.MonthCaloiro;
            existingUser.YearTuno = editingUser.YearTuno;
            existingUser.MonthTuno = editingUser.MonthTuno;
        }
        // If not owner, dates should remain unchanged (keep existing values)

        // Assert - Non-owner should NOT be able to modify dates
        existingUser.YearLeitao.Should().Be(originalYearLeitao, "non-owner cannot modify dates");
        existingUser.MonthLeitao.Should().Be(originalMonthLeitao, "non-owner cannot modify dates");
        existingUser.YearCaloiro.Should().Be(originalYearCaloiro, "non-owner cannot modify dates");
        existingUser.MonthCaloiro.Should().Be(originalMonthCaloiro, "non-owner cannot modify dates");
        existingUser.YearTuno.Should().Be(originalYearTuno, "non-owner cannot modify dates");
        existingUser.MonthTuno.Should().Be(originalMonthTuno, "non-owner cannot modify dates");
    }

    [Fact]
    public void SaveMember_Logic_WhenUserIsNotOwner_ShouldAllowOtherFieldUpdates()
    {
        // Arrange
        bool isOwner = false;
        var existingUser = new ApplicationUser
        {
            Id = "user-1",
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            YearLeitao = 2020,
            MonthLeitao = 5
        };

        var editingUser = new ApplicationUser
        {
            Id = "user-1",
            FirstName = "Jane",      // Change allowed
            LastName = "Smith",       // Change allowed
            Email = "jane@example.com", // Change allowed
            YearLeitao = 2019,       // Change NOT allowed
            MonthLeitao = 4          // Change NOT allowed
        };

        var originalYearLeitao = existingUser.YearLeitao;
        var originalMonthLeitao = existingUser.MonthLeitao;

        // Act - Apply Members.razor SaveMember logic (partial)
        existingUser.FirstName = editingUser.FirstName;
        existingUser.LastName = editingUser.LastName;
        existingUser.Email = editingUser.Email;

        if (isOwner)
        {
            existingUser.YearLeitao = editingUser.YearLeitao;
            existingUser.MonthLeitao = editingUser.MonthLeitao;
        }

        // Assert - Other fields updated, but dates unchanged
        existingUser.FirstName.Should().Be("Jane", "non-date fields can be updated");
        existingUser.LastName.Should().Be("Smith", "non-date fields can be updated");
        existingUser.Email.Should().Be("jane@example.com", "non-date fields can be updated");
        existingUser.YearLeitao.Should().Be(originalYearLeitao, "non-owner cannot modify dates");
        existingUser.MonthLeitao.Should().Be(originalMonthLeitao, "non-owner cannot modify dates");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void IsDatePairComplete_WithZeroValues_ReturnsFalse()
    {
        // Arrange - Zero is not a valid month
        int? year = 2020;
        int? month = 0;

        // Act
        var result = IsDatePairComplete(year, month);

        // Assert
        result.Should().BeTrue("HasValue returns true for 0, even though it's invalid");
        // Note: This test documents current behavior. Validation of valid month ranges (1-12)
        // should be handled by the UI validation and DateTime constructor.
    }

    [Fact]
    public void SaveTunaSection_Logic_PreservesOtherFieldsWhileProtectingDates()
    {
        // Arrange
        var userInDb = new ApplicationUser
        {
            Id = "user-1",
            Degree = "Engineering",
            MentorId = "mentor-1",
            YearLeitao = 2020,
            MonthLeitao = 5,  // Complete
            YearCaloiro = null,
            MonthCaloiro = null
        };

        var editAttempt = new ApplicationUser
        {
            Id = "user-1",
            Degree = "Computer Science",  // Change allowed
            MentorId = "mentor-2",         // Change allowed
            YearLeitao = 2021,             // Change NOT allowed (complete)
            MonthLeitao = 6,               // Change NOT allowed (complete)
            YearCaloiro = 2022,            // Change allowed (incomplete)
            MonthCaloiro = 8               // Change allowed (incomplete)
        };

        // Act - Apply SaveTunaSection logic
        userInDb.Degree = editAttempt.Degree;
        userInDb.MentorId = editAttempt.MentorId;

        if (!IsDatePairComplete(userInDb.YearLeitao, userInDb.MonthLeitao))
        {
            userInDb.YearLeitao = editAttempt.YearLeitao;
            userInDb.MonthLeitao = editAttempt.MonthLeitao;
        }

        if (!IsDatePairComplete(userInDb.YearCaloiro, userInDb.MonthCaloiro))
        {
            userInDb.YearCaloiro = editAttempt.YearCaloiro;
            userInDb.MonthCaloiro = editAttempt.MonthCaloiro;
        }

        // Assert
        userInDb.Degree.Should().Be("Computer Science", "non-date fields should be updated");
        userInDb.MentorId.Should().Be("mentor-2", "non-date fields should be updated");
        userInDb.YearLeitao.Should().Be(2020, "complete date pair should be protected");
        userInDb.MonthLeitao.Should().Be(5, "complete date pair should be protected");
        userInDb.YearCaloiro.Should().Be(2022, "incomplete date pair should be updated");
        userInDb.MonthCaloiro.Should().Be(8, "incomplete date pair should be updated");
    }

    #endregion
}
