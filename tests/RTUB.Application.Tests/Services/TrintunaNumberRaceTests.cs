using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Services;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// #368 (Codex rounds 2-3): a create that loses the numbering race retries on the unique <c>Gatherings.Number</c>
/// clash and when SQLite reports busy or locked, whether EF wraps the error or not. Other constraints (organizer FK,
/// poll-date index) and other errors take the normal path.
/// </summary>
public class TrintunaNumberRaceTests
{
    private const string NumberClash = "SQLite Error 19: 'UNIQUE constraint failed: Gatherings.Number'.";

    [Theory]
    [InlineData(19, NumberClash)]
    [InlineData(5, "SQLite Error 5: 'database is locked'.")] // SQLITE_BUSY
    [InlineData(6, "SQLite Error 6: 'database table is locked'.")] // SQLITE_LOCKED
    public void LostRace_IsRetried_BareOrWrapped(int code, string message)
    {
        var sqlite = new SqliteException(message, code);

        TrintunaService.IsNumberRace(sqlite).Should().BeTrue();
        TrintunaService.IsNumberRace(new DbUpdateException("save", sqlite)).Should().BeTrue();
    }

    [Theory]
    [InlineData("SQLite Error 19: 'FOREIGN KEY constraint failed'.")]
    [InlineData("SQLite Error 19: 'UNIQUE constraint failed: GatheringPollOptions.GatheringId, GatheringPollOptions.Date'.")]
    [InlineData("SQLite Error 19: 'NOT NULL constraint failed: Gatherings.Place'.")]
    public void OtherConstraints_AreNotRetried(string message)
    {
        var sqlite = new SqliteException(message, 19);

        TrintunaService.IsNumberRace(sqlite).Should().BeFalse();
        TrintunaService.IsNumberRace(new DbUpdateException("save", sqlite)).Should().BeFalse();
    }

    [Fact]
    public void OtherErrors_AreNotRetried()
    {
        TrintunaService.IsNumberRace(new SqliteException("io", 10)).Should().BeFalse();
        TrintunaService.IsNumberRace(new DbUpdateException("save", new InvalidOperationException())).Should().BeFalse();
        TrintunaService.IsNumberRace(new InvalidOperationException()).Should().BeFalse();
    }
}
