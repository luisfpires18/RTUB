using System.Data.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Task 082 (#263): <see cref="PushPreferenceService.SaveAsync"/> over real SQLite, for what the API tests cannot
/// provoke: a write that fails. Only a conflict that leaves exactly the requested rows is success; any other failure
/// is thrown, so the endpoint never answers 200 with the old choices still stored.
/// </summary>
public sealed class PushPreferenceServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly IDbContextFactory<ApplicationDbContext> _contexts;

    public PushPreferenceServiceTests()
    {
        _connection.Open();
        var factory = new Mock<IDbContextFactory<ApplicationDbContext>>();
        factory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => Context());
        _contexts = factory.Object;
        using var db = Context();
        db.Database.EnsureCreated();
    }

    [Fact]
    public async Task Save_StoresTheRequestedKeys_AndRemovesTheOthers()
    {
        var userId = await MemberAsync();
        var service = new PushPreferenceService(_contexts);

        var first = await service.SaveAsync(userId, [PushPreferenceCatalog.EventsNew, PushPreferenceCatalog.Messages]);
        Off(first).Should().BeEquivalentTo([PushPreferenceCatalog.EventsNew, PushPreferenceCatalog.Messages]);

        var second = await service.SaveAsync(userId, [PushPreferenceCatalog.Messages]);
        Off(second).Should().Equal(PushPreferenceCatalog.Messages);
    }

    [Fact]
    public async Task Save_WhenAnotherTabStoredTheSameKeyFirst_IsStillASuccess()
    {
        var userId = await MemberAsync();
        // The "other tab" writes the same row between this save's read and its INSERT, so the unique index refuses
        // the insert; what is stored is exactly what was asked, so that is a success, not an error.
        var race = new RaceOnInsert(() => Context(), userId);
        var factory = new Mock<IDbContextFactory<ApplicationDbContext>>();
        factory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => Context(race));
        var service = new PushPreferenceService(factory.Object);

        var result = await service.SaveAsync(userId, [PushPreferenceCatalog.EventsNew]);

        race.Fired.Should().BeTrue();
        result.Errors.Should().BeNull();
        Off(result).Should().Equal(PushPreferenceCatalog.EventsNew);
        await using var check = Context();
        (await check.PushPreferences.CountAsync(p => p.UserId == userId)).Should().Be(1);
    }

    [Fact]
    public async Task Save_WhenTheWriteFails_Throws_InsteadOfAnsweringWithTheOldChoices()
    {
        var userId = await MemberAsync();
        var service = new PushPreferenceService(_contexts);
        (await service.SaveAsync(userId, [PushPreferenceCatalog.Messages])).Errors.Should().BeNull();
        await using (var db = Context())
        {
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER refuse BEFORE INSERT ON PushPreferences BEGIN SELECT RAISE(ABORT, 'disk full'); END;");
        }

        var act = () => service.SaveAsync(userId, [PushPreferenceCatalog.EventsNew]);

        await act.Should().ThrowAsync<DbUpdateException>();
        await using var check = Context();
        (await check.PushPreferences.Where(p => p.UserId == userId).Select(p => p.Key).ToListAsync())
            .Should().Equal(new[] { PushPreferenceCatalog.Messages }, "the old choice is still stored, and the caller was told the save failed");
    }

    private static string[] Off(PushPreferenceService.SaveResult result) =>
        result.Items!.Where(i => !i.Enabled).Select(i => i.Key).ToArray();

    private sealed class RaceOnInsert(Func<ApplicationDbContext> other, string userId) : DbCommandInterceptor
    {
        public bool Fired { get; private set; }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (!Fired && command.CommandText.Contains("INSERT INTO \"PushPreferences\"", StringComparison.Ordinal))
            {
                Fired = true;
                await using var db = other();
                db.PushPreferences.Add(new PushPreference { UserId = userId, Key = PushPreferenceCatalog.EventsNew, MutedAt = DateTime.UtcNow });
                await db.SaveChangesAsync(cancellationToken);
            }

            return result;
        }
    }

    private ApplicationDbContext Context(DbCommandInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection);
        if (interceptor is not null) builder.AddInterceptors(interceptor);
        var options = builder.Options;
        return new ApplicationDbContext(options, Mock.Of<IHttpContextAccessor>(), new AuditContext(), new AuditLogAppender());
    }

    private async Task<string> MemberAsync()
    {
        await using var db = Context();
        var user = new ApplicationUser { UserName = $"u{Guid.NewGuid():N}"[..12], Email = "u@rtub.test", FirstName = "Nome", LastName = "Apelido", Nickname = "Nick" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    public void Dispose() => _connection.Dispose();
}
