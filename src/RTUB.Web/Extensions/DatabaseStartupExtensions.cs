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

                    // Only migrate if there are pending migrations (performance optimization)
                    var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
                    bool hadMemberStatusMigration = pendingMigrations.Any(m =>
                        m.Contains("AddMemberStatusTable") ||
                        m.Contains("AddTotalActivitiesCountToMemberStatus"));

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

                    // The premade /messages groups (061): retired ones (VETERANOS, TUNOSSAUROS...) are archived on deploy,
                    // not only at the daily job, which MemberStatusUpdate:Enabled can switch off. Idempotent; never fails startup.
                    try
                    {
                        await MessagingGroups.SyncAsync(db, DateTime.UtcNow);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to sync the premade message groups on startup");
                    }

                    // Logística 3/5 (067): pre-067 columns get their workflow state (mixed ones split, no card changes
                    // status) and pre-067 boards their file folder name, once. Idempotent; never fails startup.
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

                    // Logística files live in Documentação (104): files still kept outside it move into their board's
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

                    // Initialize MemberStatus table if the migration just ran
                    // This ensures "Gestao de membros ativos" has data immediately after deployment
                    // instead of waiting for the scheduled daily update
                    if (hadMemberStatusMigration)
                    {
                        try
                        {
                            logger.LogInformation("MemberStatus migration detected. Starting initial population...");
                            var memberStatusService = sp.GetRequiredService<IMemberStatusService>();
                            var run = await memberStatusService.ReconcileAllMemberStatusesAsync();
                            logger.LogInformation("Initial MemberStatus population completed: {Run}", run);
                        }
                        catch (Exception ex)
                        {
                            // Log error but don't fail startup - scheduled update will populate later
                            logger.LogError(ex, "Failed to populate MemberStatus table on startup. Data will be populated during next scheduled update.");
                        }
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
