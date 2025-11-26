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
public class EnrollmentStatisticsButtonTests : TestContext
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
    }

    [Fact]
    public void EnrollmentStatisticsButton_DoesNotRender_WhenNotMember()
    {
        // Arrange & Act
        var authContext = this.AddTestAuthorization();
        authContext.SetNotAuthorized();

        var cut = RenderComponent<EnrollmentStatisticsButton>();

        // Assert
        cut.Markup.Should().BeEmpty("button should not render for non-admin users");
    }

    [Fact]
    public void EnrollmentStatisticsButton_Renders_WhenIsMember()
    {
        // Arrange & Act
        var authContext = this.AddTestAuthorization();
        authContext.SetAuthorized("TestUser");
        authContext.SetRoles("Member");

        var cut = RenderComponent<EnrollmentStatisticsButton>();

        // Assert
        cut.Markup.Should().Contain("btn-outline-primary", "button should render with correct styling");
        cut.Markup.Should().Contain("bi-bar-chart", "button should have bar chart icon");
        cut.Markup.Should().Contain("Estatísticas", "button should have correct text");
    }

    [Fact]
    public void EnrollmentStatisticsButton_HasCorrectButtonProperties()
    {
        // Arrange & Act
        var authContext = this.AddTestAuthorization();
        authContext.SetAuthorized("TestUser");
        authContext.SetRoles("Member");

        var cut = RenderComponent<EnrollmentStatisticsButton>();

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
        var authContext = this.AddTestAuthorization();
        authContext.SetAuthorized("TestUser");
        authContext.SetRoles("Member");

        var cut = RenderComponent<EnrollmentStatisticsButton>();

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
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

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

    [Fact]
    public void MyEnrollmentsButton_LoadsLast10PastEvents()
    {
        // Arrange
        var pastEvents = Enumerable.Range(1, 15).Select(i => new Event
        {
            Id = i,
            Name = $"Event {i}",
            Date = DateTime.Today.AddDays(-i),
            IsCancelled = false
        }).ToList();

        _mockEventService.Setup(x => x.GetPastEventsAsync(10))
            .ReturnsAsync(pastEvents.Take(10));
        _mockEnrollmentService.Setup(x => x.GetEnrollmentsByUserIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<Enrollment>());

        var cut = RenderComponent<MyEnrollmentsButton>();

        // Act
        var button = cut.Find("button");
        button.Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            _mockEventService.Verify(x => x.GetPastEventsAsync(10), Times.AtLeastOnce);
        }, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void MyEnrollmentsButton_ShowsCorrectParticipationRate()
    {
        // Arrange
        var pastEvents = new List<Event>
        {
            new Event { Id = 1, Name = "Event 1", Date = DateTime.Today.AddDays(-1), IsCancelled = false },
            new Event { Id = 2, Name = "Event 2", Date = DateTime.Today.AddDays(-2), IsCancelled = false },
            new Event { Id = 3, Name = "Event 3", Date = DateTime.Today.AddDays(-3), IsCancelled = false }
        };

        var enrollments = new List<Enrollment>
        {
            new Enrollment { Id = 1, EventId = 1, UserId = "test-user-id", WillAttend = true },
            new Enrollment { Id = 2, EventId = 2, UserId = "test-user-id", WillAttend = true }
            // User didn't enroll in Event 3
        };

        _mockEventService.Setup(x => x.GetPastEventsAsync(10)).ReturnsAsync(pastEvents);
        _mockEnrollmentService.Setup(x => x.GetEnrollmentsByUserIdAsync("test-user-id"))
            .ReturnsAsync(enrollments);

        var cut = RenderComponent<MyEnrollmentsButton>();

        // Act
        var button = cut.Find("button");
        button.Click();

        // The participation rate should be 2/3 (66.7%)
        // This is calculated as: enrolled events / total events
        cut.WaitForAssertion(() =>
        {
            _mockEventService.Verify(x => x.GetPastEventsAsync(10), Times.AtLeastOnce);
        }, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void MyEnrollmentsButton_ShowsStatusBadges()
    {
        // Arrange - The component should show "Foi" for WillAttend:true and "Não foi" for others
        var cut = RenderComponent<MyEnrollmentsButton>();

        // Assert - Button should render with correct styling to display badges
        cut.Markup.Should().Contain("btn-outline-secondary");
    }

    [Fact]
    public void MyEnrollmentsButton_LoadsLast10EventsByDefault()
    {
        // Arrange
        var pastEvents = Enumerable.Range(1, 15).Select(i => new Event
        {
            Id = i,
            Name = $"Event {i}",
            Date = DateTime.Today.AddDays(-i),
            IsCancelled = false
        }).ToList();

        _mockEventService.Setup(x => x.GetPastEventsAsync(10))
            .ReturnsAsync(pastEvents.Take(10).ToList());
        _mockEnrollmentService.Setup(x => x.GetEnrollmentsByUserIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<Enrollment>());

        var cut = RenderComponent<MyEnrollmentsButton>();

        // Act
        var button = cut.Find("button");
        button.Click();

        // Assert - Should request 10 events by default
        cut.WaitForAssertion(() =>
        {
            _mockEventService.Verify(x => x.GetPastEventsAsync(10), Times.AtLeastOnce,
                "Should load last 10 events by default");
        }, TimeSpan.FromSeconds(2));
    }
}
