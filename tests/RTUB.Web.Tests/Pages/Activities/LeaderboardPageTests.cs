using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MockQueryable.Moq;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Pages.Activities;
using RTUB.Web.Tests.Pages.Base;

namespace RTUB.Web.Tests.Pages.Activities;

/// <summary>
/// Component tests for Leaderboard.razor page
/// Tests page rendering, loading state, and leaderboard display
/// </summary>
public class LeaderboardPageTests : PageTestBase
{
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<IRankingService> _mockRankingService;
    private readonly Mock<ILabelService> _mockLabelService;
    private readonly Mock<ILeaderboardCommentService> _mockLeaderboardCommentService;
    private readonly Mock<IMemberStatisticsService> _mockMemberStatisticsService;
    private readonly Mock<IFiscalYearService> _mockFiscalYearService;
    private readonly Mock<Microsoft.Extensions.Options.IOptions<RTUB.Application.Configuration.RankingConfiguration>> _mockRankingConfig;

    public LeaderboardPageTests()
    {
        // Setup service mocks
        _mockUserManager = SetupUserManager();
        _mockRankingService = SetupService<IRankingService>();
        _mockLabelService = SetupService<ILabelService>();
        _mockLeaderboardCommentService = SetupService<ILeaderboardCommentService>();
        _mockMemberStatisticsService = SetupService<IMemberStatisticsService>();
        _mockFiscalYearService = SetupService<IFiscalYearService>();
        _mockRankingConfig = SetupService<Microsoft.Extensions.Options.IOptions<RTUB.Application.Configuration.RankingConfiguration>>();

        // Setup default service responses
        _mockRankingService
            .Setup(x => x.GetRankProgressBatchAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, RTUB.Application.DTOs.RankProgressInfo>());

        _mockRankingService
            .Setup(x => x.GetRankProgressBatchAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, RTUB.Application.DTOs.RankProgressInfo>());

        _mockMemberStatisticsService
            .Setup(x => x.GetRehearsalAttendanceCountsByUserAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new Dictionary<string, int>());

        _mockMemberStatisticsService
            .Setup(x => x.GetEnrollmentsByUserWithEventTypeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<RTUB.Application.DTOs.UserEnrollmentWithEventType>());

        // Setup for single DateTime parameter overloads (used when no fiscal year filter)
        _mockMemberStatisticsService
            .Setup(x => x.GetRehearsalAttendanceCountsByUserAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new Dictionary<string, int>());

        _mockMemberStatisticsService
            .Setup(x => x.GetEnrollmentsByUserWithEventTypeAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<RTUB.Application.DTOs.UserEnrollmentWithEventType>());

        _mockRankingConfig
            .Setup(x => x.Value)
            .Returns(new RTUB.Application.Configuration.RankingConfiguration());

        _mockLabelService
            .Setup(x => x.GetLabelByReferenceAsync(It.IsAny<string>()))
            .ReturnsAsync((Label?)null);

        _mockFiscalYearService
            .Setup(x => x.GetAllFiscalYearsAsync())
            .ReturnsAsync(new List<FiscalYear>());

        // Setup UserManager.Users for async queries
        var emptyUsers = new List<ApplicationUser>();
        var mockDbSet = emptyUsers.BuildMockDbSet();
        SetupAsyncQueryableForLeaderboard(mockDbSet);
        _mockUserManager.Setup(x => x.Users).Returns(mockDbSet.Object);
        _mockUserManager
            .Setup(x => x.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        SetupAuthentication("test-user", "Test User");
    }

    #region Page Rendering Tests

    [Fact]
    public async Task LeaderboardPage_RendersPageTitle()
    {
        // Arrange & Act
        var cut = RenderComponent<Leaderboard>();
        cut.WaitForState(() => cut.Markup.Contains("Tabela de Classificação") || cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Tabela de Classificação", "page should display 'Tabela de Classificação' title");
    }

    [Fact]
    public async Task LeaderboardPage_ShowsLoadingState_Initially()
    {
        // Arrange - Setup slow service to test loading state
        var tcs = new TaskCompletionSource<Dictionary<string, RTUB.Application.DTOs.RankProgressInfo>>();
        _mockRankingService
            .Setup(x => x.GetRankProgressBatchAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        // Act
        var cut = RenderComponent<Leaderboard>();

        // Assert - Check loading state before async operations complete
        cut.Markup.Should().Contain("A carregar", "page should show loading state initially");
        
        // Complete the delayed task to allow test cleanup
        tcs.SetResult(new Dictionary<string, RTUB.Application.DTOs.RankProgressInfo>());
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task LeaderboardPage_ShowsEmptyState_WhenNoMembers()
    {
        // Arrange - Already set up with empty data in constructor

        // Act
        var cut = RenderComponent<Leaderboard>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Nenhum membro encontrado", "page should show empty state when no members");
    }

    #endregion

    #region Helper Methods

    private static void SetupAsyncQueryableForLeaderboard<T>(Mock<DbSet<T>> mockDbSet) where T : class
    {
        var queryable = mockDbSet.Object.AsQueryable();
        mockDbSet.As<IAsyncEnumerable<T>>()
            .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(new TestAsyncEnumerator<T>(queryable.GetEnumerator()));
        mockDbSet.As<IQueryable<T>>()
            .Setup(m => m.Provider)
            .Returns(new TestAsyncQueryProvider<T>(queryable.Provider));
    }

    // Helper classes for async query support
    private class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        private readonly IEnumerator<T> _inner;

        public TestAsyncEnumerator(IEnumerator<T> inner) => _inner = inner;

        public T Current => _inner.Current;

        public ValueTask<bool> MoveNextAsync() => new ValueTask<bool>(_inner.MoveNext());

        public ValueTask DisposeAsync()
        {
            _inner.Dispose();
            return new ValueTask();
        }
    }

    private class TestAsyncQueryProvider<TEntity> : Microsoft.EntityFrameworkCore.Query.IAsyncQueryProvider
    {
        private readonly IQueryProvider _inner;

        internal TestAsyncQueryProvider(IQueryProvider inner) => _inner = inner;

        public IQueryable CreateQuery(System.Linq.Expressions.Expression expression)
            => new TestAsyncEnumerable<TEntity>(expression);

        public IQueryable<TElement> CreateQuery<TElement>(System.Linq.Expressions.Expression expression)
            => new TestAsyncEnumerable<TElement>(expression);

        public object? Execute(System.Linq.Expressions.Expression expression)
            => _inner.Execute(expression);

        public TResult Execute<TResult>(System.Linq.Expressions.Expression expression)
            => _inner.Execute<TResult>(expression);

        public TResult ExecuteAsync<TResult>(System.Linq.Expressions.Expression expression, CancellationToken cancellationToken = default)
        {
            var expectedResultType = typeof(TResult).GetGenericArguments()[0];
            var executionResult = typeof(IQueryProvider)
                .GetMethod(nameof(IQueryProvider.Execute), genericParameterCount: 1, types: new[] { typeof(System.Linq.Expressions.Expression) })?
                .MakeGenericMethod(expectedResultType)
                .Invoke(this, new[] { expression });

            return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))?
                .MakeGenericMethod(expectedResultType)
                .Invoke(null, new[] { executionResult })!;
        }
    }

    private class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public TestAsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable) { }
        public TestAsyncEnumerable(System.Linq.Expressions.Expression expression) : base(expression) { }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            => new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());

        IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
    }

    #endregion
}
