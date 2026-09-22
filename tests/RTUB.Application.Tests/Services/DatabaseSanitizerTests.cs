using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using Xunit;

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
public class DatabaseSanitizerTests : IDisposable
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

    #region Source is immutable

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

    #endregion

    #region Sanitization contract

    [Fact]
    public void Sanitize_WithPushSubscriptions_RemovesEveryRow()
    {
        // Arrange
        var source = CreateDatabase(userCount: 4, pushSubscriptionCount: 7);
        var destination = NewTempPath();

        // Act
        var result = DatabaseSanitizer.Sanitize(source, destination, DevPassword);

        // Assert
        result.Success.Should().BeTrue(result.FailureReason);
        result.PushSubscriptionsRemoved.Should().Be(7);
        Count(destination, "PushSubscriptions").Should().Be(0);
        Count(source, "PushSubscriptions").Should().Be(7, "the source must be untouched");
    }

    [Fact]
    public void Sanitize_Always_RewritesEmailsToTheDevelopmentDomain()
    {
        // Arrange
        var source = CreateDatabase(userCount: 3, pushSubscriptionCount: 0);
        var destination = NewTempPath();

        // Act
        var result = DatabaseSanitizer.Sanitize(source, destination, DevPassword);

        // Assert
        result.Success.Should().BeTrue(result.FailureReason);

        var rows = ReadUsers(destination);
        rows.Should().HaveCount(3);
        foreach (var row in rows)
        {
            row.Email.Should().Be($"{row.UserName}@rtub.pt");
            row.NormalizedEmail.Should().Be(row.Email!.ToUpperInvariant());
        }
    }

    [Fact]
    public void Sanitize_Always_ReplacesEveryPasswordHashWithOneThatVerifies()
    {
        // Arrange
        var source = CreateDatabase(userCount: 4, pushSubscriptionCount: 0);
        var destination = NewTempPath();
        var originals = DatabaseSanitizer.ReadPasswordHashes(source);

        // Act
        var result = DatabaseSanitizer.Sanitize(source, destination, DevPassword);

        // Assert
        result.Success.Should().BeTrue(result.FailureReason);
        result.UsersSanitized.Should().Be(4);

        var hasher = new PasswordHasher<ApplicationUser>();
        var probe = new ApplicationUser();
        var rows = ReadUsers(destination);

        foreach (var row in rows)
        {
            row.PasswordHash.Should().NotBe(originals[row.Id], "no original hash may survive");
            hasher.VerifyHashedPassword(probe, row.PasswordHash!, DevPassword)
                .Should().NotBe(PasswordVerificationResult.Failed);
        }

        // Per-user salt: the same password must not produce the same stored hash twice.
        rows.Select(r => r.PasswordHash).Distinct().Should().HaveCount(rows.Count);
    }

    [Fact]
    public void Sanitize_Always_RotatesTheSecurityStamp()
    {
        // Arrange
        var source = CreateDatabase(userCount: 3, pushSubscriptionCount: 0);
        var destination = NewTempPath();
        var before = ReadUsers(source).ToDictionary(r => r.Id, r => r.SecurityStamp);

        // Act
        var result = DatabaseSanitizer.Sanitize(source, destination, DevPassword);

        // Assert
        result.Success.Should().BeTrue(result.FailureReason);
        foreach (var row in ReadUsers(destination))
        {
            row.SecurityStamp.Should().NotBe(before[row.Id]);
        }
    }

    [Fact]
    public void Sanitize_Always_PreservesUsersAndTheirDomainData()
    {
        // Arrange
        var source = CreateDatabase(userCount: 6, pushSubscriptionCount: 2);
        var destination = NewTempPath();

        // Act
        var result = DatabaseSanitizer.Sanitize(source, destination, DevPassword);

        // Assert - realistic DEV testing is the point; only credentials and contact data change.
        result.Success.Should().BeTrue(result.FailureReason);
        Count(destination, "AspNetUsers").Should().Be(6);
        Count(destination, "__EFMigrationsHistory").Should().Be(Count(source, "__EFMigrationsHistory"));

        var sanitized = ReadUsers(destination).ToDictionary(r => r.Id);
        foreach (var original in ReadUsers(source))
        {
            sanitized[original.Id].UserName.Should().Be(original.UserName);
            sanitized[original.Id].FirstName.Should().Be(original.FirstName);
        }
    }

    [Fact]
    public void Sanitize_UserWithoutAUsableUserName_ClearsItsCredentialsRatherThanKeepingThem()
    {
        // Arrange
        var source = CreateDatabase(userCount: 2, pushSubscriptionCount: 0);
        AddUser(source, id: "blank-user", userName: "   ", email: "real.person@example.com");
        var destination = NewTempPath();

        // Act
        var result = DatabaseSanitizer.Sanitize(source, destination, DevPassword);

        // Assert - the production address must not survive, and the account must not be usable.
        result.Success.Should().BeTrue(result.FailureReason);
        result.UsersSanitized.Should().Be(2);
        result.UsersWithoutUserName.Should().Be(1);

        var blank = ReadUsers(destination).Single(r => r.Id == "blank-user");
        blank.Email.Should().BeNull();
        blank.NormalizedEmail.Should().BeNull();
        blank.PasswordHash.Should().BeNull();
    }

    [Fact]
    public void Sanitize_Always_LeavesTheResultPassingQuickCheck()
    {
        // Arrange
        var source = CreateDatabase(userCount: 5, pushSubscriptionCount: 5);
        var destination = NewTempPath();

        // Act
        var result = DatabaseSanitizer.Sanitize(source, destination, DevPassword);

        // Assert
        result.Success.Should().BeTrue(result.FailureReason);
        QuickCheck(destination).Should().Be("ok");
    }

    /// <summary>
    /// The DEV storage-ownership model depends on inherited production media URLs arriving in DEV
    /// exactly as production wrote them: the ownership check classifies an object by comparing the
    /// stored URL's origin, so a URL the sanitizer rewrote - or normalised, or trimmed - would be
    /// classified wrongly. The sanitizer must not touch any media column.
    /// </summary>
    [Fact]
    public void Sanitize_Always_LeavesStoredMediaUrlsByteForByteUnchanged()
    {
        // Arrange
        var source = CreateDatabase(userCount: 2, pushSubscriptionCount: 1);
        Execute(source, """
            UPDATE "AspNetUsers"
               SET "ImageUrl" = 'https://pub-prod.r2.dev/images/Production/member/' || "Id" || '_20250101120000.webp';
            """);
        var destination = NewTempPath();
        var before = ReadImageUrls(source);
        before.Values.Should().OnlyContain(v => v!.StartsWith("https://pub-prod.r2.dev/"));

        // Act
        var result = DatabaseSanitizer.Sanitize(source, destination, DevPassword);

        // Assert
        result.Success.Should().BeTrue(result.FailureReason);
        ReadImageUrls(destination).Should().Equal(before);
    }

    #endregion

    #region Fails closed

    [Fact]
    public void Sanitize_NoPasswordSupplied_FailsBeforeTheDestinationIsReplaced()
    {
        // Arrange
        var source = CreateDatabase(userCount: 2, pushSubscriptionCount: 1);
        var destination = NewTempPath();
        File.WriteAllText(destination, "existing content that must survive");

        // Act
        var result = DatabaseSanitizer.Sanitize(source, destination, "   ");

        // Assert
        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("password");
        File.ReadAllText(destination).Should().Be("existing content that must survive");
    }

    [Fact]
    public void Sanitize_NonSqliteInput_IsRejected()
    {
        // Arrange
        var source = NewTempPath();
        File.WriteAllText(source, "this is definitely not a database");
        var destination = NewTempPath();

        // Act
        var result = DatabaseSanitizer.Sanitize(source, destination, DevPassword);

        // Assert
        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("not a readable SQLite database");
        File.Exists(destination).Should().BeFalse();
    }

    [Fact]
    public void Sanitize_EmptyFile_IsRejected()
    {
        // Arrange
        var source = NewTempPath();
        File.WriteAllBytes(source, []);
        var destination = NewTempPath();

        // Act
        var result = DatabaseSanitizer.Sanitize(source, destination, DevPassword);

        // Assert
        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("empty");
        File.Exists(destination).Should().BeFalse();
    }

    [Fact]
    public void Sanitize_MissingFile_IsRejected()
    {
        // Arrange
        var source = NewTempPath();
        var destination = NewTempPath();

        // Act
        var result = DatabaseSanitizer.Sanitize(source, destination, DevPassword);

        // Assert
        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("no file at");
        File.Exists(destination).Should().BeFalse();
    }

    [Theory]
    [InlineData("AspNetUsers")]
    [InlineData("PushSubscriptions")]
    [InlineData("__EFMigrationsHistory")]
    public void Sanitize_CriticalTableMissing_IsRejected(string missingTable)
    {
        // Arrange
        var source = CreateDatabase(userCount: 2, pushSubscriptionCount: 1);
        Execute(source, $"DROP TABLE \"{missingTable}\";");
        var destination = NewTempPath();

        // Act
        var result = DatabaseSanitizer.Sanitize(source, destination, DevPassword);

        // Assert
        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain(missingTable);
        File.Exists(destination).Should().BeFalse();
    }

    [Fact]
    public void Sanitize_SourceWithNoUsers_IsRejected()
    {
        // Arrange
        var source = CreateDatabase(userCount: 0, pushSubscriptionCount: 0);
        var destination = NewTempPath();

        // Act
        var result = DatabaseSanitizer.Sanitize(source, destination, DevPassword);

        // Assert
        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("no users");
        File.Exists(destination).Should().BeFalse();
    }

    [Fact]
    public void ValidateSanitized_UnsanitizedDatabase_ReportsTheSurvivingHash()
    {
        // Arrange - the post-check run against a database nothing was done to.
        var untouched = CreateDatabase(userCount: 3, pushSubscriptionCount: 2);
        var originals = DatabaseSanitizer.ReadPasswordHashes(untouched);

        // Act
        var failure = DatabaseSanitizer.ValidateSanitized(untouched, originals, DevPassword);

        // Assert
        failure.Should().NotBeNull();
        failure.Should().Contain("push subscription(s) survived");
    }

    [Fact]
    public void ValidateSanitized_OriginalPasswordHashLeftInPlace_IsCaught()
    {
        // Arrange - subscriptions cleared but a password hash left alone, which is what a
        // partially applied sanitization looks like.
        var partial = CreateDatabase(userCount: 3, pushSubscriptionCount: 2);
        Execute(partial, "DELETE FROM PushSubscriptions;");
        Execute(partial, "UPDATE AspNetUsers SET Email = UserName || '@rtub.pt', NormalizedEmail = UPPER(UserName || '@rtub.pt');");
        var originals = DatabaseSanitizer.ReadPasswordHashes(partial);

        // Act
        var failure = DatabaseSanitizer.ValidateSanitized(partial, originals, DevPassword);

        // Assert
        failure.Should().NotBeNull();
        failure.Should().Contain("original password hash");
    }

    [Fact]
    public void ValidateSanitized_FailureReasons_NeverQuoteRowContent()
    {
        // Arrange
        var source = CreateDatabase(userCount: 1, pushSubscriptionCount: 1);
        var originals = DatabaseSanitizer.ReadPasswordHashes(source);
        var email = ReadUsers(source).Single().Email;

        // Act
        var failure = DatabaseSanitizer.ValidateSanitized(source, originals, DevPassword);

        // Assert - ids only; no address, hash or password may reach a message.
        failure.Should().NotBeNull();
        failure.Should().NotContain(email!);
        failure.Should().NotContain(DevPassword);
        failure.Should().NotContain(originals.Values.Single()!);
    }

    #endregion

    #region Fixtures

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

    private static void AddUser(string path, string id, string userName, string email)
    {
        using var connection = OpenWritable(path);
        connection.Open();

        using var insert = connection.CreateCommand();
        insert.CommandText =
            """
            INSERT INTO "AspNetUsers" ("Id", "UserName", "Email", "PasswordHash", "SecurityStamp")
            VALUES ($id, $userName, $email, $passwordHash, $securityStamp);
            """;
        insert.Parameters.AddWithValue("$id", id);
        insert.Parameters.AddWithValue("$userName", userName);
        insert.Parameters.AddWithValue("$email", email);
        insert.Parameters.AddWithValue("$passwordHash", new PasswordHasher<ApplicationUser>().HashPassword(new ApplicationUser(), "original"));
        insert.Parameters.AddWithValue("$securityStamp", Guid.NewGuid().ToString());
        insert.ExecuteNonQuery();
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

    private static long Count(string path, string table)
    {
        using var connection = OpenReadable(path);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM \"{table}\";";
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static string? QuickCheck(string path)
    {
        using var connection = OpenReadable(path);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check;";
        return command.ExecuteScalar() as string;
    }

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

    private static Dictionary<string, string?> ReadImageUrls(string path)
    {
        var urls = new Dictionary<string, string?>(StringComparer.Ordinal);

        using var connection = OpenReadable(path);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, ImageUrl FROM AspNetUsers;";
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            urls[reader.GetString(0)] = reader.IsDBNull(1) ? null : reader.GetString(1);
        }

        return urls;
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

    #endregion
}
