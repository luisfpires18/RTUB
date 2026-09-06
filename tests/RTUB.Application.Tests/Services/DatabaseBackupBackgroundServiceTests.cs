using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Configuration;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for DatabaseBackupBackgroundService: snapshot creation, integrity
/// validation, path resolution, scheduling and backup rotation.
/// </summary>
public class DatabaseBackupBackgroundServiceTests : IDisposable
{
    private readonly List<string> _tempFiles = new();
    private readonly DatabaseBackupOptions _options = new()
    {
        Enabled = true,
        Bucket = "rtub-db-backups",
        CurrentKey = "database/current.db",
        PreviousKey = "database/previous.db",
        StagingKey = "database/incoming.db"
    };

    public void Dispose()
    {
        foreach (var connection in _openConnections)
        {
            connection.Close();
            connection.Dispose();
        }

        // Sqlite pools connections; release the handles so the temp files can be deleted.
        SqliteConnection.ClearAllPools();

        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file)) File.Delete(file);
            }
            catch (IOException)
            {
                // Best effort - a leaked temp file must not fail the test run.
            }
        }

        GC.SuppressFinalize(this);
    }

    #region CreateSnapshot

    [Fact]
    public void CreateSnapshot_WalDatabaseWithData_ProducesReadableCopy()
    {
        // Arrange
        var source = CreateSeededDatabase(rowCount: 25);
        var destination = NewTempPath();

        // Act
        DatabaseBackupBackgroundService.CreateSnapshot(source, destination);

        // Assert
        File.Exists(destination).Should().BeTrue();
        CountRows(destination).Should().Be(25);
    }

    [Fact]
    public void CreateSnapshot_DoesNotCopyWalOrShmSidecars()
    {
        // Arrange - an open connection keeps the -wal sidecar present on disk
        var source = CreateSeededDatabase(rowCount: 5, keepWalOpen: true);
        var destination = NewTempPath();

        // Act
        DatabaseBackupBackgroundService.CreateSnapshot(source, destination);

        // Assert - the snapshot is a single self-contained file
        File.Exists(destination + "-wal").Should().BeFalse();
        File.Exists(destination + "-shm").Should().BeFalse();
        CountRows(destination).Should().Be(5);
    }

    [Fact]
    public void CreateSnapshot_CapturesRowsCommittedWhileWalIsActive()
    {
        // Arrange - rows still sitting in the WAL must appear in the snapshot,
        // which is exactly what a raw file copy of app.db would miss.
        var source = CreateSeededDatabase(rowCount: 10, keepWalOpen: true);
        AppendRows(source, 7);
        var destination = NewTempPath();

        // Act
        DatabaseBackupBackgroundService.CreateSnapshot(source, destination);

        // Assert
        CountRows(destination).Should().Be(17);
    }

    #endregion

    #region ValidateSnapshot

    [Fact]
    public void ValidateSnapshot_HealthySnapshot_IsValid()
    {
        // Arrange
        var source = CreateSeededDatabase(rowCount: 10);
        var snapshot = NewTempPath();
        DatabaseBackupBackgroundService.CreateSnapshot(source, snapshot);
        var liveSize = new FileInfo(source).Length;

        // Act
        var result = DatabaseBackupBackgroundService.ValidateSnapshot(snapshot, liveSize, 0.5);

        // Assert
        result.IsValid.Should().BeTrue();
        result.FailureReason.Should().BeNull();
    }

    [Fact]
    public void ValidateSnapshot_MissingFile_IsInvalid()
    {
        // Arrange
        var missing = NewTempPath();

        // Act
        var result = DatabaseBackupBackgroundService.ValidateSnapshot(missing, 1000, 0.5);

        // Assert
        result.IsValid.Should().BeFalse();
        result.FailureReason.Should().Contain("was not created");
    }

    [Fact]
    public void ValidateSnapshot_EmptyFile_IsInvalid()
    {
        // Arrange
        var empty = NewTempPath();
        File.WriteAllBytes(empty, Array.Empty<byte>());

        // Act
        var result = DatabaseBackupBackgroundService.ValidateSnapshot(empty, 1000, 0.5);

        // Assert
        result.IsValid.Should().BeFalse();
        result.FailureReason.Should().Contain("empty");
    }

    [Fact]
    public void ValidateSnapshot_CorruptFile_IsInvalid()
    {
        // Arrange - a valid SQLite header followed by garbage
        var corrupt = NewTempPath();
        var bytes = new byte[8192];
        "SQLite format 3\0"u8.ToArray().CopyTo(bytes, 0);
        Random.Shared.NextBytes(bytes.AsSpan(16));
        File.WriteAllBytes(corrupt, bytes);

        // Act
        var result = DatabaseBackupBackgroundService.ValidateSnapshot(corrupt, 8192, 0.5);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateSnapshot_TruncatedRelativeToLiveDatabase_IsInvalid()
    {
        // Arrange - internally consistent but far too small for the live database,
        // which quick_check alone would happily accept
        var source = CreateSeededDatabase(rowCount: 10);
        var snapshot = NewTempPath();
        DatabaseBackupBackgroundService.CreateSnapshot(source, snapshot);
        var overstatedLiveSize = new FileInfo(snapshot).Length * 10;

        // Act
        var result = DatabaseBackupBackgroundService.ValidateSnapshot(snapshot, overstatedLiveSize, 0.5);

        // Assert
        result.IsValid.Should().BeFalse();
        result.FailureReason.Should().Contain("threshold");
    }

    [Fact]
    public void ValidateSnapshot_EmptySchema_IsInvalid()
    {
        // Arrange - a valid, non-empty database file that holds no tables. Creating and
        // dropping a table forces SQLite to write the header without leaving any schema.
        var empty = NewTempPath();
        using (var connection = new SqliteConnection($"Data Source={empty}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE Placeholder (Id INTEGER); DROP TABLE Placeholder;";
            command.ExecuteNonQuery();
        }
        SqliteConnection.ClearAllPools();
        new FileInfo(empty).Length.Should().BeGreaterThan(0, "the empty-file check must not be what fails here");

        // Act - liveSize 0 disables the ratio check so the schema check is what runs
        var result = DatabaseBackupBackgroundService.ValidateSnapshot(empty, 0, 0.5);

        // Assert
        result.IsValid.Should().BeFalse();
        result.FailureReason.Should().Contain("no tables");
    }

    #endregion

    #region ResolveDatabasePath

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

    #endregion

    #region CalculateNextRunTime

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

    #endregion

    #region RotateAsync

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

    #endregion

    #region Helpers

    private DatabaseBackupBackgroundService CreateService()
    {
        return new DatabaseBackupBackgroundService(
            Mock.Of<ILogger<DatabaseBackupBackgroundService>>(),
            Mock.Of<IServiceScopeFactory>(),
            new ConfigurationBuilder().Build(),
            Options.Create(_options));
    }

    private string NewTempPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"rtub-test-{Guid.NewGuid():N}.db");
        _tempFiles.Add(path);
        _tempFiles.Add(path + "-wal");
        _tempFiles.Add(path + "-shm");
        return path;
    }

    /// <summary>
    /// Creates a WAL-mode SQLite database with a seeded table, mirroring how the
    /// application configures its own connections.
    /// </summary>
    private string CreateSeededDatabase(int rowCount, bool keepWalOpen = false)
    {
        var path = NewTempPath();

        var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();

        using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA journal_mode = WAL;";
            pragma.ExecuteNonQuery();
        }

        using (var create = connection.CreateCommand())
        {
            create.CommandText = "CREATE TABLE Members (Id INTEGER PRIMARY KEY, Nickname TEXT NOT NULL);";
            create.ExecuteNonQuery();
        }

        InsertRows(connection, rowCount, startAt: 1);

        if (keepWalOpen)
        {
            // Leave the connection open so the -wal sidecar stays on disk.
            _openConnections.Add(connection);
        }
        else
        {
            connection.Close();
            connection.Dispose();
            SqliteConnection.ClearAllPools();
        }

        return path;
    }

    private readonly List<SqliteConnection> _openConnections = new();

    private void AppendRows(string path, int count)
    {
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        InsertRows(connection, count, startAt: 1000);
    }

    private static void InsertRows(SqliteConnection connection, int count, int startAt)
    {
        using var transaction = connection.BeginTransaction();
        for (var i = 0; i < count; i++)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO Members (Id, Nickname) VALUES ($id, $nickname);";
            insert.Parameters.AddWithValue("$id", startAt + i);
            insert.Parameters.AddWithValue("$nickname", $"tuno-{startAt + i}");
            insert.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    private static long CountRows(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Members;";
        return Convert.ToInt64(command.ExecuteScalar());
    }

    #endregion
}
