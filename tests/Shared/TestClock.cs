namespace RTUB.Tests;

/// <summary>
/// A clock that reads what the test says, for code that takes a <see cref="TimeProvider"/>. Set <see cref="Now"/> to
/// move it. Source-linked into every test project by <c>tests/Directory.Build.props</c>.
/// </summary>
internal sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;

    /// <summary>A clock at this UTC time. Portugal is UTC in winter and UTC+1 in summer.</summary>
    public static TestClock AtUtc(int year, int month, int day, int hour = 0, int minute = 0) =>
        new(new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero));
}
