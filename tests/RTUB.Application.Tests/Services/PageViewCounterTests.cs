using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Services;
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Task 089: page views are counted from the Referer of /api/account/me, normalised (ids → {id}, no query, same origin
/// only), buffered per Portugal day and upserted on flush; a failed flush keeps the counts.
/// </summary>
public sealed class PageViewCounterTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly IDbContextFactory<ApplicationDbContext> _contexts;

    public PageViewCounterTests()
    {
        _connection.Open();
        var factory = new Mock<IDbContextFactory<ApplicationDbContext>>();
        factory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => Context());
        _contexts = factory.Object;
        using var db = Context();
        db.Database.EnsureCreated();
    }

    [Theory]
    [InlineData("https://rtub.pt/events/42?tab=1#x", "rtub.pt", "/events/{id}")]
    [InlineData("https://rtub.pt/events/42/discussion", "rtub.pt", "/events/{id}/discussion")]
    [InlineData("https://rtub.pt/Treasury/Reports/7/", "rtub.pt", "/treasury/reports/{id}")]
    [InlineData("https://rtub.pt/", "rtub.pt", "/")]
    [InlineData("https://localhost:5178/profile", "localhost:5178", "/profile")]
    [InlineData("https://evil.example/events", "rtub.pt", null)]
    [InlineData("", "rtub.pt", null)]
    [InlineData("not a url", "rtub.pt", null)]
    public void Normalise_KeepsTheRouteOnly_SameOriginOnly(string referer, string host, string? expected) =>
        PageViewCounter.Normalise(referer, host).Should().Be(expected);

    [Fact]
    public void Normalise_RefusesAnOverlongPath()
    {
        PageViewCounter.Normalise("https://rtub.pt/" + new string('a', 200), "rtub.pt").Should().BeNull();
    }

    [Fact]
    public async Task Flush_UpsertsPerDayAndPath_AndDrainsTheBuffer()
    {
        var time = new Mock<TimeProvider>();
        time.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(2026, 11, 7, 10, 0, 0, TimeSpan.Zero));
        var counter = new PageViewCounter(time.Object);
        counter.Record("https://rtub.pt/events/1", "rtub.pt");
        counter.Record("https://rtub.pt/events/2", "rtub.pt");
        counter.Record("https://rtub.pt/music", "rtub.pt");
        counter.Record("https://other.example/music", "rtub.pt");

        (await counter.FlushAsync(_contexts)).Should().Be(2);
        counter.Record("https://rtub.pt/events/3", "rtub.pt");
        (await counter.FlushAsync(_contexts)).Should().Be(1, "only what was counted since the last flush");
        (await counter.FlushAsync(_contexts)).Should().Be(0);

        await using var db = Context();
        var rows = await db.PageViews.AsNoTracking().OrderBy(v => v.Path).ToListAsync();
        rows.Select(r => (r.Path, r.Count, r.Day)).Should().Equal(
            ("/events/{id}", 3, new DateTime(2026, 11, 7)),
            ("/music", 1, new DateTime(2026, 11, 7)));
    }

    [Fact]
    public void Record_CapsDistinctPaths_InOneOtherBucket()
    {
        var counter = new PageViewCounter();
        for (var i = 0; i < PageViewCounter.MaxKeys + 50; i++)
        {
            counter.Record($"https://rtub.pt/made-up-{i}", "rtub.pt");
        }

        counter.Record("https://rtub.pt/made-up-0", "rtub.pt"); // known already: still its own key

        var drained = counter.Drain();
        drained.Should().HaveCount(PageViewCounter.MaxKeys + 1);
        drained.Single(d => d.Path == PageViewCounter.Other).Count.Should().Be(50);
        drained.Single(d => d.Path == "/made-up-0").Count.Should().Be(2);
    }

    [Fact]
    public async Task Flush_KeepsTheCountsWhenTheWriteFails()
    {
        var counter = new PageViewCounter();
        counter.Record("https://rtub.pt/news", "rtub.pt");
        var broken = new Mock<IDbContextFactory<ApplicationDbContext>>();
        broken.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("locked"));

        var act = () => counter.FlushAsync(broken.Object);

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await counter.FlushAsync(_contexts)).Should().Be(1, "the count survived the failed flush");
    }

    private ApplicationDbContext Context()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        return new ApplicationDbContext(options, Mock.Of<IHttpContextAccessor>(), new AuditContext(), new AuditLogAppender());
    }

    public void Dispose() => _connection.Dispose();
}
