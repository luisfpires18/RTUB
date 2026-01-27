using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MockQueryable.Moq;
using Moq;
using RTUB.Core.Entities;
using RTUB.Pages.Members;
using RTUB.Web.Tests.Pages.Base;

namespace RTUB.Web.Tests.Pages.Members;

/// <summary>
/// Component tests for Hierarchy.razor page
/// Tests page rendering, loading state, and member hierarchy display
/// </summary>
public class HierarchyPageTests : PageTestBase
{
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;

    public HierarchyPageTests()
    {
        // Setup service mocks
        _mockUserManager = SetupUserManager();

        // Setup UserManager.Users for async queries
        var emptyUsers = new List<ApplicationUser>();
        var mockDbSet = emptyUsers.BuildMockDbSet();
        SetupAsyncQueryable(mockDbSet);
        _mockUserManager.Setup(x => x.Users).Returns(mockDbSet.Object);

        // Setup authentication
        SetupAuthentication("test-user", "Test User");
    }

    #region Page Rendering Tests

    [Fact]
    public async Task HierarchyPage_RendersPageTitle()
    {
        // Arrange & Act
        var cut = RenderComponent<Hierarchy>();
        cut.WaitForState(() => cut.Markup.Contains("Hierarquia da Tuna") || cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Hierarquia da Tuna", "page should display 'Hierarquia da Tuna' title");
    }

    [Fact]
    public async Task HierarchyPage_ShowsEmptyState_WhenNoMembers()
    {
        // Arrange - Already set up with empty data in constructor

        // Act
        var cut = RenderComponent<Hierarchy>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Nenhum membro encontrado", "page should show empty state when no members");
    }

    [Fact]
    public async Task HierarchyPage_DisplaysFamilyTreeDescription()
    {
        // Arrange & Act
        var cut = RenderComponent<Hierarchy>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Árvore genealógica", "page should display family tree description");
    }

    #endregion

    #region Helper Methods

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
