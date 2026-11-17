// MigrationChainTests: TaskSchemas tests.
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace RTUB.Integration.Tests;

public partial class MigrationChainTests
{
    /// <summary>
    /// Task 066: a card written before AddLogisticsCardWorkspace comes out Normal (1), version 0, with its owner,
    /// assignment rows, checklist and dates untouched; the new tables cascade with the card and set the author null when
    /// an account is deleted. The migration only adds.
    /// </summary>
    [Fact]
    public async Task Task066_LegacyCards_BecomeNormalPriority_AndKeepTheirData()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync("20261101211252_AddUserPasskeys");
        // ExecuteSqlRaw formats its text: the JSON's braces are doubled.
        await context.Database.ExecuteSqlRawAsync("""
            INSERT INTO LogisticsBoards (Name, Description, IsCompleted, CreatedAt) VALUES ('FITAB', '', 0, '2026-01-01');
            INSERT INTO LogisticsLists (Name, BoardId, Position, CreatedAt) VALUES ('Logística', 1, 0, '2026-01-01');
            INSERT INTO LogisticsCards (Title, Description, ListId, Position, Status, ChecklistJson, DueDate, CreatedAt)
                VALUES ('Antigo', 'Texto', 1, 0, 1, '[{{"Id":0,"Task":"Lavar","Done":true}}]', '2026-10-09', '2026-01-01');
            """);
        await context.Database.MigrateAsync();

        (await ScalarsAsync(context, "SELECT Priority || ':' || Version || ':' || Status || ':' || Title || ':' || ChecklistJson || ':' || DueDate FROM LogisticsCards;"))
            .Should().Equal("1:0:1:Antigo:[{\"Id\":0,\"Task\":\"Lavar\",\"Done\":true}]:2026-10-09");
        foreach (var (table, author) in new[] { ("LogisticsCardComments", "AuthorId"), ("LogisticsCardActivities", "ActorId"), ("LogisticsCardAttachments", "UploadedById") })
        {
            (await ScalarsAsync(context, $"SELECT \"table\" || ':' || \"from\" || ':' || on_delete FROM pragma_foreign_key_list('{table}') ORDER BY \"from\";"))
                .Should().BeEquivalentTo($"AspNetUsers:{author}:SET NULL", "LogisticsCards:CardId:CASCADE");
        }
    }

    /// <summary>
    /// Task 067: AddLogisticsBoardAccess only adds. A board written before it is members-visible, not archived, its file
    /// folder unresolved (null) and its columns unmapped (State null) until the startup mapping (LogisticsWorkflow); the
    /// access list goes with the board or the account. No table is rebuilt.
    /// </summary>
    [Fact]
    public async Task Task067_LegacyBoards_StayMembersVisible_AndAwaitTheWorkflowMapping()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync("20261106105508_AddLogisticsCardWorkspace");
        await context.Database.ExecuteSqlRawAsync("""
            INSERT INTO LogisticsBoards (Name, Description, IsCompleted, CreatedAt) VALUES ('FITAB', '', 0, '2026-01-01');
            INSERT INTO LogisticsLists (Name, BoardId, Position, CreatedAt) VALUES ('Logística', 1, 0, '2026-01-01');
            INSERT INTO LogisticsCards (Title, Description, ListId, Position, Status, Priority, Version, CreatedAt)
                VALUES ('Antigo', 'Texto', 1, 0, 2, 1, 0, '2026-01-01');
            """);
        await context.Database.MigrateAsync();

        (await ScalarsAsync(context, "SELECT Visibility || ':' || IFNULL(ArchivedAt, '-') || ':' || IFNULL(LegacyFolder, 'null') || ':' || IFNULL(OwnerUserId, '-') FROM LogisticsBoards;"))
            .Should().Equal("0:-:null:-");
        (await ScalarsAsync(context, "SELECT IFNULL(State, 'null') FROM LogisticsLists;")).Should().Equal("null");
        (await ScalarsAsync(context, "SELECT Status || ':' || IFNULL(ArchivedAt, '-') FROM LogisticsCards;")).Should().Equal("2:-");
        (await ScalarsAsync(context, "SELECT \"table\" || ':' || \"from\" || ':' || on_delete FROM pragma_foreign_key_list('LogisticsBoardMembers') ORDER BY \"from\";"))
            .Should().Equal("LogisticsBoards:BoardId:CASCADE", "AspNetUsers:UserId:CASCADE");
    }

    /// <summary>
    /// Task 069 (AddLogisticsReminderDelivery, additive): a pre-069 reminder keeps its schedule and comes out not yet
    /// activated (ActivatedAt null), unclaimed, with no creator and not personal - so delivery takes it on once, without
    /// replaying the past, and only managers may change it.
    /// </summary>
    [Fact]
    public async Task Task069_LegacyReminders_AwaitActivation_AndStayManagers()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync("20261106114520_AddLogisticsBoardAccess");
        await context.Database.ExecuteSqlRawAsync("""
            INSERT INTO LogisticsBoards (Name, Description, IsCompleted, CreatedAt, Visibility) VALUES ('FITAB', '', 0, '2026-01-01', 0);
            INSERT INTO LogisticsLists (Name, BoardId, Position, CreatedAt) VALUES ('Logística', 1, 0, '2026-01-01');
            INSERT INTO LogisticsCards (Title, Description, ListId, Position, Status, Priority, Version, CreatedAt)
                VALUES ('Antigo', '', 1, 0, 0, 1, 0, '2026-01-01');
            INSERT INTO LogisticsCardReminders (CardId, Frequency, TargetUserIds, NextReminderDate, IsActive, CreatedAt)
                VALUES (1, 1, 'a,b', '2026-02-01 10:00:00', 1, '2026-01-01');
            """);
        await context.Database.MigrateAsync();

        (await ScalarsAsync(context, """
            SELECT Frequency || ':' || TargetUserIds || ':' || NextReminderDate || ':' || IsActive || ':' || IFNULL(ActivatedAt, 'null') || ':'
                || IFNULL(ClaimedUntil, 'null') || ':' || Attempts || ':' || IFNULL(CreatedByUserId, 'null') || ':' || Personal
            FROM LogisticsCardReminders;
            """)).Should().Equal("1:a,b:2026-02-01 10:00:00:1:null:null:0:null:0");
    }

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
}
