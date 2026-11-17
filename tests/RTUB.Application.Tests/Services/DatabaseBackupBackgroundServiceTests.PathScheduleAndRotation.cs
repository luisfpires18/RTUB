using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;

namespace RTUB.Application.Tests.Services;

// DatabaseBackupBackgroundServiceTests: Path Schedule And Rotation tests.
public partial class DatabaseBackupBackgroundServiceTests
{
    [Fact]
    public void ResolveDatabasePath_ExistingFile_ReturnsFullPath()
    {
        // Arrange
        var source = CreateSeededDatabase(rowCount: 1);

        // Act
        var result = DatabaseBackupBackgroundService.ResolveDatabasePath($"Data Source={source}");

        // Assert
        result.Path.Should().Be(Path.GetFullPath(source));
        result.SkipReason.Should().BeNull();
    }

    [Fact]
    public void ResolveDatabasePath_InMemory_IsSkipped()
    {
        // Act
        var result = DatabaseBackupBackgroundService.ResolveDatabasePath("Data Source=:memory:");

        // Assert
        result.Path.Should().BeNull();
        result.SkipReason.Should().Contain("in-memory");
    }

    [Fact]
    public void ResolveDatabasePath_MissingFile_IsSkipped()
    {
        // Act
        var result = DatabaseBackupBackgroundService.ResolveDatabasePath($"Data Source={NewTempPath()}");

        // Assert
        result.Path.Should().BeNull();
        result.SkipReason.Should().Contain("no database file");
    }



    [Fact]
    public void CalculateNextRunTime_TimeLaterToday_SchedulesToday()
    {
        // Arrange
        var now = new DateTime(2026, 9, 6, 1, 0, 0, DateTimeKind.Utc);

        // Act
        var next = DatabaseBackupBackgroundService.CalculateNextRunTime("03:30", now);

        // Assert
        next.Should().Be(new DateTime(2026, 9, 6, 3, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void CalculateNextRunTime_TimeAlreadyPassed_SchedulesTomorrow()
    {
        // Arrange
        var now = new DateTime(2026, 9, 6, 5, 0, 0, DateTimeKind.Utc);

        // Act
        var next = DatabaseBackupBackgroundService.CalculateNextRunTime("03:30", now);

        // Assert
        next.Should().Be(new DateTime(2026, 9, 7, 3, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void CalculateNextRunTime_InvalidFormat_FallsBackToDefault()
    {
        // Arrange
        var now = new DateTime(2026, 9, 6, 1, 0, 0, DateTimeKind.Utc);

        // Act
        var next = DatabaseBackupBackgroundService.CalculateNextRunTime("not-a-time", now);

        // Assert
        next.TimeOfDay.Should().Be(DatabaseBackupBackgroundService.DefaultScheduledTime);
    }



    [Fact]
    public async Task RotateAsync_WithExistingCurrent_DemotesThenPromotesThenCleansUp()
    {
        // Arrange
        var storage = new Mock<IDatabaseBackupStorageService>();
        storage.Setup(s => s.ExistsAsync(_options.CurrentKey)).ReturnsAsync(true);

        var copies = new List<(string Source, string Destination)>();
        storage.Setup(s => s.CopyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((src, dst, _) => copies.Add((src, dst)))
            .Returns(Task.CompletedTask);

        var service = CreateService();

        // Act
        await service.RotateAsync(storage.Object);

        // Assert - order matters: the old current must be preserved before it is overwritten
        copies.Should().HaveCount(2);
        copies[0].Should().Be((_options.CurrentKey, _options.PreviousKey));
        copies[1].Should().Be((_options.StagingKey, _options.CurrentKey));
        storage.Verify(s => s.DeleteAsync(_options.StagingKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RotateAsync_FirstEverBackup_SkipsDemotion()
    {
        // Arrange
        var storage = new Mock<IDatabaseBackupStorageService>();
        storage.Setup(s => s.ExistsAsync(_options.CurrentKey)).ReturnsAsync(false);

        var copies = new List<(string Source, string Destination)>();
        storage.Setup(s => s.CopyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((src, dst, _) => copies.Add((src, dst)))
            .Returns(Task.CompletedTask);

        var service = CreateService();

        // Act
        await service.RotateAsync(storage.Object);

        // Assert
        copies.Should().ContainSingle().Which.Should().Be((_options.StagingKey, _options.CurrentKey));
        storage.Verify(s => s.CopyAsync(_options.CurrentKey, _options.PreviousKey, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RotateAsync_PromotionFails_LeavesCurrentUntouchedAndPropagates()
    {
        // Arrange
        var storage = new Mock<IDatabaseBackupStorageService>();
        storage.Setup(s => s.ExistsAsync(_options.CurrentKey)).ReturnsAsync(false);
        storage.Setup(s => s.CopyAsync(_options.StagingKey, _options.CurrentKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("R2 unavailable"));

        var service = CreateService();

        // Act
        var act = async () => await service.RotateAsync(storage.Object);

        // Assert - the failure surfaces and the staging object is not cleaned up,
        // so nothing silently reports success
        await act.Should().ThrowAsync<InvalidOperationException>();
        storage.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RotateAsync_StagingDeleteFails_DoesNotFailTheBackup()
    {
        // Arrange - the backup already succeeded; a leftover staging object is harmless
        var storage = new Mock<IDatabaseBackupStorageService>();
        storage.Setup(s => s.ExistsAsync(_options.CurrentKey)).ReturnsAsync(true);
        storage.Setup(s => s.CopyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        storage.Setup(s => s.DeleteAsync(_options.StagingKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("delete failed"));

        var service = CreateService();

        // Act
        var act = async () => await service.RotateAsync(storage.Object);

        // Assert
        await act.Should().NotThrowAsync();
    }



    private DatabaseBackupBackgroundService CreateService()
    {
        return new DatabaseBackupBackgroundService(
            Mock.Of<ILogger<DatabaseBackupBackgroundService>>(),
            Mock.Of<IServiceScopeFactory>(),
            new ConfigurationBuilder().Build(),
            Options.Create(_options));
    }
}
