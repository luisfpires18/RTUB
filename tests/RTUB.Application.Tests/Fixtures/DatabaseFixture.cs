using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Services;

namespace RTUB.Application.Tests.Fixtures;

/// <summary>
/// Shared database fixture for test collections
/// Reuses a single InMemoryDatabase instance across multiple tests for better performance
/// </summary>
public class DatabaseFixture : IDisposable
{
    private readonly string _databaseName;
    private bool _disposed;

    public DatabaseFixture()
    {
        // Use a single database name for this fixture instance
        // Each test collection will get its own fixture instance
        _databaseName = $"TestDb_{Guid.NewGuid()}";
    }

    /// <summary>
    /// Creates a new DbContext instance using the shared database
    /// </summary>
    public ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: _databaseName)
            .EnableSensitiveDataLogging()
            .Options;

        return new ApplicationDbContext(
            options,
            Mock.Of<IHttpContextAccessor>(),
            new AuditContext());
    }

    /// <summary>
    /// Cleans the database between tests to ensure isolation
    /// Much faster than creating a new database
    /// Note: Does not clean Users table to allow shared test users
    /// </summary>
    public async Task CleanDatabase(ApplicationDbContext context)
    {
        // Remove all data but keep the database structure
        // Note: Not cleaning Users table to allow shared test user fixtures
        context.EventRepertoires.RemoveRange(context.EventRepertoires);
        context.SongYouTubeUrls.RemoveRange(context.SongYouTubeUrls);
        context.Songs.RemoveRange(context.Songs);
        context.Albums.RemoveRange(context.Albums);
        context.RehearsalAttendances.RemoveRange(context.RehearsalAttendances);
        context.Rehearsals.RemoveRange(context.Rehearsals);
        context.Enrollments.RemoveRange(context.Enrollments);
        context.Events.RemoveRange(context.Events);
        context.MeetingRequests.RemoveRange(context.MeetingRequests);
        context.Meetings.RemoveRange(context.Meetings);
        context.MemberInstruments.RemoveRange(context.MemberInstruments);
        context.Instruments.RemoveRange(context.Instruments);
        context.Comments.RemoveRange(context.Comments);
        context.Posts.RemoveRange(context.Posts);
        context.Discussions.RemoveRange(context.Discussions);
        context.ProductReservations.RemoveRange(context.ProductReservations);
        context.Products.RemoveRange(context.Products);
        context.Transactions.RemoveRange(context.Transactions);
        context.Activities.RemoveRange(context.Activities);
        context.Reports.RemoveRange(context.Reports);
        context.LeaderboardCommentLikes.RemoveRange(context.LeaderboardCommentLikes);
        context.LeaderboardComments.RemoveRange(context.LeaderboardComments);
        context.RoleAssignments.RemoveRange(context.RoleAssignments);
        context.Trophies.RemoveRange(context.Trophies);
        context.AuditLogs.RemoveRange(context.AuditLogs);
        context.LogisticsCards.RemoveRange(context.LogisticsCards);
        context.LogisticsLists.RemoveRange(context.LogisticsLists);
        context.LogisticsBoards.RemoveRange(context.LogisticsBoards);
        context.Slideshows.RemoveRange(context.Slideshows);
        context.Labels.RemoveRange(context.Labels);
        context.Requests.RemoveRange(context.Requests);
        context.FiscalYears.RemoveRange(context.FiscalYears);

        await context.SaveChangesAsync();
    }

    public void Dispose()
    {
        if (_disposed) return;

        // Cleanup is handled by InMemoryDatabase disposal
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
