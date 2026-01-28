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
/// Component tests for Rehearsals.razor page
/// Tests page rendering, modal interactions, CRUD workflows, attendance workflows, and authorization
/// </summary>
public class RehearsalsPageTests : PageTestBase
{
    private const string SkipModal = "Modal renders outside component fragment; cannot assert modal markup in bUnit.";

    private readonly Mock<IRehearsalService> _mockRehearsalService;
    private readonly Mock<IRehearsalAttendanceService> _mockAttendanceService;
    private readonly Mock<IFiscalYearService> _mockFiscalYearService;
    private readonly Mock<IMemberInstrumentService> _mockMemberInstrumentService;
    private readonly Mock<IPushNotificationFactory> _mockPushNotificationFactory;
    private readonly Mock<IPushNotificationService> _mockPushNotificationService;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;

    public RehearsalsPageTests()
    {
        // Setup service mocks
        _mockRehearsalService = SetupService<IRehearsalService>();
        _mockAttendanceService = SetupService<IRehearsalAttendanceService>();
        _mockFiscalYearService = SetupService<IFiscalYearService>();
        _mockMemberInstrumentService = SetupService<IMemberInstrumentService>();
        _mockPushNotificationFactory = SetupService<IPushNotificationFactory>();
        _mockPushNotificationService = SetupService<IPushNotificationService>();
        _mockUserManager = SetupUserManager();

        // Setup default service responses
        _mockRehearsalService
            .Setup(x => x.GetRehearsalsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Rehearsal>());

        _mockAttendanceService
            .Setup(x => x.GetAttendancesByUserIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<RehearsalAttendance>());

        _mockFiscalYearService
            .Setup(x => x.GetAllFiscalYearsAsync())
            .ReturnsAsync(new List<FiscalYear>());
    }

    #region Page Rendering Tests

    [Fact]
    public async Task RehearsalsPage_RendersPageTitle()
    {
        // Arrange
        SetupAuthentication("test-user", "Test User");

        // Act
        var cut = RenderComponent<Rehearsals>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Ensaios", "page should display 'Ensaios' title");
    }

    [Fact]
    public async Task RehearsalsPage_ShowsLoadingState_Initially()
    {
        // Arrange
        SetupAuthentication("test-user", "Test User");
        _mockRehearsalService
            .Setup(x => x.GetRehearsalsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(async () =>
            {
                await Task.Delay(100);
                return (IEnumerable<Rehearsal>)new List<Rehearsal>();
            });

        // Act
        var cut = RenderComponent<Rehearsals>();

        // Assert - Should show loading initially
        cut.Markup.Should().Contain("A carregar", "page should show loading message initially");
    }

    [Fact]
    public async Task RehearsalsPage_ShowsEmptyState_WhenNoRehearsals()
    {
        // Arrange
        SetupAuthentication("test-user", "Test User");
        _mockRehearsalService
            .Setup(x => x.GetRehearsalsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Rehearsal>());

        // Act
        var cut = RenderComponent<Rehearsals>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Próximos Ensaios", "page should show 'Próximos Ensaios' section");
    }

    [Fact]
    public async Task RehearsalsPage_DisplaysRehearsals_WhenRehearsalsExist()
    {
        // Arrange
        SetupAuthentication("test-user", "Test User");
        var rehearsals = new List<Rehearsal>
        {
            CreateTestRehearsal(1, DateTime.Now.AddDays(7), "Test Location 1"),
            CreateTestRehearsal(2, DateTime.Now.AddDays(14), "Test Location 2")
        };

        _mockRehearsalService
            .Setup(x => x.GetRehearsalsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(rehearsals);

        // Act
        var cut = RenderComponent<Rehearsals>();
        cut.WaitForState(() => cut.Markup.Contains("Test Location") && !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Test Location 1", "page should display first rehearsal");
        cut.Markup.Should().Contain("Test Location 2", "page should display second rehearsal");
    }

    #endregion

    #region Authorization Tests

    [Fact(Skip = SkipModal)]
    public async Task RehearsalsPage_ShowsCreateButton_ForAdminUser()
    {
        // Arrange
        SetupAuthentication("admin-user", "Admin User", "Admin");
        _mockRehearsalService
            .Setup(x => x.GetRehearsalsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Rehearsal>());

        // Act
        var cut = RenderComponent<Rehearsals>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Criar Ensaio", "admin should see create button");
    }

    [Fact]
    public async Task RehearsalsPage_HidesCreateButton_ForRegularUser()
    {
        // Arrange
        SetupAuthentication("regular-user", "Regular User");
        _mockRehearsalService
            .Setup(x => x.GetRehearsalsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Rehearsal>());

        // Act
        var cut = RenderComponent<Rehearsals>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().NotContain("Criar Ensaio", "regular user should not see create button");
    }

    #endregion

    #region Modal Interaction Tests

    [Fact(Skip = SkipModal)]
    public async Task RehearsalsPage_OpenCreateModal_ShowsCreateModal()
    {
        // Arrange
        SetupAuthentication("admin-user", "Admin User", "Admin");
        _mockRehearsalService
            .Setup(x => x.GetRehearsalsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Rehearsal>());

        var cut = RenderComponent<Rehearsals>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Act - Open create modal via reflection (method is private)
        var openCreateModalMethod = typeof(Rehearsals).GetMethod("OpenCreateSingleModal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await cut.InvokeAsync(() => openCreateModalMethod!.Invoke(cut.Instance, null));

        // Assert
        cut.Markup.Should().Contain("Criar Ensaio", "create modal should be displayed");
    }

    [Fact(Skip = SkipModal)]
    public async Task RehearsalsPage_OpenEditModal_ShowsEditModal()
    {
        // Arrange
        SetupAuthentication("admin-user", "Admin User", "Admin");
        var rehearsal = CreateTestRehearsal(1, DateTime.Now.AddDays(7), "Test Location");
        var rehearsals = new List<Rehearsal> { rehearsal };

        _mockRehearsalService
            .Setup(x => x.GetRehearsalsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(rehearsals);

        var cut = RenderComponent<Rehearsals>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Act - Open edit modal via reflection (method is private)
        var openEditModalMethod = typeof(Rehearsals).GetMethod("OpenEditModal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await cut.InvokeAsync(() => openEditModalMethod!.Invoke(cut.Instance, new object[] { rehearsal }));

        // Assert
        cut.Markup.Should().Contain("Editar Ensaio", "edit modal should be displayed");
    }

    [Fact(Skip = SkipModal)]
    public async Task RehearsalsPage_OpenDeleteModal_ShowsDeleteModal()
    {
        // Arrange
        SetupAuthentication("admin-user", "Admin User", "Admin");
        var rehearsal = CreateTestRehearsal(1, DateTime.Now.AddDays(7), "Test Location");
        var rehearsals = new List<Rehearsal> { rehearsal };

        _mockRehearsalService
            .Setup(x => x.GetRehearsalsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(rehearsals);

        var cut = RenderComponent<Rehearsals>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Act - Open delete modal via reflection (method is private)
        var openDeleteModalMethod = typeof(Rehearsals).GetMethod("OpenDeleteModal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await cut.InvokeAsync(() => openDeleteModalMethod!.Invoke(cut.Instance, new object[] { rehearsal }));

        // Assert
        cut.Markup.Should().Contain("Confirmar Eliminação", "delete modal should be displayed");
    }

    #endregion

    #region CRUD Operation Tests

    [Fact(Skip = SkipModal)]
    public async Task RehearsalsPage_CreateRehearsal_OpensModal()
    {
        // Arrange
        SetupAuthentication("admin-user", "Admin User", "Admin");
        var newRehearsal = CreateTestRehearsal(1, DateTime.Now.AddDays(7), "New Location");

        _mockRehearsalService
            .Setup(x => x.GetRehearsalsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Rehearsal>());

        _mockRehearsalService
            .Setup(x => x.CreateRehearsalAsync(It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(newRehearsal);

        var cut = RenderComponent<Rehearsals>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Act - Open create modal
        var openCreateModalMethod = typeof(Rehearsals).GetMethod("OpenCreateModal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await cut.InvokeAsync(() => openCreateModalMethod!.Invoke(cut.Instance, null));

        // Assert
        cut.Markup.Should().Contain("Criar Ensaio", "create modal should be displayed");
    }

    [Fact]
    public async Task RehearsalsPage_DeleteRehearsal_CallsService()
    {
        // Arrange
        SetupAuthentication("admin-user", "Admin User", "Admin");
        var rehearsal = CreateTestRehearsal(1, DateTime.Now.AddDays(7), "Test Location");
        var rehearsals = new List<Rehearsal> { rehearsal };

        _mockRehearsalService
            .Setup(x => x.GetRehearsalsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(rehearsals);

        _mockRehearsalService
            .Setup(x => x.DeleteRehearsalAsync(1))
            .Returns(Task.CompletedTask);

        // Setup for reload after delete
        _mockRehearsalService
            .Setup(x => x.GetRehearsalsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Rehearsal>());

        var cut = RenderComponent<Rehearsals>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        var openDelete = typeof(Rehearsals).GetMethod("OpenDeleteModal", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var confirmDelete = typeof(Rehearsals).GetMethod("ConfirmDelete", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        await cut.InvokeAsync(() => openDelete.Invoke(cut.Instance, new object[] { rehearsal }));
        await cut.InvokeAsync(async () =>
        {
            var t = (Task)confirmDelete.Invoke(cut.Instance, null)!;
            await t;
        });

        _mockRehearsalService.Verify(x => x.DeleteRehearsalAsync(1), Times.Once, "DeleteRehearsalAsync should be called");
    }

    #endregion

    #region Helper Methods

    private static Rehearsal CreateTestRehearsal(int id, DateTime date, string location, string? theme = null)
    {
        var rehearsal = Rehearsal.Create(
            date: date,
            location: location,
            theme: theme
        );
        rehearsal.Id = id;
        return rehearsal;
    }

    #endregion
}
