using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;

namespace RTUB.Web.Extensions;

// Migrates the database and seeds it at startup (skipped in the Test environment).
public static class DatabaseStartupExtensions
{
    public static async Task MigrateAndSeedDatabaseAsync(this WebApplication app, IConfiguration configuration, string connectionString)
    {
        if (app.Environment.EnvironmentName != "Test")
        {
            using (var scope = app.Services.CreateScope())
            {
                var sp = scope.ServiceProvider;
                var logger = sp.GetRequiredService<ILogger<Program>>();

                try
                {
                    var db = sp.GetRequiredService<ApplicationDbContext>();

                    // A database that ran the squashed chain gets the Baseline's history row instead of running it.
                    await MigrationBaseline.AdoptAsync(db.Database);

                    // Only migrate if there are pending migrations (performance optimization)
                    var pendingMigrations = await db.Database.GetPendingMigrationsAsync();

                    if (pendingMigrations.Any())
                    {
                        // An existing database is about to change shape: take a restore point
                        // first. Throws on failure, and the catch below rethrows, so a database
                        // is never migrated without one. A fresh database has nothing to protect.
                        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
                        if (appliedMigrations.Any())
                        {
                            var snapshot = PreMigrationSnapshot.Take(
                                connectionString, pendingMigrations.Last(), DateTime.UtcNow);
                            logger.LogInformation(
                                "Pre-migration snapshot {Snapshot} taken before applying {Count} migration(s) after {LastApplied}",
                                snapshot, pendingMigrations.Count(), appliedMigrations.Last());
                        }

                        await db.Database.MigrateAsync();
                        logger.LogInformation("Database migrations applied successfully");
                    }

                    await SeedData.InitializeAsync(sp, configuration);

                    // Positions and Admin / Mod follow the current mandate (a new fiscal year's elections leave
                    // last year's holders behind otherwise). Idempotent; never fails startup.
                    try
                    {
                        await sp.GetRequiredService<IGovernanceManagementService>().SyncCurrentMandateAsync();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to sync positions and roles with the current mandate on startup");
                    }

                    // The premade /messages groups: retired ones (VETERANOS, TUNOSSAUROS...) are archived on deploy,
                    // not only at the daily job, which MemberStatusUpdate:Enabled can switch off. Idempotent; never fails startup.
                    try
                    {
                        await MessagingGroups.SyncAsync(db, DateTime.UtcNow);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to sync the premade message groups on startup");
                    }

                    // Logística: columns without a workflow state get one (mixed ones split, no card changes
                    // status) and boards without a file folder name get one, once. Idempotent; never fails startup.
                    try
                    {
                        var mapped = await LogisticsWorkflow.MapLegacyAsync(db);
                        if (mapped != (0, 0, 0))
                        {
                            logger.LogInformation("Logistics workflow mapped: {Columns} column(s), {Split} split column(s), {Boards} board folder(s)",
                                mapped.Columns, mapped.Split, mapped.Boards);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to map the legacy logistics columns on startup");
                    }

                    // Logística files live in Documentação: files still kept outside it move into their board's
                    // folder, once. Idempotent, best effort per file; never fails startup.
                    try
                    {
                        var moved = await sp.GetRequiredService<ILogisticsKanbanService>().MoveFilesIntoDocumentationAsync();
                        if (moved > 0)
                        {
                            logger.LogInformation("Logistics files moved into Documentação: {Moved}", moved);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to move the logistics files into Documentação on startup");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while migrating or seeding the database");
                    throw;
                }
            }
        }
    }
}
