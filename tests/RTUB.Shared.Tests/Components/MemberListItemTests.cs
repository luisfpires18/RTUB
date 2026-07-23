using AutoFixture;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the MemberListItem component to ensure member list rows display correctly
/// </summary>
public class MemberListItemTests : BunitContext
{
    private readonly Fixture _fixture;

    public MemberListItemTests()
    {
        _fixture = new Fixture();
        // Disable recursion for complex properties
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }

    private ApplicationUser CreateTestUser(string firstName = "John", string lastName = "Doe", string nickname = "Johnny")
    {
        var user = new ApplicationUser
        {
            Id = _fixture.Create<string>(),
            FirstName = firstName,
            LastName = lastName,
            Nickname = nickname,
            Email = _fixture.Create<string>(),
            PhoneNumber = _fixture.Create<string>()
        };
        return user;
    }

    [Fact]
    public void MemberListItem_RendersFullName()
    {
        // Arrange
        var user = CreateTestUser("Jane", "Smith", "Janie");

        // Act
        var cut = Render<MemberListItem>(parameters => parameters
            .Add(p => p.User, user));

        // Assert
        cut.Markup.Should().Contain("Jane Smith", "should display full name");
    }

    [Fact]
    public void MemberListItem_RendersNickname()
    {
        // Arrange
        var user = CreateTestUser("John", "Doe", "Johnny");

        // Act
        var cut = Render<MemberListItem>(parameters => parameters
            .Add(p => p.User, user));

        // Assert
        cut.Markup.Should().Contain("Johnny", "should display nickname");
    }

    [Fact]
    public void MemberListItem_DisplaysDash_ForInstrumentColumn()
    {
        // Arrange - MainInstrument field was removed, instrument column shows dash
        var user = CreateTestUser();

        // Act
        var cut = Render<MemberListItem>(parameters => parameters
            .Add(p => p.User, user));

        // Assert
        var tds = cut.FindAll("td");
        tds[3].InnerHtml.Should().Contain("-", "should display dash in instrument column");
    }

    [Fact]
    public void MemberListItem_ShowsViewDetailsButton()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var cut = Render<MemberListItem>(parameters => parameters
            .Add(p => p.User, user));

        // Assert
        cut.Markup.Should().Contain("btn-info", "should have view details button");
        cut.Markup.Should().Contain("bi-eye", "view button should have eye icon");
    }

    [Fact]
    public void MemberListItem_ShowsEditButton_WhenUserIsAdmin()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var cut = Render<MemberListItem>(parameters => parameters
            .Add(p => p.User, user)
            .Add(p => p.IsAdmin, true));

        // Assert
        cut.Markup.Should().Contain("btn-light", "should have edit button");
        cut.Markup.Should().Contain("bi-pencil", "edit button should have pencil icon");
    }

    [Fact]
    public void MemberListItem_DoesNotShowEditButton_WhenUserIsNotAdmin()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var cut = Render<MemberListItem>(parameters => parameters
            .Add(p => p.User, user)
            .Add(p => p.IsAdmin, false));

        // Assert
        var buttons = cut.FindAll("button");
        buttons.Should().NotContain(b => b.InnerHtml.Contains("bi-pencil"), "edit button should not appear for non-admin");
    }

    [Fact]
    public void MemberListItem_ShowsDeleteButton_WhenUserIsOwner()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var cut = Render<MemberListItem>(parameters => parameters
            .Add(p => p.User, user)
            .Add(p => p.IsOwner, true));

        // Assert
        cut.Markup.Should().Contain("btn-danger", "should have delete button");
        cut.Markup.Should().Contain("bi-trash", "delete button should have trash icon");
    }

    [Fact]
    public void MemberListItem_DoesNotShowDeleteButton_WhenUserIsNotOwner()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var cut = Render<MemberListItem>(parameters => parameters
            .Add(p => p.User, user)
            .Add(p => p.IsOwner, false));

        // Assert
        var buttons = cut.FindAll("button");
        buttons.Should().NotContain(b => b.InnerHtml.Contains("bi-trash"), "delete button should not appear for non-owner");
    }

    [Fact]
    public void MemberListItem_ShowsPositionBadge_WhenCurrentFiscalYearPositionProvided()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var cut = Render<MemberListItem>(parameters => parameters
            .Add(p => p.User, user)
            .Add(p => p.CurrentFiscalYearPosition, Position.Magister));

        // Assert
        cut.Markup.Should().Contain("badge-position", "should display position badge");
    }

    [Fact]
    public void MemberListItem_ShowsNoRolesBadge_WhenUserHasNoPositionsOrCategories()
    {
        // Arrange
        var user = CreateTestUser();
        user.Positions = new List<Position>();
        user.Categories = new List<MemberCategory>();

        // Act
        var cut = Render<MemberListItem>(parameters => parameters
            .Add(p => p.User, user));

        // Assert
        cut.Markup.Should().Contain("Sem Cargos", "should display 'no roles' message");
        cut.Markup.Should().Contain("bg-secondary", "no roles badge should have secondary color");
    }

    [Fact]
    public void MemberListItem_RendersAsTableRow()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var cut = Render<MemberListItem>(parameters => parameters
            .Add(p => p.User, user));

        // Assert
        cut.Markup.Should().StartWith("<tr", "should render as a table row");
    }

    [Fact]
    public void MemberListItem_InvokesOnViewDetails_WhenViewButtonClicked()
    {
        // Arrange
        var user = CreateTestUser();
        bool callbackInvoked = false;

        var cut = Render<MemberListItem>(parameters => parameters
            .Add(p => p.User, user)
            .Add(p => p.OnViewDetails, EventCallback.Factory.Create(this, () => callbackInvoked = true)));

        // Act
        var viewButton = cut.FindAll("button").First(b => b.ClassList.Contains("btn-info"));
        viewButton.Click();

        // Assert
        callbackInvoked.Should().BeTrue("OnViewDetails callback should be invoked");
    }

    [Fact]
    public void MemberListItem_InvokesOnEdit_WhenEditButtonClicked()
    {
        // Arrange
        var user = CreateTestUser();
        bool callbackInvoked = false;

        var cut = Render<MemberListItem>(parameters => parameters
            .Add(p => p.User, user)
            .Add(p => p.IsAdmin, true)
            .Add(p => p.OnEdit, EventCallback.Factory.Create(this, () => callbackInvoked = true)));

        // Act
        var editButton = cut.FindAll("button").First(b => b.ClassList.Contains("btn-light"));
        editButton.Click();

        // Assert
        callbackInvoked.Should().BeTrue("OnEdit callback should be invoked");
    }

    [Fact]
    public void MemberListItem_InvokesOnDelete_WhenDeleteButtonClicked()
    {
        // Arrange
        var user = CreateTestUser();
        bool callbackInvoked = false;

        var cut = Render<MemberListItem>(parameters => parameters
            .Add(p => p.User, user)
            .Add(p => p.IsOwner, true)
            .Add(p => p.OnDelete, EventCallback.Factory.Create(this, () => callbackInvoked = true)));

        // Act
        var deleteButton = cut.FindAll("button").First(b => b.ClassList.Contains("btn-danger"));
        deleteButton.Click();

        // Assert
        callbackInvoked.Should().BeTrue("OnDelete callback should be invoked");
    }
}
