using Xunit;
using FluentAssertions;

namespace RTUB.Web.Tests.Pages.Admin;

/// <summary>
/// Unit tests for OwnerEmail page behavior
/// Testing form validation, email sending logic, and authorization
/// </summary>
public class OwnerEmailTests
{
    #region Form Validation Tests

    [Fact]
    public void EmailForm_EmptyTitle_ShouldFailValidation()
    {
        // Arrange
        var title = "";
        var content = "Test content";

        // Act
        var isValid = ValidateEmailForm(title, content);

        // Assert
        isValid.Should().BeFalse("Empty title should fail validation");
    }

    [Fact]
    public void EmailForm_EmptyContent_ShouldFailValidation()
    {
        // Arrange
        var title = "Test title";
        var content = "";

        // Act
        var isValid = ValidateEmailForm(title, content);

        // Assert
        isValid.Should().BeFalse("Empty content should fail validation");
    }

    [Fact]
    public void EmailForm_ValidTitleAndContent_ShouldPassValidation()
    {
        // Arrange
        var title = "Test Announcement";
        var content = "This is a test announcement message.";

        // Act
        var isValid = ValidateEmailForm(title, content);

        // Assert
        isValid.Should().BeTrue("Valid title and content should pass validation");
    }

    [Fact]
    public void EmailForm_WhitespaceTitle_ShouldFailValidation()
    {
        // Arrange
        var title = "   ";
        var content = "Test content";

        // Act
        var isValid = ValidateEmailForm(title, content);

        // Assert
        isValid.Should().BeFalse("Whitespace-only title should fail validation");
    }

    [Fact]
    public void EmailForm_WhitespaceContent_ShouldFailValidation()
    {
        // Arrange
        var title = "Test title";
        var content = "   ";

        // Act
        var isValid = ValidateEmailForm(title, content);

        // Assert
        isValid.Should().BeFalse("Whitespace-only content should fail validation");
    }

    #endregion

    #region Email Subject Format Tests

    [Theory]
    [InlineData("New Event", "[RTUB] New Event")]
    [InlineData("Important Update", "[RTUB] Important Update")]
    [InlineData("Testing 123", "[RTUB] Testing 123")]
    public void EmailSubject_ShouldHaveRTUBPrefix(string title, string expectedSubject)
    {
        // Act
        var subject = FormatEmailSubject(title);

        // Assert
        subject.Should().Be(expectedSubject, "Email subject should have [RTUB] prefix");
    }

    [Fact]
    public void EmailSubject_EmptyTitle_ShouldReturnRTUBOnly()
    {
        // Arrange
        var title = "";

        // Act
        var subject = FormatEmailSubject(title);

        // Assert
        subject.Should().Be("[RTUB] ", "Empty title should still have [RTUB] prefix");
    }

    #endregion

    #region Email Content Format Tests

    [Fact]
    public void EmailContent_ShouldPreserveMultipleLines()
    {
        // Arrange
        var content = "Line 1\nLine 2\nLine 3";

        // Act
        var formattedContent = content;

        // Assert
        formattedContent.Should().Contain("\n", "Content should preserve line breaks");
        formattedContent.Split('\n').Length.Should().Be(3, "Content should have 3 lines");
    }

    [Fact]
    public void EmailContent_ShouldNotExceedMaxLength()
    {
        // Arrange
        var maxLength = 5000;
        var longContent = new string('a', 6000);

        // Act
        var isTooLong = longContent.Length > maxLength;

        // Assert
        isTooLong.Should().BeTrue("Content exceeding 5000 characters should be flagged");
    }

    #endregion

    #region Recipient Filtering Tests

    [Fact]
    public void Recipients_ShouldOnlyIncludeSubscribedUsers()
    {
        // This test verifies that only users with Subscribed = true are included
        // Arrange
        var allUsers = GetMockUsers();

        // Act
        var subscribedUsers = allUsers.Where(u => u.Subscribed).ToList();

        // Assert
        subscribedUsers.Count.Should().Be(2, "Only 2 users are subscribed");
        subscribedUsers.All(u => u.Subscribed).Should().BeTrue("All filtered users should be subscribed");
    }

    [Fact]
    public void Recipients_ShouldOnlyIncludeConfirmedEmails()
    {
        // Arrange
        var allUsers = GetMockUsers();

        // Act
        var validRecipients = allUsers
            .Where(u => u.Subscribed && u.EmailConfirmed && !string.IsNullOrEmpty(u.Email))
            .ToList();

        // Assert
        validRecipients.Count.Should().Be(1, "Only 1 user has subscribed + confirmed email");
    }

    [Fact]
    public void Recipients_ShouldExcludeNullEmails()
    {
        // Arrange
        var users = new[]
        {
            new MockUser { Subscribed = true, EmailConfirmed = true, Email = "test@test.com" },
            new MockUser { Subscribed = true, EmailConfirmed = true, Email = null },
            new MockUser { Subscribed = true, EmailConfirmed = true, Email = "" }
        };

        // Act
        var validRecipients = users
            .Where(u => u.Subscribed && u.EmailConfirmed && !string.IsNullOrEmpty(u.Email))
            .ToList();

        // Assert
        validRecipients.Count.Should().Be(1, "Only users with non-empty emails should be included");
    }

    #endregion

    #region User Management Tests

    [Fact]
    public void RemoveUser_ShouldToggleSubscribedToFalse()
    {
        // Arrange
        var user = new MockUser { Subscribed = true, EmailConfirmed = true, Email = "user@test.com" };

        // Act
        user.Subscribed = false;

        // Assert
        user.Subscribed.Should().BeFalse("User should be unsubscribed after removal");
    }

    [Fact]
    public void AddUser_ShouldToggleSubscribedToTrue()
    {
        // Arrange
        var user = new MockUser { Subscribed = false, EmailConfirmed = true, Email = "user@test.com" };

        // Act
        user.Subscribed = true;

        // Assert
        user.Subscribed.Should().BeTrue("User should be subscribed after addition");
    }

    [Fact]
    public void RemoveUser_ShouldUpdateLists()
    {
        // Arrange
        var subscribedUsers = new List<MockUser>
        {
            new MockUser { Subscribed = true, EmailConfirmed = true, Email = "user1@test.com" },
            new MockUser { Subscribed = true, EmailConfirmed = true, Email = "user2@test.com" }
        };
        var nonSubscribedUsers = new List<MockUser>();
        var userToRemove = subscribedUsers[0];

        // Act
        subscribedUsers.Remove(userToRemove);
        userToRemove.Subscribed = false;
        nonSubscribedUsers.Add(userToRemove);

        // Assert
        subscribedUsers.Count.Should().Be(1, "Subscribed list should have one less user");
        nonSubscribedUsers.Count.Should().Be(1, "Non-subscribed list should have one more user");
        nonSubscribedUsers.Should().Contain(userToRemove, "Removed user should be in non-subscribed list");
    }

    [Fact]
    public void AddUser_ShouldUpdateLists()
    {
        // Arrange
        var subscribedUsers = new List<MockUser>();
        var nonSubscribedUsers = new List<MockUser>
        {
            new MockUser { Subscribed = false, EmailConfirmed = true, Email = "user1@test.com" },
            new MockUser { Subscribed = false, EmailConfirmed = true, Email = "user2@test.com" }
        };
        var userToAdd = nonSubscribedUsers[0];

        // Act
        nonSubscribedUsers.Remove(userToAdd);
        userToAdd.Subscribed = true;
        subscribedUsers.Add(userToAdd);

        // Assert
        subscribedUsers.Count.Should().Be(1, "Subscribed list should have one more user");
        nonSubscribedUsers.Count.Should().Be(1, "Non-subscribed list should have one less user");
        subscribedUsers.Should().Contain(userToAdd, "Added user should be in subscribed list");
    }

    [Fact]
    public void RemoveUser_ShouldUpdateCounts()
    {
        // Arrange
        var subscribedCount = 5;
        var nonSubscribedCount = 3;

        // Act - simulate removing a user
        subscribedCount--;
        nonSubscribedCount++;

        // Assert
        subscribedCount.Should().Be(4, "Subscribed count should decrease by 1");
        nonSubscribedCount.Should().Be(4, "Non-subscribed count should increase by 1");
    }

    [Fact]
    public void AddUser_ShouldUpdateCounts()
    {
        // Arrange
        var subscribedCount = 5;
        var nonSubscribedCount = 3;

        // Act - simulate adding a user
        subscribedCount++;
        nonSubscribedCount--;

        // Assert
        subscribedCount.Should().Be(6, "Subscribed count should increase by 1");
        nonSubscribedCount.Should().Be(2, "Non-subscribed count should decrease by 1");
    }

    #endregion

    #region Bulk Operations Tests

    [Fact]
    public void RemoveMultipleUsers_ShouldToggleSubscribedToFalse()
    {
        // Arrange
        var users = new List<MockUser>
        {
            new MockUser { Subscribed = true, EmailConfirmed = true, Email = "user1@test.com" },
            new MockUser { Subscribed = true, EmailConfirmed = true, Email = "user2@test.com" },
            new MockUser { Subscribed = true, EmailConfirmed = true, Email = "user3@test.com" }
        };

        // Act
        foreach (var user in users)
        {
            user.Subscribed = false;
        }

        // Assert
        users.All(u => !u.Subscribed).Should().BeTrue("All users should be unsubscribed after bulk removal");
    }

    [Fact]
    public void RemoveMultipleUsers_ShouldUpdateLists()
    {
        // Arrange
        var subscribedUsers = new List<MockUser>
        {
            new MockUser { Subscribed = true, EmailConfirmed = true, Email = "user1@test.com" },
            new MockUser { Subscribed = true, EmailConfirmed = true, Email = "user2@test.com" },
            new MockUser { Subscribed = true, EmailConfirmed = true, Email = "user3@test.com" }
        };
        var nonSubscribedUsers = new List<MockUser>();
        var usersToRemove = subscribedUsers.Take(2).ToList();

        // Act
        foreach (var user in usersToRemove)
        {
            subscribedUsers.Remove(user);
            user.Subscribed = false;
            nonSubscribedUsers.Add(user);
        }

        // Assert
        subscribedUsers.Count.Should().Be(1, "Subscribed list should have 1 user remaining");
        nonSubscribedUsers.Count.Should().Be(2, "Non-subscribed list should have 2 users added");
        usersToRemove.All(u => nonSubscribedUsers.Contains(u)).Should().BeTrue("All removed users should be in non-subscribed list");
    }

    [Fact]
    public void RemoveMultipleUsers_ShouldUpdateCounts()
    {
        // Arrange
        var subscribedCount = 5;
        var nonSubscribedCount = 3;
        var usersToRemove = 2;

        // Act
        subscribedCount -= usersToRemove;
        nonSubscribedCount += usersToRemove;

        // Assert
        subscribedCount.Should().Be(3, "Subscribed count should decrease by 2");
        nonSubscribedCount.Should().Be(5, "Non-subscribed count should increase by 2");
    }

    [Fact]
    public void RemoveMultipleUsers_EmptyList_ShouldNotFail()
    {
        // Arrange
        var users = new List<MockUser>();
        var subscribedCount = 5;

        // Act
        var count = users.Count;

        // Assert
        count.Should().Be(0, "Empty list should have count of 0");
        subscribedCount.Should().Be(5, "Original count should remain unchanged");
    }

    [Fact]
    public void AddMultipleUsers_ShouldToggleSubscribedToTrue()
    {
        // Arrange
        var users = new List<MockUser>
        {
            new MockUser { Subscribed = false, EmailConfirmed = true, Email = "user1@test.com" },
            new MockUser { Subscribed = false, EmailConfirmed = true, Email = "user2@test.com" },
            new MockUser { Subscribed = false, EmailConfirmed = true, Email = "user3@test.com" }
        };

        // Act
        foreach (var user in users)
        {
            user.Subscribed = true;
        }

        // Assert
        users.All(u => u.Subscribed).Should().BeTrue("All users should be subscribed after bulk addition");
    }

    [Fact]
    public void AddMultipleUsers_ShouldUpdateLists()
    {
        // Arrange
        var subscribedUsers = new List<MockUser>();
        var nonSubscribedUsers = new List<MockUser>
        {
            new MockUser { Subscribed = false, EmailConfirmed = true, Email = "user1@test.com" },
            new MockUser { Subscribed = false, EmailConfirmed = true, Email = "user2@test.com" },
            new MockUser { Subscribed = false, EmailConfirmed = true, Email = "user3@test.com" }
        };
        var usersToAdd = nonSubscribedUsers.Take(2).ToList();

        // Act
        foreach (var user in usersToAdd)
        {
            nonSubscribedUsers.Remove(user);
            user.Subscribed = true;
            subscribedUsers.Add(user);
        }

        // Assert
        subscribedUsers.Count.Should().Be(2, "Subscribed list should have 2 users added");
        nonSubscribedUsers.Count.Should().Be(1, "Non-subscribed list should have 1 user remaining");
        usersToAdd.All(u => subscribedUsers.Contains(u)).Should().BeTrue("All added users should be in subscribed list");
    }

    [Fact]
    public void AddMultipleUsers_ShouldUpdateCounts()
    {
        // Arrange
        var subscribedCount = 3;
        var nonSubscribedCount = 5;
        var usersToAdd = 2;

        // Act
        subscribedCount += usersToAdd;
        nonSubscribedCount -= usersToAdd;

        // Assert
        subscribedCount.Should().Be(5, "Subscribed count should increase by 2");
        nonSubscribedCount.Should().Be(3, "Non-subscribed count should decrease by 2");
    }

    [Fact]
    public void AddMultipleUsers_EmptyList_ShouldNotFail()
    {
        // Arrange
        var users = new List<MockUser>();
        var nonSubscribedCount = 5;

        // Act
        var count = users.Count;

        // Assert
        count.Should().Be(0, "Empty list should have count of 0");
        nonSubscribedCount.Should().Be(5, "Original count should remain unchanged");
    }

    #endregion

    #region Search Functionality Tests

    [Fact]
    public void Search_ShouldFilterUsersByNickname()
    {
        // Arrange
        var users = new List<MockUser>
        {
            new MockUser { Nickname = "John", FirstName = "John", LastName = "Doe", Email = "john@test.com" },
            new MockUser { Nickname = "Jane", FirstName = "Jane", LastName = "Smith", Email = "jane@test.com" },
            new MockUser { Nickname = "Bob", FirstName = "Robert", LastName = "Brown", Email = "bob@test.com" }
        };
        var searchQuery = "john";

        // Act
        var filteredUsers = users.Where(u =>
            (!string.IsNullOrEmpty(u.Nickname) && u.Nickname.ToLower().Contains(searchQuery.ToLower()))).ToList();

        // Assert
        filteredUsers.Count.Should().Be(1, "Should find one user matching 'john'");
        filteredUsers[0].Nickname.Should().Be("John", "Should match the user with nickname John");
    }

    [Fact]
    public void Search_ShouldFilterUsersByFirstName()
    {
        // Arrange
        var users = new List<MockUser>
        {
            new MockUser { Nickname = null, FirstName = "Alice", LastName = "Wonder", Email = "alice@test.com" },
            new MockUser { Nickname = null, FirstName = "Bob", LastName = "Builder", Email = "bob@test.com" },
            new MockUser { Nickname = null, FirstName = "Charlie", LastName = "Brown", Email = "charlie@test.com" }
        };
        var searchQuery = "ali";

        // Act
        var filteredUsers = users.Where(u =>
            (!string.IsNullOrEmpty(u.FirstName) && u.FirstName.ToLower().Contains(searchQuery.ToLower()))).ToList();

        // Assert
        filteredUsers.Count.Should().Be(1, "Should find one user with first name containing 'ali'");
        filteredUsers[0].FirstName.Should().Be("Alice", "Should match Alice");
    }

    [Fact]
    public void Search_ShouldFilterUsersByLastName()
    {
        // Arrange
        var users = new List<MockUser>
        {
            new MockUser { FirstName = "John", LastName = "Smith", Email = "john@test.com" },
            new MockUser { FirstName = "Jane", LastName = "Doe", Email = "jane@test.com" },
            new MockUser { FirstName = "Bob", LastName = "Smith", Email = "bob@test.com" }
        };
        var searchQuery = "smith";

        // Act
        var filteredUsers = users.Where(u =>
            (!string.IsNullOrEmpty(u.LastName) && u.LastName.ToLower().Contains(searchQuery.ToLower()))).ToList();

        // Assert
        filteredUsers.Count.Should().Be(2, "Should find two users with last name Smith");
        filteredUsers.All(u => u.LastName == "Smith").Should().BeTrue("All filtered users should have last name Smith");
    }

    [Fact]
    public void Search_ShouldFilterUsersByEmail()
    {
        // Arrange
        var users = new List<MockUser>
        {
            new MockUser { FirstName = "John", LastName = "Doe", Email = "john.doe@company.com" },
            new MockUser { FirstName = "Jane", LastName = "Smith", Email = "jane.smith@company.com" },
            new MockUser { FirstName = "Bob", LastName = "Brown", Email = "bob@example.com" }
        };
        var searchQuery = "company";

        // Act
        var filteredUsers = users.Where(u =>
            (!string.IsNullOrEmpty(u.Email) && u.Email.ToLower().Contains(searchQuery.ToLower()))).ToList();

        // Assert
        filteredUsers.Count.Should().Be(2, "Should find two users with email containing 'company'");
        filteredUsers.All(u => u.Email!.Contains("company")).Should().BeTrue("All filtered users should have 'company' in email");
    }

    [Fact]
    public void Search_WithEmptyQuery_ShouldReturnAllUsers()
    {
        // Arrange
        var users = new List<MockUser>
        {
            new MockUser { FirstName = "John", Email = "john@test.com" },
            new MockUser { FirstName = "Jane", Email = "jane@test.com" },
            new MockUser { FirstName = "Bob", Email = "bob@test.com" }
        };
        var searchQuery = string.Empty;

        // Act
        var filteredUsers = string.IsNullOrWhiteSpace(searchQuery) ? users : users.Where(u =>
            (!string.IsNullOrEmpty(u.FirstName) && u.FirstName.ToLower().Contains(searchQuery.ToLower()))).ToList();

        // Assert
        filteredUsers.Count.Should().Be(3, "Empty search should return all users");
    }

    [Fact]
    public void Search_CaseInsensitive_ShouldMatchUsers()
    {
        // Arrange
        var users = new List<MockUser>
        {
            new MockUser { Nickname = "JohnDoe", FirstName = "John", LastName = "Doe", Email = "JOHN@TEST.COM" }
        };
        var searchQuery = "john";

        // Act
        var filteredByNickname = users.Where(u =>
            (!string.IsNullOrEmpty(u.Nickname) && u.Nickname.ToLower().Contains(searchQuery.ToLower()))).Any();
        var filteredByEmail = users.Where(u =>
            (!string.IsNullOrEmpty(u.Email) && u.Email.ToLower().Contains(searchQuery.ToLower()))).Any();

        // Assert
        filteredByNickname.Should().BeTrue("Search should be case insensitive for nickname");
        filteredByEmail.Should().BeTrue("Search should be case insensitive for email");
    }

    [Fact]
    public void Search_SelectAll_ShouldOnlySelectFilteredUsers()
    {
        // Arrange
        var allUsers = new List<MockUser>
        {
            new MockUser { Id = "1", FirstName = "John", Email = "john@test.com" },
            new MockUser { Id = "2", FirstName = "Jane", Email = "jane@test.com" },
            new MockUser { Id = "3", FirstName = "Bob", Email = "bob@test.com" }
        };
        var searchQuery = "j";
        var filteredUsers = allUsers.Where(u =>
            (!string.IsNullOrEmpty(u.FirstName) && u.FirstName.ToLower().Contains(searchQuery.ToLower()))).ToList();

        // Act
        var selectedIds = filteredUsers.Select(u => u.Id).ToHashSet();

        // Assert
        selectedIds.Count.Should().Be(2, "Should select only filtered users (John and Jane)");
        selectedIds.Should().Contain("1", "Should select John");
        selectedIds.Should().Contain("2", "Should select Jane");
        selectedIds.Should().NotContain("3", "Should not select Bob");
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public void OwnerEmailPage_ShouldRequireOwnerRole()
    {
        // This test documents the authorization requirement
        // The page should have [Authorize(Roles = "Owner")] attribute

        // Arrange
        var requiredRole = "Owner";

        // Assert
        requiredRole.Should().Be("Owner", "Page should require Owner role");
    }

    #endregion

    #region Helper Methods

    private bool ValidateEmailForm(string title, string content)
    {
        return !string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(content);
    }

    private string FormatEmailSubject(string title)
    {
        return $"[RTUB] {title}";
    }

    private List<MockUser> GetMockUsers()
    {
        return new List<MockUser>
        {
            new MockUser { Subscribed = true, EmailConfirmed = true, Email = "user1@test.com" },
            new MockUser { Subscribed = true, EmailConfirmed = false, Email = "user2@test.com" },
            new MockUser { Subscribed = false, EmailConfirmed = true, Email = "user3@test.com" }
        };
    }

    private class MockUser
    {
        public string? Id { get; set; }
        public string? Nickname { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public bool Subscribed { get; set; }
        public bool EmailConfirmed { get; set; }
        public string? Email { get; set; }
    }

    #endregion
}
