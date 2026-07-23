using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Web.Services;
using RTUB.Web.Tests.Pages.Base;
using AuditLogPage = RTUB.Pages.Operations.AuditLog;

namespace RTUB.Web.Tests.Pages.Operations;

public class AuditLogPageTests : PageTestBase
{
    private const string SkipModal = "Modal renders outside component fragment; cannot assert modal markup in bUnit.";
    private readonly Mock<IAuditLogService> _mockAuditLogService;
    private readonly Mock<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>> _mockUserManager;

    public AuditLogPageTests()
    {
        _mockAuditLogService = SetupService<IAuditLogService>();
        _mockUserManager = SetupUserManager();
        Services.AddSingleton(new AnnouncementService());
    }

    #region Page Rendering Tests

    [Fact]
    public async Task AuditLogPage_RendersPageTitle()
    {
        // Arrange
        SetupAuthentication("owner-user", "Owner User", "Owner");
        SetupEmptyServiceData();

        // Act
        var component = Render<AuditLogPage>();
        component.WaitForState(() => !component.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        component.Markup.Should().Contain("Histórico de atividades");
    }

    [Fact]
    public async Task AuditLogPage_ShowsLoadingState_Initially()
    {
        // Arrange
        SetupAuthentication("owner-user", "Owner User", "Owner");

        // Setup delayed service responses to ensure loading state is visible
        var tcs = new TaskCompletionSource<IEnumerable<string>>();
        _mockAuditLogService
            .Setup(x => x.GetUserNamesAsync())
            .Returns(tcs.Task);

        _mockAuditLogService
            .Setup(x => x.GetEntityTypesAsync())
            .ReturnsAsync(Enumerable.Empty<string>());

        _mockAuditLogService
            .Setup(x => x.GetActionTypesAsync())
            .ReturnsAsync(Enumerable.Empty<string>());

        _mockAuditLogService
            .Setup(x => x.GetPagedWithCountAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<bool?>(),
                It.IsAny<int>(),
                It.IsAny<int>()))
            .ReturnsAsync((Enumerable.Empty<AuditLog>(), 0));

        // Act
        var component = Render<AuditLogPage>();

        // Assert - Check loading state before async operations complete
        component.Markup.Should().Contain("A carregar");

        // Complete the delayed task to allow test cleanup
        tcs.SetResult(Enumerable.Empty<string>());
        component.WaitForState(() => !component.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task AuditLogPage_ShowsEmptyState_WhenNoLogs()
    {
        // Arrange
        SetupAuthentication("owner-user", "Owner User", "Owner");
        SetupEmptyServiceData();

        // Act
        var component = Render<AuditLogPage>();
        component.WaitForState(() => !component.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        component.Markup.Should().Contain("No audit logs found matching the filters");
    }

    [Fact]
    public async Task AuditLogPage_DisplaysLogs_WhenLogsExist()
    {
        // Arrange
        SetupAuthentication("owner-user", "Owner User", "Owner");
        var logs = CreateTestAuditLogs(3);
        SetupServiceData(logs);

        // Act
        var component = Render<AuditLogPage>();
        component.WaitForState(() => !component.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        component.Markup.Should().Contain("Activity Log");
        component.Markup.Should().Contain("Event");
        component.Markup.Should().Contain("Created");
    }

    [Fact]
    public async Task AuditLogPage_DisplaysStatistics()
    {
        // Arrange
        SetupAuthentication("owner-user", "Owner User", "Owner");
        var logs = CreateTestAuditLogs(5);
        SetupServiceData(logs);

        // Act
        var component = Render<AuditLogPage>();
        component.WaitForState(() => !component.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        component.Markup.Should().Contain("Total Registos");
        component.Markup.Should().Contain("Critical Actions");
        component.Markup.Should().Contain("Entity Types");
        component.Markup.Should().Contain("Active Users");
    }

    [Fact]
    public async Task AuditLogPage_ShowsFiltersSection()
    {
        // Arrange
        SetupAuthentication("owner-user", "Owner User", "Owner");
        SetupServiceData(CreateTestAuditLogs(2));

        // Act
        var component = Render<AuditLogPage>();
        component.WaitForState(() => !component.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        component.Markup.Should().Contain("Filtros");
        component.Markup.Should().Contain("User");
        component.Markup.Should().Contain("Entity Type");
        component.Markup.Should().Contain("Action");
    }

    [Fact]
    public async Task AuditLogPage_ShowsExportButton_ForOwner()
    {
        // Arrange
        SetupAuthentication("owner-user", "Owner User", "Owner");
        SetupServiceData(CreateTestAuditLogs(1));

        // Act
        var component = Render<AuditLogPage>();
        component.WaitForState(() => !component.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        component.Markup.Should().Contain("Export JSON");
    }

    [Fact]
    public async Task AuditLogPage_ShowsTruncateButton_ForOwner()
    {
        // Arrange
        SetupAuthentication("owner-user", "Owner User", "Owner");
        SetupServiceData(CreateTestAuditLogs(1));

        // Act
        var component = Render<AuditLogPage>();
        component.WaitForState(() => !component.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        component.Markup.Should().Contain("Truncate All");
    }

    #endregion

    #region Helper Methods


    private void SetupEmptyServiceData()
    {
        _mockAuditLogService
            .Setup(x => x.GetUserNamesAsync())
            .ReturnsAsync(Enumerable.Empty<string>());

        _mockAuditLogService
            .Setup(x => x.GetEntityTypesAsync())
            .ReturnsAsync(Enumerable.Empty<string>());

        _mockAuditLogService
            .Setup(x => x.GetActionTypesAsync())
            .ReturnsAsync(Enumerable.Empty<string>());

        _mockAuditLogService
            .Setup(x => x.GetPagedWithCountAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<bool?>(),
                It.IsAny<int>(),
                It.IsAny<int>()))
            .ReturnsAsync((Enumerable.Empty<AuditLog>(), 0));
    }

    private void SetupServiceData(List<AuditLog> logs)
    {
        _mockAuditLogService
            .Setup(x => x.GetUserNamesAsync())
            .ReturnsAsync(logs.Select(l => l.UserName ?? "System").Distinct());

        _mockAuditLogService
            .Setup(x => x.GetEntityTypesAsync())
            .ReturnsAsync(logs.Select(l => l.EntityType).Distinct());

        _mockAuditLogService
            .Setup(x => x.GetActionTypesAsync())
            .ReturnsAsync(logs.Select(l => l.Action).Distinct());

        _mockAuditLogService
            .Setup(x => x.GetPagedWithCountAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<bool?>(),
                It.IsAny<int>(),
                It.IsAny<int>()))
            .ReturnsAsync((logs, logs.Count));
    }

    private static List<AuditLog> CreateTestAuditLogs(int count)
    {
        var logs = new List<AuditLog>();
        for (int i = 1; i <= count; i++)
        {
            logs.Add(new AuditLog
            {
                Id = i,
                EntityType = "Event",
                EntityId = i,
                Action = i == 1 ? "Created" : "Modified",
                UserId = $"user-{i}",
                UserName = $"user{i}",
                Timestamp = DateTime.UtcNow.AddDays(-i),
                Changes = $"{{\"Name\": \"Event {i}\"}}",
                EntityDisplayName = $"Event {i}",
                IsCriticalAction = i == 1
            });
        }
        return logs;
    }

    #endregion
}
