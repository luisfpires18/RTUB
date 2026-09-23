using System.Security.Cryptography;
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
public static class DatabaseSanitizer
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

    /// <summary>
    /// Checks a snapshot is a readable, complete RTUB database before it is copied.
    /// Returns null when it passes, otherwise the reason it did not.
    /// </summary>
    public static string? ValidateSource(string path)
    {
        if (!File.Exists(path))
        {
            return $"no file at {path}";
        }

        // Zero bytes is a valid *empty* database as far as SQLite is concerned, so this has to be
        // rejected on its own rather than left to the table checks.
        if (new FileInfo(path).Length == 0)
        {
            return "the source file is empty";
        }

        try
        {
            using var connection = Open(path, readOnly: true);
            connection.Open();

            var failure = CheckIntegrityAndTables(connection);
            if (failure != null)
            {
                return failure;
            }

            if (CountRows(connection, "AspNetUsers") == 0)
            {
                return "the source database contains no users";
            }

            return null;
        }
        catch (Exception ex)
        {
            return $"the source is not a readable SQLite database ({ex.Message})";
        }
    }

    /// <summary>
    /// Re-reads a sanitized database and proves the contract held. Returns null when it passes.
    /// </summary>
    /// <remarks>
    /// Failure messages name the offending user by id only. No email address, password or hash
    /// reaches a message, a log or a console.
    /// </remarks>
    public static string? ValidateSanitized(
        string path,
        IReadOnlyDictionary<string, string?> originalHashes,
        string devPassword)
    {
        try
        {
            using var connection = Open(path, readOnly: true);
            connection.Open();

            var failure = CheckIntegrityAndTables(connection);
            if (failure != null)
            {
                return failure;
            }

            var subscriptions = CountRows(connection, "PushSubscriptions");
            if (subscriptions != 0)
            {
                return $"{subscriptions} push subscription(s) survived sanitization";
            }

            if (CountRows(connection, "AspNetUsers") == 0)
            {
                return "no users survived sanitization";
            }

            if (CountRows(connection, "__EFMigrationsHistory") == 0)
            {
                return "the migration history is empty";
            }

            var hasher = new PasswordHasher<ApplicationUser>();
            var normalizer = new UpperInvariantLookupNormalizer();
            var probe = new ApplicationUser();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, UserName, Email, NormalizedEmail, PasswordHash FROM AspNetUsers;";
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var id = reader.GetString(0);
                var userName = reader.IsDBNull(1) ? null : reader.GetString(1);
                var email = reader.IsDBNull(2) ? null : reader.GetString(2);
                var normalizedEmail = reader.IsDBNull(3) ? null : reader.GetString(3);
                var passwordHash = reader.IsDBNull(4) ? null : reader.GetString(4);

                if (originalHashes.TryGetValue(id, out var before)
                    && before != null
                    && string.Equals(passwordHash, before, StringComparison.Ordinal))
                {
                    return $"user '{id}' still carries its original password hash";
                }

                if (string.IsNullOrWhiteSpace(userName))
                {
                    if (email != null || normalizedEmail != null || passwordHash != null)
                    {
                        return $"user '{id}' has no usable user name but still carries credentials";
                    }

                    continue;
                }

                var expected = $"{userName}@{DevEmailDomain}";

                if (!string.Equals(email, expected, StringComparison.Ordinal))
                {
                    return $"user '{id}' does not carry the expected development email";
                }

                if (!string.Equals(normalizedEmail, normalizer.NormalizeEmail(expected), StringComparison.Ordinal))
                {
                    return $"user '{id}' has an incorrect NormalizedEmail";
                }

                if (passwordHash == null
                    || hasher.VerifyHashedPassword(probe, passwordHash, devPassword) == PasswordVerificationResult.Failed)
                {
                    return $"user '{id}' does not authenticate with the development password";
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            return $"the sanitized database could not be verified ({ex.Message})";
        }
    }

    /// <summary>
    /// Reads the pre-sanitization password hashes, so the post-check can prove every one of them
    /// was replaced. Held in memory only and never written anywhere.
    /// </summary>
    public static Dictionary<string, string?> ReadPasswordHashes(string path)
    {
        var hashes = new Dictionary<string, string?>(StringComparer.Ordinal);

        using var connection = Open(path, readOnly: true);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, PasswordHash FROM AspNetUsers;";
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            hashes[reader.GetString(0)] = reader.IsDBNull(1) ? null : reader.GetString(1);
        }

        return hashes;
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

    private static string? CheckIntegrityAndTables(SqliteConnection connection)
    {
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA quick_check;";
            var result = command.ExecuteScalar() as string;

            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            {
                return $"PRAGMA quick_check returned '{result ?? "(null)"}'";
            }
        }

        foreach (var table in RequiredTables)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
            command.Parameters.AddWithValue("$name", table);

            if (Convert.ToInt64(command.ExecuteScalar()) == 0)
            {
                return $"required table '{table}' is missing";
            }
        }

        return null;
    }

    private static long CountRows(SqliteConnection connection, string table)
    {
        // Table names cannot be parameterised; every caller passes a RequiredTables constant.
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM \"{table}\";";
        return Convert.ToInt64(command.ExecuteScalar());
    }

    /// <summary>
    /// Length plus SHA-256 of the whole file. Cheap enough at RTUB's database size and a stronger
    /// statement than a timestamp: it proves the bytes are identical, not merely untouched-looking.
    /// </summary>
    private static string Fingerprint(string path)
    {
        using var stream = File.OpenRead(path);
        var length = stream.Length;
        return $"{length}:{Convert.ToHexString(SHA256.HashData(stream))}";
    }

    private static SqliteConnection Open(string path, bool readOnly) =>
        new(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWrite,
            // Pooling keeps the file handle alive past Dispose, which would block the fingerprint
            // read and the temp-file cleanup that follow.
            Pooling = false,
            DefaultTimeout = 30
        }.ToString());

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // A leftover file on an already-failing run is not worth masking the real reason.
        }
    }
}
