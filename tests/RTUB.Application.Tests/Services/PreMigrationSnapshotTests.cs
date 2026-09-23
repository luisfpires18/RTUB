using FluentAssertions;
using Microsoft.Data.Sqlite;
using RTUB.Application.Services;
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// The restore point Program.cs takes before migrating an existing database. It has to capture
/// what is still in the WAL, refuse loudly rather than let a migration run without it, and keep
/// only the newest five.
/// </summary>
public class PreMigrationSnapshotTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 9, 22, 10, 15, 0, DateTimeKind.Utc);

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"rtub-premigration-{Guid.NewGuid():N}");
    private readonly List<SqliteConnection> _open = new();

    public PreMigrationSnapshotTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        foreach (var connection in _open)
        {
            connection.Dispose();
        }

        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Best effort - a leaked temp directory must not fail the run.
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Take_CapturesRowsStillInTheWal_AsOneSelfContainedFile()
    {
        var database = CreateWalDatabase(rows: 10);
        var writer = OpenAndKeep(database);
        Insert(writer, 7); // committed, but sitting in the WAL: a raw copy of app.db would miss it

        var snapshot = PreMigrationSnapshot.Take($"Data Source={database}", "20260901000000_AddThing", Now);

        snapshot.Should().Be(Path.Combine(_root, "backups", "pre-migration",
            "20260922T101500Z-before-20260901000000_AddThing.db"));
        CountRows(snapshot).Should().Be(17);
        SqliteConnection.ClearAllPools();
        File.Exists(snapshot + "-wal").Should().BeFalse();
        File.Exists(snapshot + "-shm").Should().BeFalse();
        Directory.GetFiles(Path.GetDirectoryName(snapshot)!, "*.partial").Should().BeEmpty();
    }

    [Fact]
    public void Take_WhenTheDatabaseCannotBeSnapshotted_Throws_AndLeavesNothingBehind()
    {
        var database = Path.Combine(_root, "app.db");
        File.WriteAllText(database, "this is not a SQLite database, but it is big enough to not be empty");

        var take = () => PreMigrationSnapshot.Take($"Data Source={database}", "20260901000000_AddThing", Now);

        take.Should().Throw<Exception>();
        var directory = Path.Combine(_root, "backups", "pre-migration");
        Directory.GetFiles(directory).Should().BeEmpty("a failed snapshot must not look like a restore point");
    }

    [Theory]
    [InlineData("Data Source=:memory:")]
    [InlineData("Data Source=does-not-exist-anywhere.db")]
    public void Take_WhenThereIsNoDatabaseFile_RefusesInsteadOfSkipping(string connectionString)
    {
        var take = () => PreMigrationSnapshot.Take(connectionString, "20260901000000_AddThing", Now);

        take.Should().Throw<InvalidOperationException>().WithMessage("*Migrations were not applied*");
    }

    [Fact]
    public void Take_KeepsOnlyTheFiveNewest()
    {
        var database = CreateWalDatabase(rows: 3);

        for (var i = 0; i < 7; i++)
        {
            PreMigrationSnapshot.Take($"Data Source={database}", $"2026090{i}000000_Step{i}", Now.AddDays(i));
        }

        var kept = Directory.GetFiles(Path.Combine(_root, "backups", "pre-migration"), "*.db")
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToList();

        kept.Should().HaveCount(PreMigrationSnapshot.Keep);
        kept.First().Should().StartWith("20260924T", "the two oldest (22nd, 23rd) are pruned");
        kept.Last().Should().StartWith("20260928T");
    }

    [Fact]
    public void Take_DeletesAHalfWrittenSnapshotLeftByACrashedStartup()
    {
        var database = CreateWalDatabase(rows: 3);
        var directory = Directory.CreateDirectory(Path.Combine(_root, "backups", "pre-migration")).FullName;
        var leftover = Path.Combine(directory, "20260101T000000Z-before-X.db.partial");
        File.WriteAllText(leftover, "half");

        PreMigrationSnapshot.Take($"Data Source={database}", "20260901000000_AddThing", Now);

        File.Exists(leftover).Should().BeFalse();
    }

    private string CreateWalDatabase(int rows)
    {
        var path = Path.Combine(_root, "app.db");
        using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            connection.Open();
            Execute(connection, "PRAGMA journal_mode = WAL;");
            Execute(connection, "CREATE TABLE Members (Id INTEGER PRIMARY KEY, Nickname TEXT NOT NULL);");
            Insert(connection, rows);
        }

        SqliteConnection.ClearAllPools();
        return path;
    }

    private SqliteConnection OpenAndKeep(string path)
    {
        var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        _open.Add(connection);
        return connection;
    }

    private static void Insert(SqliteConnection connection, int count)
    {
        using var transaction = connection.BeginTransaction();
        for (var i = 0; i < count; i++)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO Members (Nickname) VALUES ('tuno');";
            insert.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
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
