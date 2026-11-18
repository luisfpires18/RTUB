// MigrationChainTests: the squashed chain (task 130) and how existing databases adopt the Baseline.
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;

namespace RTUB.Integration.Tests;

public partial class MigrationChainTests
{
    [Fact]
    public async Task ADatabaseThatRanTheSquashedChain_AdoptsTheBaseline_KeepsItsOldRows_AndRunsOnlyLaterMigrations()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        await AsOldChainAsync(context, "20261101211252_AddUserPasskeys", MigrationBaseline.LastSquashedId);

        await MigrationBaseline.AdoptAsync(context.Database);

        (await context.Database.GetPendingMigrationsAsync()).Should().NotBeEmpty("the later migrations still run")
            .And.OnlyContain(id => string.CompareOrdinal(id, MigrationBaseline.BaselineId) > 0, "the Baseline itself is not run again");
        (await ScalarsAsync(context, "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId")).Should().Equal(
            "20261101211252_AddUserPasskeys", MigrationBaseline.LastSquashedId, MigrationBaseline.BaselineId);

        await MigrationBaseline.AdoptAsync(context.Database);
        (await ScalarsAsync(context, "SELECT MigrationId FROM __EFMigrationsHistory")).Should().HaveCount(3, "adopting twice adds nothing");
        await context.Database.MigrateAsync();
        (await context.Database.GetPendingMigrationsAsync()).Should().BeEmpty("the later migrations apply on top of the adopted Baseline");
    }

    [Fact]
    public async Task ADatabaseFromBeforeTheSquashPoint_IsRefused_AndLeftAsItWas()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        await AsOldChainAsync(context, "20261101211252_AddUserPasskeys");

        var adopt = () => MigrationBaseline.AdoptAsync(context.Database);

        (await adopt.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*20261101211252_AddUserPasskeys*2.3.12 or older*");
        (await ScalarsAsync(context, "SELECT MigrationId FROM __EFMigrationsHistory")).Should().Equal("20261101211252_AddUserPasskeys");
    }

    [Fact]
    public async Task AFreshDatabase_RunsOnlyTheBaseline()
    {
        await using var context = CreateContext();

        await MigrationBaseline.AdoptAsync(context.Database);

        (await context.Database.GetPendingMigrationsAsync()).Should().StartWith(MigrationBaseline.BaselineId);
    }

    /// <summary>Makes the history look like a database built by the old chain, whose last rows were these.</summary>
    private static async Task AsOldChainAsync(ApplicationDbContext context, params string[] oldIds)
    {
        await context.Database.ExecuteSqlRawAsync("DELETE FROM __EFMigrationsHistory");
        foreach (var id in oldIds)
        {
            await context.Database.ExecuteSqlAsync($"INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ({id}, '10.0.0')");
        }
    }
}
