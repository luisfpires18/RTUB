using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Outcome of sanitizing a production snapshot into a development copy.
/// </summary>
/// <param name="Success">Whether a sanitized database was produced</param>
/// <param name="FailureReason">Why it failed, or null when it succeeded. Never contains a
/// password, a password hash, an email address or any other row content.</param>
/// <param name="UsersSanitized">Users that were given a development email and password</param>
/// <param name="UsersWithoutUserName">Users with no usable user name, whose email and password
/// were cleared instead</param>
/// <param name="PushSubscriptionsRemoved">Push subscription rows deleted</param>
public sealed record SanitizationResult(
    bool Success,
    string? FailureReason,
    int UsersSanitized,
    int UsersWithoutUserName,
    int PushSubscriptionsRemoved)
{
    public static SanitizationResult Fail(string reason) => new(false, reason, 0, 0, 0);
}

/// <summary>
/// Turns a production SQLite snapshot into a database that is safe to load into the Azure DEV
/// App Service, without ever writing to the snapshot it was given.
/// </summary>
/// <remarks>
/// This is the offline counterpart to <see cref="Data.SeedData.ResetDevDataAsync"/>, which
/// applies the same contract in-process at startup. Two differences make a separate
/// implementation worth having:
///
/// 1. It runs before the database reaches Azure DEV, so no production credential or push
///    endpoint is ever uploaded anywhere.
/// 2. It goes through raw ADO.NET rather than EF Core and <c>UserManager</c>, so it does not
///    require the snapshot's schema to match the current model. A production snapshot is
///    normally a few migrations behind <c>dev</c>; an EF query against it would throw
///    "no such column" before it sanitized anything. Only columns that have existed since the
///    initial migration are touched, and the app's own startup migration brings the schema
///    forward afterwards.
///
/// Passwords are hashed with <see cref="PasswordHasher{TUser}"/> using its default options -
/// the same component, with the same options, that Identity resolves at runtime, since
/// <c>AddIdentityServices</c> does not configure <see cref="PasswordHasherOptions"/>. Every user
/// therefore gets its own salt; no hash is hardcoded or copied between rows.
/// </remarks>
public static partial class DatabaseSanitizer
{
    /// <summary>
    /// Domain every development email is rewritten to. Matches
    /// <see cref="Data.SeedData.ResetDevDataAsync"/>.
    /// </summary>
    public const string DevEmailDomain = "rtub.pt";

    /// <summary>
    /// Tables that must exist in the snapshot before anything is copied. Their absence means the
    /// file is not an RTUB database, whatever else it might be.
    /// </summary>
    public static readonly IReadOnlyList<string> RequiredTables =
        ["AspNetUsers", "PushSubscriptions", "__EFMigrationsHistory"];

    /// <summary>
    /// Validates <paramref name="sourcePath"/>, copies it to <paramref name="destinationPath"/>,
    /// sanitizes the copy and validates the result. Fails closed: any failed check deletes the
    /// destination and returns a reason, so a half-sanitized database can never be shipped.
    /// </summary>
    /// <param name="sourcePath">Snapshot to read. Opened read-only and never modified.</param>
    /// <param name="destinationPath">Path the sanitized database is written to. Overwritten.</param>
    /// <param name="devPassword">Password every usable account is reset to. Never logged.</param>
    public static SanitizationResult Sanitize(string sourcePath, string destinationPath, string devPassword)
    {
        // Ordered so that nothing is written until every argument has been accepted. A missing
        // password must leave the destination exactly as it was, not replaced by a half-done copy.
        if (string.IsNullOrWhiteSpace(devPassword))
        {
            return SanitizationResult.Fail("no development password was supplied");
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return SanitizationResult.Fail("no source path was supplied");
        }

        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            return SanitizationResult.Fail("no destination path was supplied");
        }

        var source = Path.GetFullPath(sourcePath);
        var destination = Path.GetFullPath(destinationPath);

        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
        {
            return SanitizationResult.Fail("the destination is the source file; the source is immutable input");
        }

        var sourceFailure = ValidateSource(source);
        if (sourceFailure != null)
        {
            return SanitizationResult.Fail(sourceFailure);
        }

        // Proof, at the end, that the source came through untouched.
        var sourceFingerprint = Fingerprint(source);
        var originalHashes = ReadPasswordHashes(source);

        var destinationDirectory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        File.Copy(source, destination, overwrite: true);

        int sanitized, withoutUserName, subscriptionsRemoved;
        try
        {
            (sanitized, withoutUserName, subscriptionsRemoved) = Rewrite(destination, devPassword);
        }
        catch (Exception ex)
        {
            TryDelete(destination);
            return SanitizationResult.Fail($"sanitization failed: {ex.Message}");
        }

        var destinationFailure = ValidateSanitized(destination, originalHashes, devPassword);
        if (destinationFailure != null)
        {
            TryDelete(destination);
            return SanitizationResult.Fail(destinationFailure);
        }

        if (Fingerprint(source) != sourceFingerprint)
        {
            TryDelete(destination);
            return SanitizationResult.Fail("the source file changed while it was being read");
        }

        return new SanitizationResult(true, null, sanitized, withoutUserName, subscriptionsRemoved);
    }

    private static (int Sanitized, int WithoutUserName, int SubscriptionsRemoved) Rewrite(
        string path,
        string devPassword)
    {
        var hasher = new PasswordHasher<ApplicationUser>();
        var normalizer = new UpperInvariantLookupNormalizer();

        // Identity's v3 hash format derives nothing from the user instance, so one throwaway is
        // enough - and the salt is still random per HashPassword call.
        var probe = new ApplicationUser();

        using var connection = Open(path, readOnly: false);
        connection.Open();

        // Leaves the artifact with no -wal/-shm sidecar to carry to App Service. The app's own
        // SqliteConnectionInterceptor puts the database back into WAL on first use.
        // Must happen outside a transaction.
        using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA journal_mode=DELETE;";
            pragma.ExecuteScalar();
        }

        using var transaction = connection.BeginTransaction();

        int subscriptionsRemoved;
        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM PushSubscriptions;";
            subscriptionsRemoved = delete.ExecuteNonQuery();
        }

        var users = new List<(string Id, string? UserName)>();
        using (var read = connection.CreateCommand())
        {
            read.Transaction = transaction;
            read.CommandText = "SELECT Id, UserName FROM AspNetUsers;";
            using var reader = read.ExecuteReader();
            while (reader.Read())
            {
                users.Add((reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1)));
            }
        }

        using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText =
            """
            UPDATE AspNetUsers
               SET Email = $email,
                   NormalizedEmail = $normalizedEmail,
                   PasswordHash = $passwordHash,
                   SecurityStamp = $securityStamp
             WHERE Id = $id;
            """;

        var emailParameter = update.Parameters.Add("$email", SqliteType.Text);
        var normalizedEmailParameter = update.Parameters.Add("$normalizedEmail", SqliteType.Text);
        var passwordHashParameter = update.Parameters.Add("$passwordHash", SqliteType.Text);
        var securityStampParameter = update.Parameters.Add("$securityStamp", SqliteType.Text);
        var idParameter = update.Parameters.Add("$id", SqliteType.Text);

        var sanitized = 0;
        var withoutUserName = 0;

        foreach (var (id, userName) in users)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                // No user name means no development address can be derived. Keeping the production
                // one would leak it into DEV, so the account is stripped instead: a null
                // PasswordHash is an account that cannot sign in at all.
                emailParameter.Value = DBNull.Value;
                normalizedEmailParameter.Value = DBNull.Value;
                passwordHashParameter.Value = DBNull.Value;
                withoutUserName++;
            }
            else
            {
                var email = $"{userName}@{DevEmailDomain}";
                emailParameter.Value = email;
                normalizedEmailParameter.Value = normalizer.NormalizeEmail(email)!;
                passwordHashParameter.Value = hasher.HashPassword(probe, devPassword);
                sanitized++;
            }

            // Rotated for every row, matching what Identity does on a password change: any
            // authentication cookie minted against the production database stops validating.
            securityStampParameter.Value = Guid.NewGuid().ToString();
            idParameter.Value = id;

            update.ExecuteNonQuery();
        }

        transaction.Commit();

        return (sanitized, withoutUserName, subscriptionsRemoved);
    }
}
