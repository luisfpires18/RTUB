// Database sanitizer: SQLite helpers (integrity check, row counts, fingerprint, open, delete).
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace RTUB.Application.Services;

public static partial class DatabaseSanitizer
{
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