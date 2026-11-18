using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

// DatabaseSanitizerTests: Sanitization Contract tests.
public partial class DatabaseSanitizerTests
{
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
}
