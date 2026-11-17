using FluentAssertions;
using RTUB.Application.Services;

namespace RTUB.Application.Tests.Services;

// DatabaseSanitizerTests: Fails Closed tests.
public partial class DatabaseSanitizerTests
{
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
}
