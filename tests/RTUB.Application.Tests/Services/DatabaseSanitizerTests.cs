using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for <see cref="DatabaseSanitizer"/>: the source is never modified, push
/// subscriptions are removed, emails and password hashes are replaced, and every failure path
/// fails closed.
/// </summary>
/// <remarks>
/// Every fixture is a synthetic SQLite file built here. No production snapshot, and no file
/// outside the test's own temp directory, is opened by these tests.
/// </remarks>
public partial class DatabaseSanitizerTests : IDisposable
{
    private const string DevPassword = "dev-password-for-tests";

    private readonly List<string> _tempFiles = [];

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file)) File.Delete(file);
            }
            catch (IOException)
            {
                // A leaked temp file must not fail the run.
            }
        }

        GC.SuppressFinalize(this);
    }


    [Fact]
    public void Sanitize_Always_LeavesTheSourceByteIdentical()
    {
        // Arrange
        var source = CreateDatabase(userCount: 5, pushSubscriptionCount: 3);
        var destination = NewTempPath();
        var before = Fingerprint(source);

        // Act
        var result = DatabaseSanitizer.Sanitize(source, destination, DevPassword);

        // Assert
        result.Success.Should().BeTrue(result.FailureReason);
        Fingerprint(source).Should().Be(before);
        File.Exists(source + "-wal").Should().BeFalse();
        File.Exists(source + "-shm").Should().BeFalse();
    }

    [Fact]
    public void Sanitize_SourceUsedAsDestination_IsRejectedAndChangesNothing()
    {
        // Arrange
        var source = CreateDatabase(userCount: 2, pushSubscriptionCount: 1);
        var before = Fingerprint(source);

        // Act
        var result = DatabaseSanitizer.Sanitize(source, source, DevPassword);

        // Assert
        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("immutable input");
        Fingerprint(source).Should().Be(before);
    }

    [Fact]
    public void Sanitize_Always_LeavesNoSidecarsBesideTheSanitizedFile()
    {
        // Arrange - a WAL-mode source, which is what a live RTUB database looks like.
        var source = CreateDatabase(userCount: 3, pushSubscriptionCount: 2, walMode: true);
        var destination = NewTempPath();

        // Act
        var result = DatabaseSanitizer.Sanitize(source, destination, DevPassword);

        // Assert
        result.Success.Should().BeTrue(result.FailureReason);
        File.Exists(destination + "-wal").Should().BeFalse();
        File.Exists(destination + "-shm").Should().BeFalse();
    }

    private string NewTempPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"rtub-sanitizer-test-{Guid.NewGuid():N}.db");
        _tempFiles.Add(path);
        _tempFiles.Add(path + "-wal");
        _tempFiles.Add(path + "-shm");
        return path;
    }

    /// <summary>
    /// Builds a synthetic database with only the columns the sanitizer touches, plus one extra
    /// column so "domain data is preserved" can be asserted. Deliberately not the EF model: the
    /// sanitizer must work against a schema older than the current one.
    /// </summary>
    private string CreateDatabase(int userCount, int pushSubscriptionCount, bool walMode = false)
    {
        var path = NewTempPath();

        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString());
        connection.Open();

        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                CREATE TABLE "AspNetUsers" (
                    "Id" TEXT NOT NULL PRIMARY KEY,
                    "UserName" TEXT NULL,
                    "NormalizedUserName" TEXT NULL,
                    "Email" TEXT NULL,
                    "NormalizedEmail" TEXT NULL,
                    "PasswordHash" TEXT NULL,
                    "SecurityStamp" TEXT NULL,
                    "FirstName" TEXT NULL,
                    "ImageUrl" TEXT NULL
                );
                CREATE TABLE "PushSubscriptions" (
                    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    "UserId" TEXT NOT NULL,
                    "Endpoint" TEXT NOT NULL
                );
                CREATE TABLE "__EFMigrationsHistory" (
                    "MigrationId" TEXT NOT NULL PRIMARY KEY,
                    "ProductVersion" TEXT NOT NULL
                );
                INSERT INTO "__EFMigrationsHistory" VALUES ('20251026224954_AddMentorField', '10.0.11');
                """;
            command.ExecuteNonQuery();
        }

        if (walMode)
        {
            using var pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA journal_mode=WAL;";
            pragma.ExecuteScalar();
        }

        var hasher = new PasswordHasher<ApplicationUser>();
        var probe = new ApplicationUser();

        for (var i = 0; i < userCount; i++)
        {
            using var insert = connection.CreateCommand();
            insert.CommandText =
                """
                INSERT INTO "AspNetUsers"
                    ("Id", "UserName", "NormalizedUserName", "Email", "NormalizedEmail",
                     "PasswordHash", "SecurityStamp", "FirstName")
                VALUES ($id, $userName, $normalizedUserName, $email, $normalizedEmail,
                        $passwordHash, $securityStamp, $firstName);
                """;
            insert.Parameters.AddWithValue("$id", $"user-{i}");
            insert.Parameters.AddWithValue("$userName", $"member{i}");
            insert.Parameters.AddWithValue("$normalizedUserName", $"MEMBER{i}");
            insert.Parameters.AddWithValue("$email", $"member{i}@example.invalid");
            insert.Parameters.AddWithValue("$normalizedEmail", $"MEMBER{i}@EXAMPLE.INVALID");
            insert.Parameters.AddWithValue("$passwordHash", hasher.HashPassword(probe, $"original-{i}"));
            insert.Parameters.AddWithValue("$securityStamp", Guid.NewGuid().ToString());
            insert.Parameters.AddWithValue("$firstName", $"Member {i}");
            insert.ExecuteNonQuery();
        }

        for (var i = 0; i < pushSubscriptionCount; i++)
        {
            using var insert = connection.CreateCommand();
            insert.CommandText = """INSERT INTO "PushSubscriptions" ("UserId", "Endpoint") VALUES ($userId, $endpoint);""";
            insert.Parameters.AddWithValue("$userId", $"user-{i % Math.Max(userCount, 1)}");
            insert.Parameters.AddWithValue("$endpoint", $"https://push.example.invalid/{i}");
            insert.ExecuteNonQuery();
        }

        connection.Close();
        SqliteConnection.ClearAllPools();

        return path;
    }

    private static void Execute(string path, string sql)
    {
        using var connection = OpenWritable(path);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static SqliteConnection OpenWritable(string path) =>
        new(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString());

    private static SqliteConnection OpenReadable(string path) =>
        new(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());

    private static List<UserRow> ReadUsers(string path)
    {
        var rows = new List<UserRow>();

        using var connection = OpenReadable(path);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, UserName, Email, NormalizedEmail, PasswordHash, SecurityStamp, FirstName FROM AspNetUsers;";
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            rows.Add(new UserRow(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6)));
        }

        return rows;
    }

    private static string Fingerprint(string path)
    {
        using var stream = File.OpenRead(path);
        return $"{stream.Length}:{Convert.ToHexString(SHA256.HashData(stream))}";
    }

    private sealed record UserRow(
        string Id,
        string? UserName,
        string? Email,
        string? NormalizedEmail,
        string? PasswordHash,
        string? SecurityStamp,
        string? FirstName);
}
