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
    public void Model_HasNoChangesThatAMigrationWouldHaveToCapture()
    {
        using var context = CreateContext();

        // React work adds DTOs and APIs, rarely schema (003 reused Request; 025 added NewsPosts with its
        // migration): the EF model must still match the latest migration snapshot exactly.
        context.Database.HasPendingModelChanges().Should().BeFalse(
            "a model change without its migration would silently diverge from every deployed database");
    }

    [Fact]
    public async Task MigratedDatabase_RequestsTableIsUnchanged()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        // The React request form maps onto exactly these columns; none were added for it.
        (await ColumnNamesAsync(context, "Requests")).Should().BeEquivalentTo(
            "Id", "Name", "Email", "Phone", "EventType", "PreferredDate", "PreferredEndDate", "IsDateRange",
            "Location", "Message", "Status", "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy");
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

    /// <summary>
    /// React track 025 (Novidades): the only schema change is the new NewsPosts table. A deleted account leaves its
    /// posts with no author (ON DELETE SET NULL) and the feed is read by PublishedAt.
    /// </summary>
    [Fact]
    public async Task MigratedDatabase_HasNewsPosts_WithAuthorSetNullAndPublishedAtIndex()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        (await ColumnNamesAsync(context, "NewsPosts")).Should().BeEquivalentTo(
            "Id", "Title", "Body", "PublishedAt", "AuthorId", "IsPublic", "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy");
        (await ScalarsAsync(context, "SELECT \"table\" || ':' || \"from\" || ':' || on_delete FROM pragma_foreign_key_list('NewsPosts');"))
            .Should().Equal("AspNetUsers:AuthorId:SET NULL");
        (await ScalarsAsync(context, "SELECT name FROM pragma_index_list('NewsPosts');"))
            .Should().Contain("IX_NewsPosts_PublishedAt");
    }

    /// <summary>
    /// Messaging v2 (task 041) runs on the v1 tables that task 027 preserved, with no migration of its own: the four
    /// tables, the reply / read / mute / pin / announcement columns and the one-reaction-per-member index must all still
    /// come out of the migration chain. Messaging tables are never dropped.
    /// </summary>
    [Fact]
    public async Task MigratedDatabase_KeepsTheMessagingTables_ThatMessagingV2Uses()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        (await ColumnNamesAsync(context, "Conversations")).Should().Contain(new[]
            { "Id", "Participants", "LastMessageAt", "LastMessageId", "Title", "IsSystemConversation", "IsArchived", "IsGroup",
              "CreatedByUserId", "IsAnnouncementOnly" });
        (await ColumnNamesAsync(context, "Messages")).Should().Contain(new[]
            { "Id", "ConversationId", "SenderId", "Body", "IsSystem", "ReadBy", "Link", "ReplyToMessageId", "ReplyToBody",
              "ReplyToSenderName", "CreatedAt" });
        (await ColumnNamesAsync(context, "MessageReactions")).Should().Contain(new[] { "Id", "MessageId", "UserId", "Emoji" });
        (await ColumnNamesAsync(context, "ConversationUserSettings")).Should().Contain(new[]
            { "Id", "ConversationId", "UserId", "IsMuted", "IsPinned" });
        (await ScalarsAsync(context, "SELECT name FROM pragma_index_list('MessageReactions') WHERE \"unique\" = 1;"))
            .Should().Contain("IX_MessageReaction_MessageId_UserId");
    }

    /// <summary>Task 043: the retired Games / Bets / MyTuno, Labels and Slideshows tables (docs/db-cleanup-inventory.md).</summary>
    private static readonly string[] RetiredTables =
    [
        "Bets", "BetOptions", "UserBets", "BetComments", "Games", "GameScores", "Characters", "InventoryItems",
        "ForgedWeapons", "StageProgresses", "StageEnemies", "BossModeProgresses", "SurviveModeProgresses",
        "ItemTypeConfigs", "ForgeComboConfigs", "Labels", "Slideshows",
    ];

    private static readonly string[] IdentityTables =
        ["AspNetUsers", "AspNetRoles", "AspNetUserRoles", "AspNetUserClaims", "AspNetUserLogins", "AspNetUserTokens", "AspNetRoleClaims"];

    private static readonly string[] MessagingTables = ["Conversations", "Messages", "MessageReactions", "ConversationUserSettings"];

    /// <summary>
    /// Task 043 dropped the retired feature schema and <c>AspNetUsers.FidelisBalance</c>, and nothing else: Identity and
    /// the messaging tables stay. Its Down path (local / DEV schema testing only; production rolls back by restoring
    /// the pre-migration backup) must bring the tables back in the EF model's shape, and Up must drop them again.
    /// </summary>
    [Fact]
    public async Task Task043_DropsOnlyTheRetiredSchema_AndItsDownPathRestoresIt()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        async Task<List<string>> TablesAsync() => await ScalarsAsync(context, "SELECT name FROM sqlite_master WHERE type = 'table';");

        (await TablesAsync()).Should().NotContain(RetiredTables).And.Contain(IdentityTables).And.Contain(MessagingTables);
        (await ColumnNamesAsync(context, "AspNetUsers")).Should().NotContain("FidelisBalance").And.Contain("PasswordHash");

        await context.Database.MigrateAsync("20261005175342_AddNewsPosts");
        (await TablesAsync()).Should().Contain(RetiredTables).And.Contain(MessagingTables);
        (await ColumnNamesAsync(context, "AspNetUsers")).Should().Contain("FidelisBalance");
        (await ScalarsAsync(context, "SELECT \"table\" || ':' || \"from\" || ':' || on_delete FROM pragma_foreign_key_list('BetComments');"))
            .Should().Contain("AspNetUsers:AuthorId:RESTRICT", "Down recreates the keys too");

        await context.Database.MigrateAsync();
        (await TablesAsync()).Should().NotContain(RetiredTables).And.Contain(MessagingTables);
        (await ColumnNamesAsync(context, "AspNetUsers")).Should().NotContain("FidelisBalance");
    }

    /// <summary>
    /// #227 Events.IsPublic: every event that exists before the migration stays public, and so does an event the
    /// previous release (N-1, which never writes the column) inserts after an app rollback. No row is lost.
    /// </summary>
    [Fact]
    public async Task AddEventIsPublic_KeepsExistingEventsPublic_AndDefaultsN1InsertsToPublic()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync("20261013093143_RemoveFidelisBalanceFromApplicationUser");

        const string insert = "INSERT INTO Events (Name, Date, Description, Location, Type, IsCancelled, CreatedAt) " +
                              "VALUES ('{0}', '2026-12-01 21:00:00', '', 'Bragança', 0, {1}, '2026-10-01 00:00:00');";
        await context.Database.ExecuteSqlRawAsync(string.Format(insert, "Antes", 0));
        await context.Database.ExecuteSqlRawAsync(string.Format(insert, "Antes cancelada", 1));

        await context.Database.MigrateAsync();
        (await ColumnNamesAsync(context, "Events")).Should().Contain("IsPublic");

        // What N-1 would insert after a rollback: it does not know the column.
        await context.Database.ExecuteSqlRawAsync(string.Format(insert, "Da versão anterior", 0));

        (await ScalarsAsync(context, "SELECT Name || ':' || IsPublic FROM Events ORDER BY Id;"))
            .Should().Equal("Antes:1", "Antes cancelada:1", "Da versão anterior:1");
    }

    /// <summary>
    /// #222 NewsPosts.IsPublic: every post that exists before the migration (drafts included) stays public, untouched,
    /// and so does a post the previous release (N-1, which never writes the column) inserts after an app rollback.
    /// </summary>
    [Fact]
    public async Task AddNewsPostIsPublic_KeepsExistingPostsPublic_AndDefaultsN1InsertsToPublic()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync("20261014225351_AddEventIsPublic");

        const string insert = "INSERT INTO NewsPosts (Title, Body, PublishedAt, CreatedAt) VALUES ('{0}', 'Texto {0}', {1}, '2026-10-01 00:00:00');";
        await context.Database.ExecuteSqlRawAsync(string.Format(insert, "Antes", "'2026-10-02 10:00:00'"));
        await context.Database.ExecuteSqlRawAsync(string.Format(insert, "Rascunho", "NULL"));

        await context.Database.MigrateAsync();
        (await ColumnNamesAsync(context, "NewsPosts")).Should().Contain("IsPublic");

        await context.Database.ExecuteSqlRawAsync(string.Format(insert, "Da versão anterior", "NULL"));

        (await ScalarsAsync(context, "SELECT Title || ':' || Body || ':' || IsPublic || ':' || ifnull(PublishedAt, '-') FROM NewsPosts ORDER BY Id;"))
            .Should().Equal("Antes:Texto Antes:1:2026-10-02 10:00:00", "Rascunho:Texto Rascunho:1:-", "Da versão anterior:Texto Da versão anterior:1:-");
    }

    private static async Task<List<string>> ScalarsAsync(ApplicationDbContext context, string sql)
    {
        var values = new List<string>();

        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;

        await context.Database.OpenConnectionAsync();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return values;
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
