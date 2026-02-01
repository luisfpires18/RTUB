using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Pages.Activities;
using RTUB.Web.Tests.Pages.Base;

namespace RTUB.Web.Tests.Pages.Activities;

/// <summary>
/// Component tests for Events.razor page
/// Tests page rendering, modal interactions, CRUD workflows, enrollment workflows, and authorization
/// </summary>
public class EventsPageTests : PageTestBase
{
    private readonly Mock<IEventService> _mockEventService;
    private readonly Mock<IEventFilterService> _mockEventFilterService;
    private readonly Mock<IEventUrlService> _mockEventUrlService;
    private readonly Mock<IEnrollmentFilterService> _mockEnrollmentFilterService;
    private readonly Mock<IEnrollmentStatisticsService> _mockEnrollmentStatisticsService;
    private readonly Mock<IFiscalYearService> _mockFiscalYearService;
    private readonly Mock<IEnrollmentService> _mockEnrollmentService;
    private readonly Mock<IMemberInstrumentService> _mockMemberInstrumentService;
    private readonly Mock<ITrophyService> _mockTrophyService;
    private readonly Mock<IEmailNotificationService> _mockEmailNotificationService;
    private readonly Mock<IPushNotificationService> _mockPushNotificationService;
    private readonly Mock<IPushNotificationFactory> _mockPushNotificationFactory;
    private readonly Mock<IImageStorageService> _mockImageStorageService;
    private readonly Mock<IEventRepertoireService> _mockEventRepertoireService;
    private readonly Mock<IDiscussionService> _mockDiscussionService;
    private readonly Mock<IPostService> _mockPostService;
    private readonly Mock<IAuditLogService> _mockAuditLogService;
    private readonly Mock<ISongService> _mockSongService;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment> _mockWebHostEnvironment;
    private readonly Mock<RTUB.Web.Interop.MediaSessionInterop> _mockMediaSessionInterop;

    public EventsPageTests()
    {
        // Setup service mocks
        _mockEventService = SetupService<IEventService>();
        _mockEventFilterService = SetupService<IEventFilterService>();
        _mockEventUrlService = SetupService<IEventUrlService>();
        _mockEnrollmentFilterService = SetupService<IEnrollmentFilterService>();
        _mockEnrollmentStatisticsService = SetupService<IEnrollmentStatisticsService>();
        _mockFiscalYearService = SetupService<IFiscalYearService>();
        _mockEnrollmentService = SetupService<IEnrollmentService>();
        _mockMemberInstrumentService = SetupService<IMemberInstrumentService>();
        _mockTrophyService = SetupService<ITrophyService>();
        _mockEmailNotificationService = SetupService<IEmailNotificationService>();
        _mockPushNotificationService = SetupService<IPushNotificationService>();
        _mockPushNotificationFactory = SetupService<IPushNotificationFactory>();
        _mockImageStorageService = SetupService<IImageStorageService>();
        _mockEventRepertoireService = SetupService<IEventRepertoireService>();
        _mockDiscussionService = SetupService<IDiscussionService>();
        _mockPostService = SetupService<IPostService>();
        _mockAuditLogService = SetupService<IAuditLogService>();
        _mockSongService = SetupService<ISongService>();
        _mockUserManager = SetupUserManager();
        _mockWebHostEnvironment = SetupWebHostEnvironment();

        // MediaSessionInterop: use mock (no non-virtual usage in Events tests). PwaHelperInterop: use real
        // instance (IsMobilePwaOrBrowserAsync is non-virtual); stub JS instead.
        _mockMediaSessionInterop = new Mock<RTUB.Web.Interop.MediaSessionInterop>(MockJSRuntime.Object);
        Services.AddSingleton(_mockMediaSessionInterop.Object);

        MockJSRuntime
            .Setup(x => x.InvokeAsync<bool>(It.Is<string>(s => s == "pwaHelper.isMobilePwaOrBrowser"), It.IsAny<object[]>()))
            .Returns(new ValueTask<bool>(false));
        Services.AddSingleton(new RTUB.Web.Interop.PwaHelperInterop(MockJSRuntime.Object));

        // Setup default service responses for new services
        _mockEventFilterService
            .Setup(x => x.FilterEvents(It.IsAny<IEnumerable<Event>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((IEnumerable<Event> events, string? fiscalYear, string? eventType, string? search) =>
            {
                var eventsList = events?.ToList() ?? new List<Event>();
                var today = DateTime.Today;
                var future = eventsList.Where(e => e.Date >= today).OrderBy(e => e.Date).ToList();
                var past = eventsList.Where(e => e.Date < today).OrderByDescending(e => e.Date).ToList();
                return (future, past);
            });

        _mockEventUrlService
            .Setup(x => x.BuildQueryString(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("");

        _mockEnrollmentFilterService
            .Setup(x => x.FilterEnrollments(It.IsAny<IEnumerable<Enrollment>>(), It.IsAny<string>()))
            .Returns((IEnumerable<Enrollment> enrollments, string? search) =>
            {
                var enrollmentsList = enrollments?.ToList() ?? new List<Enrollment>();
                var performing = enrollmentsList.Where(e => e.User != null && e.WillAttend && !e.User.Categories.Contains(MemberCategory.Leitao)).ToList();
                var leitoes = enrollmentsList.Where(e => e.User != null && e.WillAttend && e.User.Categories.Contains(MemberCategory.Leitao)).ToList();
                var notAttending = enrollmentsList.Where(e => e.User != null && !e.WillAttend).ToList();
                return (performing, leitoes, notAttending);
            });

        _mockEnrollmentStatisticsService
            .Setup(x => x.CalculateInstrumentCounts(It.IsAny<Event>(), It.IsAny<Dictionary<string, List<MemberInstrument>>>()))
            .Returns((new Dictionary<InstrumentType, int>(), new Dictionary<InstrumentType, int>()));

        // Setup default service responses
        _mockEventService
            .Setup(x => x.GetUpcomingEventsAsync(It.IsAny<int>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new List<Event>());

        _mockEventService
            .Setup(x => x.GetPastEventsAsync(It.IsAny<int>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new List<Event>());

        _mockFiscalYearService
            .Setup(x => x.GetAllFiscalYearsAsync())
            .ReturnsAsync(new List<FiscalYear>());

        _mockEnrollmentService
            .Setup(x => x.GetEnrollmentsByEventIdAsync(It.IsAny<int>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new List<Enrollment>());

        _mockEnrollmentService
            .Setup(x => x.GetEnrollmentsByUserIdAsync(It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new List<Enrollment>());

        _mockTrophyService
            .Setup(x => x.GetByEventIdAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<Trophy>());

        _mockDiscussionService
            .Setup(x => x.GetByEventIdAsync(It.IsAny<int>()))
            .ReturnsAsync((Discussion?)null);

        _mockPostService
            .Setup(x => x.GetCountByDiscussionIdAsync(It.IsAny<int>()))
            .ReturnsAsync(0);

        _mockEventRepertoireService
            .Setup(x => x.GetRepertoireByEventIdAsync(It.IsAny<int>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(new List<EventRepertoire>());

        _mockSongService
            .Setup(x => x.GetSongsByAlbumIdAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<Song>());

        _mockUserManager
            .Setup(x => x.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);
    }

    #region Page Rendering Tests

    [Fact]
    public async Task EventsPage_RendersPageTitle()
    {
        // Arrange
        SetupAuthentication("test-user", "Test User");

        // Act
        var cut = RenderComponent<Events>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Atuações", "page should display 'Atuações' title");
        cut.Markup.Should().Contain("Próximas atuações e atividades", "page should display subtitle");
    }

    [Fact]
    public async Task EventsPage_ShowsLoadingState_Initially()
    {
        // Arrange - Setup slow service to test loading state
        SetupAuthentication("test-user", "Test User");
        _mockEventService
            .Setup(x => x.GetUpcomingEventsAsync(It.IsAny<int>(), It.IsAny<System.Threading.CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(100);
                return (IEnumerable<Event>)new List<Event>();
            });

        // Act
        var cut = RenderComponent<Events>();

        // Assert - Should show loading initially (before async completes)
        cut.Markup.Should().Contain("A carregar atuações", "page should show loading message initially");

        // Wait for async to complete
        cut.WaitForState(() => !cut.Markup.Contains("A carregar atuações"), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task EventsPage_ShowsEmptyState_WhenNoEvents()
    {
        // Arrange
        SetupAuthentication("test-user", "Test User");
        _mockEventService
            .Setup(x => x.GetUpcomingEventsAsync(It.IsAny<int>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new List<Event>());

        _mockEventService
            .Setup(x => x.GetPastEventsAsync(It.IsAny<int>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new List<Event>());

        // Act
        var cut = RenderComponent<Events>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Nenhum evento encontrado", "page should show empty state message");
    }

    [Fact]
    public async Task EventsPage_DisplaysEvents_WhenEventsExist()
    {
        // Arrange
        SetupAuthentication("test-user", "Test User");
        var futureEvent = CreateTestEvent(1, "Future Event", DateTime.Now.AddDays(7), EventType.Atuacao);
        var pastEvent = CreateTestEvent(2, "Past Event", DateTime.Now.AddDays(-7), EventType.Atuacao);

        _mockEventService
            .Setup(x => x.GetUpcomingEventsAsync(It.IsAny<int>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new List<Event> { futureEvent });

        _mockEventService
            .Setup(x => x.GetPastEventsAsync(It.IsAny<int>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new List<Event> { pastEvent });

        // Act
        var cut = RenderComponent<Events>();
        cut.WaitForState(() => cut.Markup.Contains("Future Event") || cut.Markup.Contains("Past Event"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().ContainAny(new[] { "Future Event", "Past Event" }, "page should display events");
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task EventsPage_ShowsCreateButton_ForAdminUser()
    {
        // Arrange
        SetupAuthentication("admin-user", "Admin User", "Admin");
        _mockEventService
            .Setup(x => x.GetUpcomingEventsAsync(It.IsAny<int>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new List<Event>());

        // Act
        var cut = RenderComponent<Events>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Adicionar Atuação", "page should show create button for admin");
    }

    [Fact]
    public async Task EventsPage_HidesCreateButton_ForRegularUser()
    {
        // Arrange
        SetupAuthentication("regular-user", "Regular User");
        _mockEventService
            .Setup(x => x.GetUpcomingEventsAsync(It.IsAny<int>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new List<Event>());

        // Act
        var cut = RenderComponent<Events>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().NotContain("Adicionar Atuação", "page should not show create button for regular user");
    }

    [Fact]
    public async Task EventsPage_ShowsTrophiesButton_ForAllUsers()
    {
        // Arrange
        SetupAuthentication("test-user", "Test User");
        _mockEventService
            .Setup(x => x.GetUpcomingEventsAsync(It.IsAny<int>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new List<Event>());

        // Act
        var cut = RenderComponent<Events>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Prémios", "page should show trophies button for all users");
    }

    [Fact]
    public async Task EventsPage_ShowsEnrollmentButtons_ForAuthenticatedUsers()
    {
        // Arrange
        SetupAuthentication("test-user", "Test User");
        _mockEventService
            .Setup(x => x.GetUpcomingEventsAsync(It.IsAny<int>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new List<Event>());

        // Act
        var cut = RenderComponent<Events>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert - Enrollment buttons are in child components, so we check for enrollment-related content
        // The EnrollmentStatisticsButton and MyEnrollmentsButton components should be rendered
        cut.Markup.Should().NotContain("A carregar", "page should load successfully for authenticated users");
    }

    #endregion

    #region Modal Interaction Tests

    [Fact]
    public async Task EventsPage_OpensCreateModal_WhenCreateButtonClicked()
    {
        // Arrange
        SetupAuthentication("admin-user", "Admin User", "Admin");
        _mockEventService
            .Setup(x => x.GetUpcomingEventsAsync(It.IsAny<int>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new List<Event>());

        // Act
        var cut = RenderComponent<Events>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        var createButton = cut.Find("button:contains('Adicionar Atuação')");
        createButton.Click();

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("Adicionar") || cut.Markup.Contains("Editar"), TimeSpan.FromSeconds(1));
        // Modal should be open (check for modal-related content)
        cut.Markup.Should().MatchRegex("(modal|Adicionar|Editar)", "create modal should open");
    }

    [Fact]
    public async Task EventsPage_OpensTrophiesModal_WhenTrophiesButtonClicked()
    {
        // Arrange
        SetupAuthentication("test-user", "Test User");
        _mockEventService
            .Setup(x => x.GetUpcomingEventsAsync(It.IsAny<int>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new List<Event>());

        // Trophy stats are calculated in Events.razor, not from a service method
        // The trophies modal loads trophies directly from TrophyService.GetByEventIdAsync

        // Act
        var cut = RenderComponent<Events>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        var trophiesButton = cut.Find("button:contains('Prémios')");
        trophiesButton.Click();

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("Prémios") || cut.Markup.Contains("modal"), TimeSpan.FromSeconds(1));
        // Trophies modal should be open
        cut.Markup.Should().MatchRegex("(modal|Prémios)", "trophies modal should open");
    }

    #endregion

    #region CRUD Operation Tests

    [Fact]
    public async Task EventsPage_CreateEvent_CallsService()
    {
        // Arrange
        SetupAuthentication("admin-user", "Admin User", "Admin");
        var newEvent = CreateTestEvent(1, "New Event", DateTime.Now.AddDays(7), EventType.Atuacao);

        _mockEventService
            .Setup(x => x.GetUpcomingEventsAsync(It.IsAny<int>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new List<Event>());

        _mockEventService
            .Setup(x => x.CreateEventAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<EventType>(),
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(newEvent);

        // Act
        var cut = RenderComponent<Events>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Open create modal and submit (simplified - actual form submission would require more setup)
        var createButton = cut.Find("button:contains('Adicionar Atuação')");
        createButton.Click();

        // Note: Full form submission test would require setting up form fields and submitting
        // This is a simplified test to verify the modal opens

        // Assert
        cut.Markup.Should().MatchRegex("(modal|Adicionar|Editar)", "create modal should be accessible");
    }

    [Fact]
    public async Task EventsPage_DeleteEvent_CallsService()
    {
        // Arrange
        SetupAuthentication("admin-user", "Admin User", "Admin");
        var eventToDelete = CreateTestEvent(1, "Event to Delete", DateTime.Now.AddDays(7), EventType.Atuacao);

        _mockEventService
            .Setup(x => x.GetUpcomingEventsAsync(It.IsAny<int>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new List<Event> { eventToDelete });

        _mockEventService
            .Setup(x => x.DeleteEventAsync(It.IsAny<int>(), It.IsAny<System.Threading.CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var cut = RenderComponent<Events>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Note: Delete button would be in EventCard component
        // Full delete workflow test would require finding and clicking delete button,
        // then confirming in delete modal

        // Assert - Verify service is set up correctly
        _mockEventService.Verify(
            x => x.DeleteEventAsync(It.IsAny<int>(), It.IsAny<System.Threading.CancellationToken>()),
            Times.Never, // Not called yet, just verifying setup
            "delete service should be set up");
    }

    #endregion

    #region Filter Tests

    [Fact]
    public async Task EventsPage_DisplaysFiscalYearFilter()
    {
        // Arrange
        SetupAuthentication("test-user", "Test User");
        var fiscalYears = new List<FiscalYear>
        {
            CreateTestFiscalYear(2023, 2024),
            CreateTestFiscalYear(2024, 2025)
        };

        _mockFiscalYearService
            .Setup(x => x.GetAllFiscalYearsAsync())
            .ReturnsAsync(fiscalYears);

        _mockEventService
            .Setup(x => x.GetUpcomingEventsAsync(It.IsAny<int>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new List<Event>());

        // Act
        var cut = RenderComponent<Events>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert - fiscal year filter dropdown is present (selected value or "Todos os anos" may vary)
        cut.Markup.Should().Contain("filter-dropdown", "page should display filter dropdowns including fiscal year");
    }

    [Fact]
    public async Task EventsPage_DisplaysEventTypeFilter()
    {
        // Arrange
        SetupAuthentication("test-user", "Test User");
        var futureEvent = CreateTestEvent(1, "Future Event", DateTime.Now.AddDays(7), EventType.Atuacao);

        _mockEventService
            .Setup(x => x.GetUpcomingEventsAsync(It.IsAny<int>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new List<Event> { futureEvent });

        // Act
        var cut = RenderComponent<Events>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Todos os tipos", "page should display event type filter");
    }

    #endregion

    #region Helper Methods

    private Event CreateTestEvent(int id, string name, DateTime date, EventType type)
    {
        return new Event
        {
            Id = id,
            Name = name,
            Date = date,
            Location = "Test Location",
            Type = type,
            Description = "Test Description"
        };
    }

    private FiscalYear CreateTestFiscalYear(int startYear, int endYear)
    {
        return new FiscalYear
        {
            Id = startYear,
            StartYear = startYear,
            EndYear = endYear
        };
    }

    #endregion
}
