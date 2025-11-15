using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components.UI;

/// <summary>
/// Tests for the EnrollmentStatisticsButton component
/// </summary>
public class EnrollmentStatisticsButtonTests : TestContext
{
    private readonly Mock<IEnrollmentService> _mockEnrollmentService;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;

    public EnrollmentStatisticsButtonTests()
    {
        _mockEnrollmentService = new Mock<IEnrollmentService>();
        
        // Setup UserManager mock
        var store = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            store.Object, null, null, null, null, null, null, null, null);

        Services.AddSingleton(_mockEnrollmentService.Object);
        Services.AddSingleton(_mockUserManager.Object);
        
        // Add required services for components
        ComponentFactories.AddStub<Modal>();
        ComponentFactories.AddStub<TableSearchBar>();
        ComponentFactories.AddStub<EmptyState>();
        ComponentFactories.AddStub<TablePagination>();
    }

    [Fact]
    public void EnrollmentStatisticsButton_DoesNotRender_WhenNotAdmin()
    {
        // Arrange & Act
        var cut = RenderComponent<EnrollmentStatisticsButton>(parameters => parameters
            .Add(p => p.IsAdmin, false));

        // Assert
        cut.Markup.Should().BeEmpty("button should not render for non-admin users");
    }

    [Fact]
    public void EnrollmentStatisticsButton_Renders_WhenIsAdmin()
    {
        // Arrange & Act
        var cut = RenderComponent<EnrollmentStatisticsButton>(parameters => parameters
            .Add(p => p.IsAdmin, true));

        // Assert
        cut.Markup.Should().Contain("btn-outline-primary", "button should render with correct styling");
        cut.Markup.Should().Contain("bi-bar-chart", "button should have bar chart icon");
        cut.Markup.Should().Contain("Estatísticas", "button should have correct text");
    }

    [Fact]
    public void EnrollmentStatisticsButton_HasCorrectButtonProperties()
    {
        // Arrange & Act
        var cut = RenderComponent<EnrollmentStatisticsButton>(parameters => parameters
            .Add(p => p.IsAdmin, true));

        // Assert
        var button = cut.Find("button");
        button.ClassList.Should().Contain("btn-outline-primary", "button should have correct styling");
        cut.Markup.Should().Contain("bi-bar-chart", "button should have bar chart icon");
        cut.Markup.Should().Contain("Estatísticas", "button should have correct text");
        button.GetAttribute("title").Should().Be("Ver Estatísticas");
    }
}

/// <summary>
/// Tests for the MyEnrollmentsButton component
/// </summary>
public class MyEnrollmentsButtonTests : TestContext
{
    private readonly Mock<IEnrollmentService> _mockEnrollmentService;
    private readonly Mock<IEventService> _mockEventService;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;

    public MyEnrollmentsButtonTests()
    {
        _mockEnrollmentService = new Mock<IEnrollmentService>();
        _mockEventService = new Mock<IEventService>();
        
        // Setup UserManager mock
        var store = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            store.Object, null, null, null, null, null, null, null, null);

        Services.AddSingleton(_mockEnrollmentService.Object);
        Services.AddSingleton(_mockEventService.Object);
        Services.AddSingleton(_mockUserManager.Object);
        
        // Setup auth state
        var authContext = this.AddTestAuthorization();
        authContext.SetAuthorized("TestUser");
        
        // Mock UserManager to return a test user
        var testUser = new ApplicationUser
        {
            Id = "test-user-id",
            UserName = "testuser",
            Email = "test@test.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = "TestNick"
        };
        _mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(testUser);
        
        // Add required component stubs
        ComponentFactories.AddStub<Modal>();
        ComponentFactories.AddStub<EmptyState>();
    }

    [Fact]
    public void MyEnrollmentsButton_Renders_Always()
    {
        // Arrange & Act
        var cut = RenderComponent<MyEnrollmentsButton>();

        // Assert
        cut.Markup.Should().Contain("btn-outline-secondary", "button should render with correct styling");
        cut.Markup.Should().Contain("bi-calendar-check", "button should have calendar check icon");
        cut.Markup.Should().Contain("Minhas Inscrições", "button should have correct text");
    }

    [Fact]
    public void MyEnrollmentsButton_HasCorrectButtonProperties()
    {
        // Arrange & Act
        var cut = RenderComponent<MyEnrollmentsButton>();

        // Assert
        var button = cut.Find("button");
        button.ClassList.Should().Contain("btn-outline-secondary", "button should have correct styling");
        cut.Markup.Should().Contain("bi-calendar-check", "button should have calendar check icon");
        cut.Markup.Should().Contain("Minhas Inscrições", "button should have correct text");
        button.GetAttribute("title").Should().Be("Minhas Inscrições");
    }

    [Fact]
    public void MyEnrollmentsButton_CallsServices_WhenButtonClicked()
    {
        // Arrange
        _mockEnrollmentService.Setup(x => x.GetEnrollmentsByUserIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<Enrollment>());
        _mockEventService.Setup(x => x.GetPastEventsAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<Event>());

        var cut = RenderComponent<MyEnrollmentsButton>();

        // Act
        var button = cut.Find("button");
        button.Click();

        // Assert - Services should be called
        cut.WaitForAssertion(() =>
        {
            _mockEnrollmentService.Verify(x => x.GetEnrollmentsByUserIdAsync(It.IsAny<string>()), Times.AtLeastOnce);
        }, TimeSpan.FromSeconds(2));
    }
}
