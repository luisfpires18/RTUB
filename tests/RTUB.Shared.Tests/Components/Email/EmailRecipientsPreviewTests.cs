using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Core.Entities;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components.Email;

/// <summary>
/// Tests for the EmailRecipientsPreview component to ensure email recipient display works correctly
/// </summary>
public class EmailRecipientsPreviewTests : BunitContext
{
    private List<ApplicationUser> GetTestSubscribedUsers()
    {
        return new List<ApplicationUser>
        {
            new ApplicationUser { Id = "1", Nickname = "User1", FirstName = "First1", LastName = "Last1", Email = "user1@test.com", ImageUrl = "/img/user1.jpg" },
            new ApplicationUser { Id = "2", Nickname = "User2", FirstName = "First2", LastName = "Last2", Email = "user2@test.com", ImageUrl = "/img/user2.jpg" },
            new ApplicationUser { Id = "3", Nickname = "User3", FirstName = "First3", LastName = "Last3", Email = "user3@test.com", ImageUrl = "/img/user3.jpg" },
        };
    }

    private List<ApplicationUser> GetTestNonSubscribedUsers()
    {
        return new List<ApplicationUser>
        {
            new ApplicationUser { Id = "4", Nickname = "User4", FirstName = "First4", LastName = "Last4", Email = "user4@test.com" },
            new ApplicationUser { Id = "5", Nickname = "User5", FirstName = "First5", LastName = "Last5", Email = "user5@test.com" },
        };
    }

    [Fact]
    public void EmailRecipientsPreview_RendersSubscribedUsers()
    {
        // Arrange
        var subscribedUsers = GetTestSubscribedUsers();

        // Act
        var cut = Render<EmailRecipientsPreview>(parameters => parameters
            .Add(p => p.SubscribedUsers, subscribedUsers)
            .Add(p => p.SubscribedUsersTitle, "Recipients:"));

        // Assert
        cut.Markup.Should().Contain("Recipients:", "subscribed users title should be displayed");
        cut.Markup.Should().Contain("User1", "first user should be displayed");
        cut.Markup.Should().Contain("User2", "second user should be displayed");
        cut.Markup.Should().Contain("User3", "third user should be displayed");
    }

    [Fact]
    public void EmailRecipientsPreview_RendersNonSubscribedUsers_WhenEnabled()
    {
        // Arrange
        var nonSubscribedUsers = GetTestNonSubscribedUsers();

        // Act
        var cut = Render<EmailRecipientsPreview>(parameters => parameters
            .Add(p => p.NonSubscribedUsers, nonSubscribedUsers)
            .Add(p => p.ShowNonSubscribedUsers, true)
            .Add(p => p.NonSubscribedUsersTitle, "Won't receive:"));

        // Assert
        cut.Markup.Should().Contain("Won't receive:", "non-subscribed users title should be displayed");
        cut.Markup.Should().Contain("User4", "first non-subscribed user should be displayed");
        cut.Markup.Should().Contain("User5", "second non-subscribed user should be displayed");
    }

    [Fact]
    public void EmailRecipientsPreview_DoesNotRenderNonSubscribedUsers_WhenDisabled()
    {
        // Arrange
        var nonSubscribedUsers = GetTestNonSubscribedUsers();

        // Act
        var cut = Render<EmailRecipientsPreview>(parameters => parameters
            .Add(p => p.NonSubscribedUsers, nonSubscribedUsers)
            .Add(p => p.ShowNonSubscribedUsers, false)
            .Add(p => p.NonSubscribedUsersTitle, "Won't receive:"));

        // Assert
        cut.Markup.Should().NotContain("Won't receive:", "non-subscribed users should not be displayed when disabled");
        cut.Markup.Should().NotContain("User4", "non-subscribed users should not be displayed when disabled");
    }

    [Fact]
    public void EmailRecipientsPreview_DisplaysInfoBadge_WhenProvided()
    {
        // Arrange
        var subscribedUsers = GetTestSubscribedUsers();
        var infoBadge = "3 / 5 members will receive this message.";

        // Act
        var cut = Render<EmailRecipientsPreview>(parameters => parameters
            .Add(p => p.SubscribedUsers, subscribedUsers)
            .Add(p => p.InfoBadgeText, infoBadge));

        // Assert
        cut.Markup.Should().Contain(infoBadge, "info badge should be displayed");
        cut.Markup.Should().Contain("badge bg-purple", "info badge should have correct styling");
    }

    [Fact]
    public void EmailRecipientsPreview_DisplaysProfilePictures_WhenEnabled()
    {
        // Arrange
        var subscribedUsers = GetTestSubscribedUsers();

        // Act
        var cut = Render<EmailRecipientsPreview>(parameters => parameters
            .Add(p => p.SubscribedUsers, subscribedUsers)
            .Add(p => p.ShowProfilePicture, true));

        // Assert
        cut.Markup.Should().Contain("/img/user1.jpg", "profile picture should be displayed");
        cut.Markup.Should().Contain("member-avatar-small", "profile picture should have correct class");
    }

    [Fact]
    public void EmailRecipientsPreview_DoesNotDisplayProfilePictures_WhenDisabled()
    {
        // Arrange
        var subscribedUsers = GetTestSubscribedUsers();

        // Act
        var cut = Render<EmailRecipientsPreview>(parameters => parameters
            .Add(p => p.SubscribedUsers, subscribedUsers)
            .Add(p => p.ShowProfilePicture, false));

        // Assert
        cut.Markup.Should().NotContain("member-avatar-small", "profile picture should not be displayed");
    }

    [Fact]
    public void EmailRecipientsPreview_DisplaysEmail_WhenEnabled()
    {
        // Arrange
        var subscribedUsers = GetTestSubscribedUsers();

        // Act
        var cut = Render<EmailRecipientsPreview>(parameters => parameters
            .Add(p => p.SubscribedUsers, subscribedUsers)
            .Add(p => p.ShowEmail, true));

        // Assert
        cut.Markup.Should().Contain("user1@test.com", "email should be displayed");
        cut.Markup.Should().Contain("user2@test.com", "email should be displayed");
    }

    [Fact]
    public void EmailRecipientsPreview_DoesNotDisplayEmail_WhenDisabled()
    {
        // Arrange
        var subscribedUsers = GetTestSubscribedUsers();

        // Act
        var cut = Render<EmailRecipientsPreview>(parameters => parameters
            .Add(p => p.SubscribedUsers, subscribedUsers)
            .Add(p => p.ShowEmail, false));

        // Assert
        cut.Markup.Should().NotContain("user1@test.com", "email should not be displayed");
    }

    [Fact]
    public void EmailRecipientsPreview_DisplaysEmptyMessage_WhenNoSubscribedUsers()
    {
        // Arrange
        var emptyUsers = new List<ApplicationUser>();

        // Act
        var cut = Render<EmailRecipientsPreview>(parameters => parameters
            .Add(p => p.SubscribedUsers, emptyUsers)
            .Add(p => p.ShowEmptySubscribedMessage, true)
            .Add(p => p.EmptySubscribedMessage, "No subscribers found."));

        // Assert
        cut.Markup.Should().Contain("No subscribers found.", "empty message should be displayed");
    }

    [Fact]
    public void EmailRecipientsPreview_UsesNickname_WhenAvailable()
    {
        // Arrange
        var users = new List<ApplicationUser>
        {
            new ApplicationUser { Id = "1", Nickname = "CoolNickname", FirstName = "John", LastName = "Doe", Email = "john@test.com" }
        };

        // Act
        var cut = Render<EmailRecipientsPreview>(parameters => parameters
            .Add(p => p.SubscribedUsers, users));

        // Assert
        cut.Markup.Should().Contain("CoolNickname", "nickname should be displayed");
        cut.Markup.Should().NotContain("John Doe", "full name should not be displayed when nickname exists");
    }

    [Fact]
    public void EmailRecipientsPreview_UsesFullName_WhenNicknameNotAvailable()
    {
        // Arrange
        var users = new List<ApplicationUser>
        {
            new ApplicationUser { Id = "1", FirstName = "John", LastName = "Doe", Email = "john@test.com" }
        };

        // Act
        var cut = Render<EmailRecipientsPreview>(parameters => parameters
            .Add(p => p.SubscribedUsers, users));

        // Assert
        cut.Markup.Should().Contain("John Doe", "full name should be displayed when no nickname");
    }

    [Fact]
    public void EmailRecipientsPreview_AppliesCustomMaxHeight()
    {
        // Arrange
        var subscribedUsers = GetTestSubscribedUsers();

        // Act
        var cut = Render<EmailRecipientsPreview>(parameters => parameters
            .Add(p => p.SubscribedUsers, subscribedUsers)
            .Add(p => p.MaxHeight, "400px"));

        // Assert
        // Unit 024 replaced the inline max-height with a modifier class; the pixel
        // value is pinned in InlineStylePolicyTests.
        cut.Markup.Should().Contain("subscriber-list--h400", "custom max height should be applied");
    }

    [Fact]
    public void EmailRecipientsPreview_RendersAdditionalUserInfo_WhenFunctionProvided()
    {
        // Arrange
        var subscribedUsers = GetTestSubscribedUsers();
        Func<ApplicationUser, string> additionalInfoFunc = user => $"ID: {user.Id}";

        // Act
        var cut = Render<EmailRecipientsPreview>(parameters => parameters
            .Add(p => p.SubscribedUsers, subscribedUsers)
            .Add(p => p.AdditionalUserInfoFunc, additionalInfoFunc));

        // Assert
        cut.Markup.Should().Contain("ID: 1", "additional user info should be displayed");
        cut.Markup.Should().Contain("ID: 2", "additional user info should be displayed");
    }

    [Fact]
    public void EmailRecipientsPreview_UsesDefaultTitles()
    {
        // Arrange
        var subscribedUsers = GetTestSubscribedUsers();
        var nonSubscribedUsers = GetTestNonSubscribedUsers();

        // Act
        var cut = Render<EmailRecipientsPreview>(parameters => parameters
            .Add(p => p.SubscribedUsers, subscribedUsers)
            .Add(p => p.NonSubscribedUsers, nonSubscribedUsers)
            .Add(p => p.ShowNonSubscribedUsers, true));

        // Assert
        cut.Markup.Should().Contain("Membros que irão receber:", "default subscribed title should be displayed");
        cut.Markup.Should().Contain("Quem não irá receber:", "default non-subscribed title should be displayed");
    }

    [Fact]
    public void EmailRecipientsPreview_ShowsPagination_WhenEnabledAndManyUsers()
    {
        // Arrange
        var manyUsers = Enumerable.Range(1, 30)
            .Select(i => new ApplicationUser { Id = i.ToString(), Nickname = $"User{i}", Email = $"user{i}@test.com" })
            .ToList();

        // Act
        var cut = Render<EmailRecipientsPreview>(parameters => parameters
            .Add(p => p.SubscribedUsers, manyUsers)
            .Add(p => p.UsePagination, true)
            .Add(p => p.PageSize, 10));

        // Assert
        cut.Markup.Should().Contain("pagination", "pagination should be displayed");
        cut.Markup.Should().Contain("Mostrando", "pagination info should be displayed");
    }

    [Fact]
    public void EmailRecipientsPreview_DoesNotShowPagination_WhenDisabled()
    {
        // Arrange
        var manyUsers = Enumerable.Range(1, 30)
            .Select(i => new ApplicationUser { Id = i.ToString(), Nickname = $"User{i}", Email = $"user{i}@test.com" })
            .ToList();

        // Act
        var cut = Render<EmailRecipientsPreview>(parameters => parameters
            .Add(p => p.SubscribedUsers, manyUsers)
            .Add(p => p.UsePagination, false)
            .Add(p => p.PageSize, 10));

        // Assert
        // With pagination disabled, all 30 users should be rendered
        var userMatches = System.Text.RegularExpressions.Regex.Matches(cut.Markup, @"User\d+");
        userMatches.Count.Should().BeGreaterThanOrEqualTo(25, "all users should be displayed without pagination");
    }

    [Fact]
    public void EmailRecipientsPreview_RespectsPaginationPageSize()
    {
        // Arrange
        var manyUsers = Enumerable.Range(1, 30)
            .Select(i => new ApplicationUser { Id = i.ToString(), Nickname = $"User{i}", Email = $"user{i}@test.com" })
            .ToList();

        // Act
        var cut = Render<EmailRecipientsPreview>(parameters => parameters
            .Add(p => p.SubscribedUsers, manyUsers)
            .Add(p => p.UsePagination, true)
            .Add(p => p.PageSize, 15));

        // Assert
        // Should show first 15 users
        cut.Markup.Should().Contain("User1", "first user should be displayed");
        cut.Markup.Should().Contain("User15", "15th user should be displayed");
    }

    [Fact]
    public void EmailRecipientsPreview_HandlesEmptySubscribedList()
    {
        // Arrange
        var emptyUsers = new List<ApplicationUser>();

        // Act
        var cut = Render<EmailRecipientsPreview>(parameters => parameters
            .Add(p => p.SubscribedUsers, emptyUsers));

        // Assert - should not crash
        cut.Markup.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void EmailRecipientsPreview_HandlesEmptyNonSubscribedList()
    {
        // Arrange
        var subscribedUsers = GetTestSubscribedUsers();
        var emptyNonSubscribed = new List<ApplicationUser>();

        // Act
        var cut = Render<EmailRecipientsPreview>(parameters => parameters
            .Add(p => p.SubscribedUsers, subscribedUsers)
            .Add(p => p.NonSubscribedUsers, emptyNonSubscribed)
            .Add(p => p.ShowNonSubscribedUsers, true));

        // Assert - should not crash and should show subscribed users
        cut.Markup.Should().Contain("User1", "subscribed users should still be displayed");
    }
}
