// Database sanitizer: validate source and sanitized databases, read password hashes.
using Microsoft.AspNetCore.Identity;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

public static partial class DatabaseSanitizer
{
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
}