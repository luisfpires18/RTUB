namespace RTUB.Application.Configuration;

/// <summary>
/// Configuration options for the database backup scheduler
/// </summary>
public class DatabaseBackupOptions
{
    public const string SectionName = "DatabaseBackup";

    /// <summary>
    /// Whether the automatic database backup scheduler is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The times of day to perform database backups (format: "HH:mm")
    /// Default is 08:00 and 20:00
    /// </summary>
    public List<string> BackupTimes { get; set; } = new() { "08:00", "20:00" };

    /// <summary>
    /// The path to the source database file
    /// </summary>
    public string SourceDatabasePath { get; set; } = "/home/site/data/app.db";

    /// <summary>
    /// The path to the backup database file
    /// </summary>
    public string BackupDatabasePath { get; set; } = "/home/site/data/backup/app.db";
}
