using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components.UI;

/// <summary>
/// Tests for the EnrollmentStatisticsButton component
/// </summary>
public class EnrollmentStatisticsButtonTests : BunitContext
{
    private readonly Mock<IEnrollmentService> _mockEnrollmentService;
    private readonly Mock<IEventService> _mockEventService;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;

    public EnrollmentStatisticsButtonTests()
    {
        _mockEnrollmentService = new Mock<IEnrollmentService>();
        _mockEventService = new Mock<IEventService>();

        // Setup UserManager mock
        var store = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        Services.AddSingleton(_mockEnrollmentService.Object);
        Services.AddSingleton(_mockEventService.Object);
        Services.AddSingleton(_mockUserManager.Object);

        // Add required services for components
        ComponentFactories.AddStub<SearchBar>();
        ComponentFactories.AddStub<EmptyState>();
        ComponentFactories.AddStub<TablePagination>();

        // Setup JSInterop for modal helper methods used by the Modal component
        JSInterop.SetupVoid("modalHelper.lockBodyScroll");
        JSInterop.SetupVoid("modalHelper.unlockBodyScroll");
    }

    [Fact]
    public void EnrollmentStatisticsButton_DoesNotRender_WhenNotMember()
    {
        // Arrange & Act
        var authContext = this.AddAuthorization();
        authContext.SetNotAuthorized();

        var cut = Render<EnrollmentStatisticsButton>();

        // Assert
        cut.Markup.Should().BeEmpty("button should not render for non-admin users");
    }

    [Fact]
    public void EnrollmentStatisticsButton_Renders_WhenIsMember()
    {
        // Arrange & Act
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("TestUser");
        authContext.SetRoles("Member");

        var cut = Render<EnrollmentStatisticsButton>();

        // Assert
        cut.Markup.Should().Contain("btn-outline-primary", "button should render with correct styling");
        cut.Markup.Should().Contain("bi-bar-chart", "button should have bar chart icon");
        cut.Markup.Should().Contain("Estatísticas", "button should have correct text");
    }

    [Fact]
    public void EnrollmentStatisticsButton_HasCorrectButtonProperties()
    {
        // Arrange & Act
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("TestUser");
        authContext.SetRoles("Member");

        var cut = Render<EnrollmentStatisticsButton>();

        // Assert
        var button = cut.Find("button");
        button.ClassList.Should().Contain("btn-outline-primary", "button should have correct styling");
        cut.Markup.Should().Contain("bi-bar-chart", "button should have bar chart icon");
        cut.Markup.Should().Contain("Estatísticas", "button should have correct text");
        button.GetAttribute("title").Should().Be("Ver Estatísticas");
    }

    [Fact]
    public void EnrollmentStatisticsButton_OpensModalOnClick()
    {
        // Arrange
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("TestUser");
        authContext.SetRoles("Member");

        var cut = Render<EnrollmentStatisticsButton>();

        // Act
        cut.Find("button").Click();

        // Assert - wait for modal content to appear (modal may render asynchronously)
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Estatísticas de Inscrições",
                "modal should open when button is clicked");
        }, TimeSpan.FromSeconds(2));
    }
}
