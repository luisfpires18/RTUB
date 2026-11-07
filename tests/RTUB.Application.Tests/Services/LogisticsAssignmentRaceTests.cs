using System.Data.Common;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Task 065 (PR review): an assignee's checklist tick or status change and a manager's removal of that assignment must
/// not interleave. On a real SQLite file in WAL mode (as production), the manager's removal holds the write lock,
/// uncommitted, while the former assignee writes: the write must wait for the removal and then be refused, never save.
/// The InMemory provider has no locks, so this needs real SQLite.
/// </summary>
public sealed class LogisticsAssignmentRaceTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"rtub-065-{Guid.NewGuid():N}.db");
    private readonly string _connection;
    private readonly CardUpdates _updates = new();
    private const string Checklist = "[{\"Id\":0,\"Task\":\"Lavar\",\"Done\":false}]";

    public LogisticsAssignmentRaceTests()
    {
        _connection = $"Data Source={_file};Pooling=False";
        using var db = Context();
        db.Database.EnsureCreated();
    }

    [Fact]
    public async Task AWrite_RacingTheRemovalOfItsAssignment_WaitsAndIsRefused()
    {
        var (cardId, userId) = await SeedAsync();
        var service = Service();
        var assignee = Principal(userId);

        // Sanity: while assigned, the tick saves.
        (await service.ToggleChecklistItemAsync(cardId, 0, new LogisticsChecklistToggleInput("Lavar", true), assignee)).Status
            .Should().Be(EventResultStatus.Ok);

        // A manager removes the assignment and has not committed yet: the removal holds the write lock.
        await using var manager = Context();
        await using var removal = await manager.Database.BeginTransactionAsync();
        manager.LogisticsCardAssignments.RemoveRange(await manager.LogisticsCardAssignments.Where(a => a.CardId == cardId).ToListAsync());
        await manager.SaveChangesAsync();

        // Both writes run until they ask for the write lock (which they cannot get yet) before the removal commits. Before
        // the fix that was the save, after a check that still saw the assignment; now it is the transaction around the
        // check (BEGIN IMMEDIATE), so the check runs only after the removal.
        _updates.Expect(2);
        var tick = Task.Run(() => service.ToggleChecklistItemAsync(cardId, 0, new LogisticsChecklistToggleInput("Lavar", false), assignee));
        var status = Task.Run(() => service.SetStatusAsync(cardId, new LogisticsStatusInput("Done"), assignee));
        await _updates.Reached.WaitAsync(TimeSpan.FromMinutes(1)); // a hang guard only, not a timing assertion

        await removal.CommitAsync();
        (await tick).Status.Should().Be(EventResultStatus.Forbidden);
        (await status).Status.Should().Be(EventResultStatus.Forbidden);

        await using var read = Context();
        var card = await read.LogisticsCards.AsNoTracking().SingleAsync(c => c.Id == cardId);
        card.ChecklistJson.Should().Be("[{\"Id\":0,\"Task\":\"Lavar\",\"Done\":true}]", "the refused untick never saved");
        card.Status.Should().Be(Core.Enums.CardStatus.Todo);
    }

    private async Task<(int CardId, string UserId)> SeedAsync()
    {
        await using var db = Context();
        var user = new ApplicationUser { UserName = "leitao", Email = "leitao@rtub.test", FirstName = "L", LastName = "T", Nickname = "Leitão" };
        db.Users.Add(user);
        var board = LogisticsBoard.Create("FITAB");
        db.LogisticsBoards.Add(board);
        await db.SaveChangesAsync();
        var list = LogisticsList.Create("Logística", board.Id, 0);
        db.LogisticsLists.Add(list);
        await db.SaveChangesAsync();
        var card = LogisticsCard.Create("Credenciais", list.Id, 0);
        card.SetChecklist(Checklist);
        db.LogisticsCards.Add(card);
        await db.SaveChangesAsync();
        db.LogisticsCardAssignments.Add(LogisticsCardAssignment.Create(card.Id, user.Id));
        await db.SaveChangesAsync();
        return (card.Id, user.Id);
    }

    private LogisticsKanbanService Service()
    {
        var contexts = new Mock<IDbContextFactory<ApplicationDbContext>>();
        contexts.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => Context(_updates));
        return new LogisticsKanbanService(contexts.Object, Mock.Of<IDocumentStorageService>(), Mock.Of<IHostEnvironment>(), new AuditContext(),
            NullLogger<LogisticsKanbanService>.Instance, new DocumentTreeCache(new MemoryCache(new MemoryCacheOptions())));
    }

    private ApplicationDbContext Context(CardUpdates? updates = null)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).AddInterceptors(new SqliteConnectionInterceptor());
        if (updates is not null)
        {
            builder.AddInterceptors(updates);
        }

        var options = builder.Options;
        return new ApplicationDbContext(options, Mock.Of<IHttpContextAccessor>(), new AuditContext(), new AuditLogAppender());
    }

    private static ClaimsPrincipal Principal(string userId) =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Name, "leitao"), new Claim(ClaimTypes.Role, "Member") }, "Test"));

    /// <summary>Signals once the expected number of write-lock requests (a transaction begin, or an UPDATE of a card) are about to run.</summary>
    private sealed class CardUpdates : DbCommandInterceptor, IDbTransactionInterceptor
    {
        private TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _left = int.MaxValue;

        public Task Reached => _reached.Task;

        public void Expect(int count)
        {
            _reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Volatile.Write(ref _left, count);
        }

        private void Count()
        {
            if (Interlocked.Decrement(ref _left) == 0)
            {
                _reached.TrySetResult();
            }
        }

        private void Seen(DbCommand command)
        {
            if (command.CommandText.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("LogisticsCards", StringComparison.Ordinal))
            {
                Count();
            }
        }

        public ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(DbConnection connection, TransactionStartingEventData eventData,
            InterceptionResult<DbTransaction> result, CancellationToken cancellationToken = default)
        {
            Count();
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Seen(command);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Seen(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var path in new[] { _file, _file + "-wal", _file + "-shm" })
        {
            File.Delete(path);
        }
    }
}
