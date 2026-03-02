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
/// Component tests for HallOfFame.razor page
/// Tests page rendering, loading state, and hall of fame metrics display
/// </summary>
public class HallOfFamePageTests : PageTestBase
{
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<IRoleAssignmentService> _mockRoleAssignmentService;
    private readonly Mock<IEnrollmentService> _mockEnrollmentService;
    private readonly Mock<IRehearsalService> _mockRehearsalService;
    private readonly Mock<IRehearsalAttendanceRepository> _mockRehearsalAttendanceRepository;
    private readonly Mock<IMemberInstrumentService> _mockMemberInstrumentService;

    public HallOfFamePageTests()
    {
        // Setup service mocks
        _mockUserManager = SetupUserManager();
        _mockRoleAssignmentService = SetupService<IRoleAssignmentService>();
        _mockEnrollmentService = SetupService<IEnrollmentService>();
        _mockRehearsalService = SetupService<IRehearsalService>();
        _mockRehearsalAttendanceRepository = SetupService<IRehearsalAttendanceRepository>();
        _mockMemberInstrumentService = SetupService<IMemberInstrumentService>();

        // Setup default service responses - Use MockQueryable for async support
        var emptyUsers = new List<ApplicationUser>();
        var mockDbSet = emptyUsers.BuildMockDbSet();
        SetupAsyncQueryable(mockDbSet);
        _mockUserManager.Setup(x => x.Users).Returns(mockDbSet.Object);

        // Setup RehearsalAttendanceRepository for complex queries
        var emptyAttendances = new List<RehearsalAttendance>();
        var mockAttendanceDbSet = emptyAttendances.BuildMockDbSet();
        SetupAsyncQueryable(mockAttendanceDbSet);
        _mockRehearsalAttendanceRepository
            .Setup(x => x.QueryAsync(It.IsAny<Func<IQueryable<RehearsalAttendance>, Task<It.IsAnyType>>>(), It.IsAny<CancellationToken>()))
            .Returns(new InvocationFunc(invocation =>
            {
                var queryFunc = (Delegate)invocation.Arguments[0];
                return queryFunc.DynamicInvoke(mockAttendanceDbSet.Object)!;
            }));

        _mockRoleAssignmentService
            .Setup(x => x.GetAllRoleAssignmentsAsync())
            .ReturnsAsync(new List<RoleAssignment>());

        SetupAuthentication("test-user", "Test User");
    }

    #region Page Rendering Tests

    [Fact]
    public async Task HallOfFamePage_RendersPageTitle()
    {
        // Arrange & Act
        var cut = RenderComponent<HallOfFame>();
        cut.WaitForState(() => cut.Markup.Contains("Hall of Fame") || cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Hall of Fame", "page should display 'Hall of Fame' title");
    }

    [Fact]
    public async Task HallOfFamePage_ShowsLoadingState_Initially()
    {
        // Arrange - Setup slow service to test loading state
        // Note: HallOfFame uses complex LINQ queries with anonymous types that are difficult to mock
        // We'll test the loading state by delaying the role assignments service
        var tcs = new TaskCompletionSource<IEnumerable<RoleAssignment>>();
        _mockRoleAssignmentService
            .Setup(x => x.GetAllRoleAssignmentsAsync())
            .Returns(tcs.Task);

        // Act
        var cut = RenderComponent<HallOfFame>();

        // Assert - Check loading state before async operations complete
        cut.Markup.Should().Contain("A carregar", "page should show loading state initially");

        // Complete the delayed task to allow test cleanup
        tcs.SetResult(new List<RoleAssignment>());
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task HallOfFamePage_ShowsEmptyState_WhenNoData()
    {
        // Arrange - Already set up with empty data in constructor

        // Act
        var cut = RenderComponent<HallOfFame>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Sem dados suficientes", "page should show empty state when no data");
    }

    #endregion

    #region Helper Methods

    private static ApplicationUser CreateTestUser(string id, string firstName, string lastName)
    {
        return new ApplicationUser
        {
            Id = id,
            FirstName = firstName,
            LastName = lastName,
            Email = $"{id}@test.com",
            UserName = id
        };
    }

    private static void SetupAsyncQueryable<T>(Mock<DbSet<T>> mockDbSet) where T : class
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
