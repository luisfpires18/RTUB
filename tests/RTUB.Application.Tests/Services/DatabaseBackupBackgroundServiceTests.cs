using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Configuration;
using RTUB.Application.Services;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for DatabaseBackupBackgroundService: snapshot creation, integrity
/// validation, path resolution, scheduling and backup rotation.
/// </summary>
public partial class DatabaseBackupBackgroundServiceTests : IDisposable
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
}
