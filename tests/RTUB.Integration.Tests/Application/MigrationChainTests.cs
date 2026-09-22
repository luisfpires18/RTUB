using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Services;
using Xunit;

namespace RTUB.Integration.Tests;

/// <summary>
/// Proves an empty SQLite database can migrate from zero to the current schema.
///
/// Nothing else in the suite covers this. <see cref="TestWebApplicationFactory"/> builds its
/// schema with <c>EnsureCreated()</c>, which projects the current model straight onto an empty
/// database and never executes a single migration — so a broken migration chain is invisible to
/// every other integration test while the application itself refuses to start.
///
/// That is exactly how unit 027 shipped a chain that could not run: AddMentorField's hand-written
/// SQLite table rebuild silently dropped YearLeitao, YearCaloiro and YearTuno, and the mismatch
/// only surfaced 14 migrations later when RemoveIsActiveFromApplicationUser asked EF to rebuild
/// AspNetUsers from its model snapshot and the SELECT hit columns the table no longer had.
/// </summary>
public class MigrationChainTests : IDisposable
{
    /// <summary>
    /// Disposable in-memory database, unique per run. <c>Cache=Shared</c> plus a connection held
    /// open for the lifetime of the test is what keeps it alive — an in-memory SQLite database is
    /// dropped the moment its last connection closes. Same arrangement, and same reasoning, as
    /// <see cref="TestWebApplicationFactory"/>.
    /// </summary>
    private readonly string _connectionString =
        $"Data Source=rtub-migrations-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    private readonly SqliteConnection _keepAlive;

    public MigrationChainTests()
    {
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();
    }

    private ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connectionString, b => b.MigrationsAssembly("RTUB"))
            .Options;

        return new ApplicationDbContext(
            options,
            Mock.Of<IHttpContextAccessor>(),
            new AuditContext(),
            new AuditLogAppender());
    }

    [Fact]
    public async Task EmptyDatabase_MigratesFromZeroToCurrentSchema()
    {
        await using var context = CreateContext();

        // The database is empty: every migration in the assembly is pending.
        var pendingBefore = (await context.Database.GetPendingMigrationsAsync()).ToList();
        pendingBefore.Should().NotBeEmpty("an empty database has the whole chain still to apply");

        // This is what Program.cs does at startup. If any migration in the chain is inconsistent
        // with the schema the migrations before it produced, it throws here.
        await context.Database.MigrateAsync();

        var applied = (await context.Database.GetAppliedMigrationsAsync()).ToList();
        applied.Should().HaveCount(pendingBefore.Count);

        var pendingAfter = await context.Database.GetPendingMigrationsAsync();
        pendingAfter.Should().BeEmpty();
    }

    [Fact]
    public async Task MigratedDatabase_HasApplicationUserMembershipDateColumns()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        var columns = await ColumnNamesAsync(context, "AspNetUsers");

        // The six columns behind ApplicationUser.TempoDeTuno. YearLeitao/YearCaloiro/YearTuno are
        // the ones AddMentorField used to drop; the Month trio is asserted alongside them because
        // the same class of defect would take those too.
        columns.Should().Contain(new[]
        {
            "YearLeitao", "YearCaloiro", "YearTuno",
            "MonthLeitao", "MonthCaloiro", "MonthTuno"
        });
    }

    private static async Task<List<string>> ColumnNamesAsync(ApplicationDbContext context, string table)
    {
        var names = new List<string>();

        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"SELECT name FROM pragma_table_info('{table}');";

        await context.Database.OpenConnectionAsync();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    public void Dispose()
    {
        _keepAlive.Dispose();
        GC.SuppressFinalize(this);
    }
}
