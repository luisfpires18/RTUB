using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Services;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// #368 (Codex round 2): a create that loses the numbering race retries on a unique-index clash and also when SQLite
/// reports busy or locked, whether EF wraps the error or not. Other errors are not retried.
/// </summary>
public class TrintunaNumberRaceTests
{
    [Theory]
    [InlineData(19)] // SQLITE_CONSTRAINT: the unique Number index
    [InlineData(5)] // SQLITE_BUSY
    [InlineData(6)] // SQLITE_LOCKED
    public void LostRace_IsRetried_BareOrWrapped(int code)
    {
        var sqlite = new SqliteException("race", code);

        TrintunaService.IsNumberRace(sqlite).Should().BeTrue();
        TrintunaService.IsNumberRace(new DbUpdateException("save", sqlite)).Should().BeTrue();
    }

    [Fact]
    public void OtherErrors_AreNotRetried()
    {
        TrintunaService.IsNumberRace(new SqliteException("io", 10)).Should().BeFalse();
        TrintunaService.IsNumberRace(new DbUpdateException("save", new InvalidOperationException())).Should().BeFalse();
        TrintunaService.IsNumberRace(new InvalidOperationException()).Should().BeFalse();
    }
}
