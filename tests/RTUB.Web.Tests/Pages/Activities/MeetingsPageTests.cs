using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Pages.Activities;
using RTUB.Web.Tests.Pages.Base;

namespace RTUB.Web.Tests.Pages.Activities;

/// <summary>
/// Component tests for Meetings.razor page
/// Tests page rendering, modal interactions, CRUD workflows, ATA workflows, and authorization
/// </summary>
public class MeetingsPageTests : PageTestBase
{
    private const string SkipModal = "Modal renders outside component fragment; cannot assert modal markup in bUnit.";

    private readonly Mock<IMeetingService> _mockMeetingService;
    private readonly Mock<IMeetingRequestService> _mockMeetingRequestService;
    private readonly Mock<IMeetingParticipationService> _mockMeetingParticipationService;
    private readonly Mock<IEmailNotificationService> _mockEmailNotificationService;
    private readonly Mock<IEmailTemplateRenderer> _mockEmailTemplateRenderer;
    private readonly Mock<IFiscalYearService> _mockFiscalYearService;
    private readonly Mock<IPushNotificationFactory> _mockPushNotificationFactory;
    private readonly Mock<IPushNotificationService> _mockPushNotificationService;
    private readonly Mock<IMeetingAtaService> _mockMeetingAtaService;
    private readonly Mock<IMeetingAtaConfirmationService> _mockMeetingAtaConfirmationService;
    private readonly Mock<IAtaPdfService> _mockAtaPdfService;
    private readonly Mock<IDocumentStorageService> _mockDocumentStorageService;
    private readonly Mock<IDocumentationService> _mockDocumentationService;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<Microsoft.Extensions.Hosting.IHostEnvironment> _mockHostEnvironment;

    public MeetingsPageTests()
    {
        // Setup service mocks
        _mockMeetingService = SetupService<IMeetingService>();
        _mockMeetingRequestService = SetupService<IMeetingRequestService>();
        _mockMeetingParticipationService = SetupService<IMeetingParticipationService>();
        _mockEmailNotificationService = SetupService<IEmailNotificationService>();
        _mockEmailTemplateRenderer = SetupService<IEmailTemplateRenderer>();
        _mockFiscalYearService = SetupService<IFiscalYearService>();
        _mockPushNotificationFactory = SetupService<IPushNotificationFactory>();
        _mockPushNotificationService = SetupService<IPushNotificationService>();
        _mockMeetingAtaService = SetupService<IMeetingAtaService>();
        _mockMeetingAtaConfirmationService = SetupService<IMeetingAtaConfirmationService>();
        _mockAtaPdfService = SetupService<IAtaPdfService>();
        _mockDocumentStorageService = SetupService<IDocumentStorageService>();
        _mockDocumentationService = SetupService<IDocumentationService>();
        _mockUserManager = SetupUserManager();
        _mockHostEnvironment = SetupService<Microsoft.Extensions.Hosting.IHostEnvironment>();

        // Setup default service responses
        _mockMeetingService
            .Setup(x => x.GetAllMeetingsAsync(It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(new List<Meeting>());

        _mockMeetingRequestService
            .Setup(x => x.GetAllAsync(It.IsAny<RequestStatus?>()))
            .ReturnsAsync(new List<MeetingRequest>());

        _mockMeetingParticipationService
            .Setup(x => x.GetParticipationsByUserIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<MeetingParticipation>());

        _mockMeetingParticipationService
            .Setup(x => x.GetParticipationCountsByMeetingIdsAsync(It.IsAny<List<int>>()))
            .ReturnsAsync(new Dictionary<int, int>());

        _mockFiscalYearService
            .Setup(x => x.GetAllFiscalYearsAsync())
            .ReturnsAsync(new List<FiscalYear>());
    }

    #region Page Rendering Tests

    [Fact]
    public async Task MeetingsPage_RendersPageTitle()
    {
        // Arrange
        SetupAuthentication("test-user", "Test User");

        // Act
        var cut = RenderComponent<Meetings>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Reuniões", "page should display 'Reuniões' title");
    }

    [Fact]
    public async Task MeetingsPage_ShowsLoadingState_Initially()
    {
        // Arrange
        SetupAuthentication("test-user", "Test User");
        _mockMeetingService
            .Setup(x => x.GetAllMeetingsAsync(It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
            .Returns(async () =>
            {
                await Task.Delay(100);
                return (IEnumerable<Meeting>)new List<Meeting>();
            });

        // Act
        var cut = RenderComponent<Meetings>();

        // Assert - Should show loading initially
        cut.Markup.Should().Contain("A carregar", "page should show loading message initially");
    }

    [Fact]
    public async Task MeetingsPage_ShowsEmptyState_WhenNoMeetings()
    {
        // Arrange
        SetupAuthentication("test-user", "Test User");
        _mockMeetingService
            .Setup(x => x.GetAllMeetingsAsync(It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(new List<Meeting>());

        // Act
        var cut = RenderComponent<Meetings>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Nenhuma reunião agendada", "page should show empty state message");
    }

    [Fact]
    public async Task MeetingsPage_DisplaysMeetings_WhenMeetingsExist()
    {
        // Arrange
        SetupAuthentication("test-user", "Test User");
        var meetings = new List<Meeting>
        {
            CreateTestMeeting(1, "Test Meeting 1", DateTime.Now.AddDays(7)),
            CreateTestMeeting(2, "Test Meeting 2", DateTime.Now.AddDays(14))
        };

        _mockMeetingService
            .Setup(x => x.GetAllMeetingsAsync(It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(meetings);

        // Act
        var cut = RenderComponent<Meetings>();
        cut.WaitForState(() => cut.Markup.Contains("Test Meeting") && !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Test Meeting 1", "page should display first meeting");
        cut.Markup.Should().Contain("Test Meeting 2", "page should display second meeting");
    }

    #endregion

    #region Authorization Tests

    [Fact(Skip = SkipModal)]
    public async Task MeetingsPage_ShowsCreateButton_ForAdminUser()
    {
        // Arrange
        SetupAuthentication("admin-user", "Admin User", "Admin");
        _mockMeetingService
            .Setup(x => x.GetAllMeetingsAsync(It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(new List<Meeting>());

        // Act
        var cut = RenderComponent<Meetings>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Criar Reunião", "admin should see create button");
    }

    [Fact]
    public async Task MeetingsPage_HidesCreateButton_ForRegularUser()
    {
        // Arrange
        SetupAuthentication("regular-user", "Regular User");
        _mockMeetingService
            .Setup(x => x.GetAllMeetingsAsync(It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(new List<Meeting>());

        // Act
        var cut = RenderComponent<Meetings>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().NotContain("Criar Reunião", "regular user should not see create button");
    }

    #endregion

    #region Modal Interaction Tests

    [Fact(Skip = SkipModal)]
    public async Task MeetingsPage_OpenCreateModal_ShowsCreateModal()
    {
        // Arrange
        SetupAuthentication("admin-user", "Admin User", "Admin");
        _mockMeetingService
            .Setup(x => x.GetAllMeetingsAsync(It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(new List<Meeting>());

        var cut = RenderComponent<Meetings>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Act - Open create modal via reflection (method is private)
        var openCreateModalMethod = typeof(Meetings).GetMethod("OpenCreateModal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await cut.InvokeAsync(() => openCreateModalMethod!.Invoke(cut.Instance, null));

        // Assert
        cut.Markup.Should().Contain("Criar Reunião", "create modal should be displayed");
    }

    [Fact(Skip = SkipModal)]
    public async Task MeetingsPage_OpenEditModal_ShowsEditModal()
    {
        // Arrange
        SetupAuthentication("admin-user", "Admin User", "Admin");
        var meeting = CreateTestMeeting(1, "Test Meeting", DateTime.Now.AddDays(7));
        var meetings = new List<Meeting> { meeting };

        _mockMeetingService
            .Setup(x => x.GetAllMeetingsAsync(It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(meetings);

        var cut = RenderComponent<Meetings>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Act - Open edit modal via reflection (method is private)
        var openEditModalMethod = typeof(Meetings).GetMethod("OpenEditModal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await cut.InvokeAsync(() => openEditModalMethod!.Invoke(cut.Instance, new object[] { meeting }));

        // Assert
        cut.Markup.Should().Contain("Editar Reunião", "edit modal should be displayed");
    }

    [Fact(Skip = SkipModal)]
    public async Task MeetingsPage_OpenDeleteModal_ShowsDeleteModal()
    {
        // Arrange
        SetupAuthentication("admin-user", "Admin User", "Admin");
        var meeting = CreateTestMeeting(1, "Test Meeting", DateTime.Now.AddDays(7));
        var meetings = new List<Meeting> { meeting };

        _mockMeetingService
            .Setup(x => x.GetAllMeetingsAsync(It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(meetings);

        var cut = RenderComponent<Meetings>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Act - Open delete modal via reflection (method is private)
        var openDeleteModalMethod = typeof(Meetings).GetMethod("OpenDeleteModal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await cut.InvokeAsync(() => openDeleteModalMethod!.Invoke(cut.Instance, new object[] { meeting }));

        // Assert
        cut.Markup.Should().Contain("Confirmar Eliminação", "delete modal should be displayed");
    }

    #endregion

    #region CRUD Operation Tests

    [Fact(Skip = SkipModal)]
    public async Task MeetingsPage_CreateMeeting_OpensModal()
    {
        // Arrange
        SetupAuthentication("admin-user", "Admin User", "Admin");
        var newMeeting = CreateTestMeeting(1, "New Meeting", DateTime.Now.AddDays(7));

        _mockMeetingService
            .Setup(x => x.GetAllMeetingsAsync(It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(new List<Meeting>());

        _mockMeetingService
            .Setup(x => x.CreateMeetingAsync(It.IsAny<Meeting>()))
            .ReturnsAsync(newMeeting);

        var cut = RenderComponent<Meetings>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Act - Open create modal
        var openCreateModalMethod = typeof(Meetings).GetMethod("OpenCreateModal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await cut.InvokeAsync(() => openCreateModalMethod!.Invoke(cut.Instance, null));

        // Assert
        cut.Markup.Should().Contain("Criar Reunião", "create modal should be displayed");
    }

    [Fact]
    public async Task MeetingsPage_DeleteMeeting_CallsService()
    {
        // Arrange
        SetupAuthentication("admin-user", "Admin User", "Admin");
        var meeting = CreateTestMeeting(1, "Test Meeting", DateTime.Now.AddDays(7));
        var meetings = new List<Meeting> { meeting };

        _mockMeetingService
            .Setup(x => x.GetAllMeetingsAsync(It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(meetings);

        _mockMeetingService
            .Setup(x => x.DeleteMeetingAsync(1))
            .Returns(Task.CompletedTask);

        // Setup for reload after delete
        _mockMeetingService
            .Setup(x => x.GetAllMeetingsAsync(It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(new List<Meeting>());

        var cut = RenderComponent<Meetings>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        var openDelete = typeof(Meetings).GetMethod("OpenDeleteModal", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var deleteMeeting = typeof(Meetings).GetMethod("DeleteMeeting", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        await cut.InvokeAsync(() => openDelete.Invoke(cut.Instance, new object[] { meeting }));
        await cut.InvokeAsync(async () =>
        {
            var t = (Task)deleteMeeting.Invoke(cut.Instance, null)!;
            await t;
        });

        _mockMeetingService.Verify(x => x.DeleteMeetingAsync(1), Times.Once, "DeleteMeetingAsync should be called");
    }

    #endregion

    #region Helper Methods

    private static Meeting CreateTestMeeting(int id, string title, DateTime date, MeetingType type = MeetingType.AssembleiaGeralOrdinaria)
    {
        var meeting = new Meeting
        {
            Id = id,
            Type = type,
            Title = title,
            Date = date,
            Location = "Test Location",
            Statement = $"Statement for {title}",
            OrganizerUserId = "test-organizer-id"
        };
        return meeting;
    }

    #endregion
}
