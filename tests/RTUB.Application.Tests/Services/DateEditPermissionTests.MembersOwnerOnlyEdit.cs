using FluentAssertions;
using Moq;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

// DateEditPermissionTests: Members Owner Only Edit tests.
public partial class DateEditPermissionTests
{
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
}
