// MigrationChainTests: AddedColumns tests.
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Web.Extensions;
using Xunit;

namespace RTUB.Integration.Tests;

public partial class MigrationChainTests
{
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

    /// <summary>
    /// #221 Products.AvailableAllYears: every product that exists before the migration stays year-specific, and so
    /// does a product the previous release (N-1, which never writes the column) inserts after an app rollback. Prices,
    /// stock, dates and the reservations (the shop's only order history) are untouched.
    /// </summary>
    [Fact]
    public async Task AddProductAvailableAllYears_KeepsExistingProductsYearSpecific_AndLeavesReservationsUntouched()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync("20261015181226_AddNewsPostIsPublic");

        // A minimal member for the reservation's key: every NOT NULL column without a default gets a placeholder.
        var required = await ScalarsAsync(context,
            "SELECT name || ':' || type FROM pragma_table_info('AspNetUsers') WHERE \"notnull\" = 1 AND dflt_value IS NULL;");
        var columns = required.Select(c => c.Split(':')[0]).ToList();
        var values = required.Select(c => c.Split(':') switch
        {
            ["Id", _] => "'u1'",
            [_, "INTEGER"] => "0",
            _ => "'x'",
        });
        var insertUser = $"INSERT INTO AspNetUsers ({string.Join(", ", columns.Select(c => $"\"{c}\""))}) VALUES ({string.Join(", ", values)});";
        await context.Database.ExecuteSqlRawAsync(insertUser);

        const string product = "INSERT INTO Products (Name, Type, Price, Stock, IsAvailable, IsPublic, CreatedAt) " +
                               "VALUES ('{0}', 'Album', '10.5', {1}, {2}, 0, '2025-10-01 00:00:00');";
        await context.Database.ExecuteSqlRawAsync(string.Format(product, "CD 2025", 3, 1));
        await context.Database.ExecuteSqlRawAsync(string.Format(product, "CD esgotado", 0, 0));
        await context.Database.ExecuteSqlRawAsync("INSERT INTO ProductReservations (ProductId, UserId, UserNickname, HasSizes, Size, CreatedAt) " +
                                                  "VALUES (1, 'u1', 'ze', 1, 'M', '2025-10-02 12:00:00');");

        const string products = "SELECT Name || ':' || Price || ':' || Stock || ':' || IsAvailable || ':' || IsPublic || ':' || CreatedAt FROM Products ORDER BY Id;";
        const string reservations = "SELECT ProductId || ':' || UserId || ':' || UserNickname || ':' || HasSizes || ':' || Size || ':' || CreatedAt FROM ProductReservations ORDER BY Id;";
        var productsBefore = await ScalarsAsync(context, products);
        var reservationsBefore = await ScalarsAsync(context, reservations);

        await context.Database.MigrateAsync();
        (await ColumnNamesAsync(context, "Products")).Should().Contain("AvailableAllYears");

        // What N-1 would insert after a rollback: it does not know the column.
        await context.Database.ExecuteSqlRawAsync(string.Format(product, "Da versão anterior", 1, 1));

        (await ScalarsAsync(context, products)).Take(2).Should().Equal(productsBefore);
        (await ScalarsAsync(context, reservations)).Should().Equal(reservationsBefore);
        (await ScalarsAsync(context, "SELECT Name || ':' || AvailableAllYears FROM Products ORDER BY Id;"))
            .Should().Equal("CD 2025:0", "CD esgotado:0", "Da versão anterior:0");
    }
}
