using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MockQueryable.Moq;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Pages.Operations;
using RTUB.Web.Tests.Pages.Base;

namespace RTUB.Web.Tests.Pages.Operations;

/// <summary>
/// Component tests for Notifications.razor page
/// Tests page rendering, loading state, notification composition, and recipient selection
/// </summary>
public class NotificationsPageTests : PageTestBase
{
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<IPushNotificationService> _mockPushNotificationService;

    public NotificationsPageTests()
    {
        // Setup service mocks
        _mockUserManager = SetupUserManager();
        _mockPushNotificationService = SetupService<IPushNotificationService>();

        // Setup default service responses
        _mockPushNotificationService
            .Setup(x => x.GetSubscribedUserIdsAsync())
            .ReturnsAsync((IEnumerable<string>)new List<string>());

        // Setup UserManager.Users for async queries
        var emptyUsers = new List<ApplicationUser>();
        var mockDbSet = emptyUsers.BuildMockDbSet();
        SetupAsyncQueryable(mockDbSet);
        _mockUserManager.Setup(x => x.Users).Returns(mockDbSet.Object);

        // Setup authentication as Admin (required for this page)
        SetupAuthenticationAsAdmin("admin-user");
    }

    #region Page Rendering Tests

    [Fact]
    public async Task NotificationsPage_RendersPageTitle()
    {
        // Arrange & Act
        var cut = Render<Notifications>();
        cut.WaitForState(() => cut.Markup.Contains("Enviar Notificação Push") || cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Enviar Notificação Push", "page should display 'Enviar Notificação Push' title");
    }

    [Fact]
    public async Task NotificationsPage_ShowsLoadingState_Initially()
    {
        // Arrange - Setup slow service to test loading state
        var tcs = new TaskCompletionSource<IEnumerable<string>>();
        _mockPushNotificationService
            .Setup(x => x.GetSubscribedUserIdsAsync())
            .Returns(tcs.Task);

        // Act
        var cut = Render<Notifications>();

        // Assert - Check loading state before async operations complete
        cut.Markup.Should().Contain("A carregar destinatários", "page should show loading state initially");

        // Complete the delayed task to allow test cleanup
        tcs.SetResult((IEnumerable<string>)new List<string>());
        cut.WaitForState(() => !cut.Markup.Contains("A carregar destinatários"), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task NotificationsPage_DisplaysNotificationForm()
    {
        // Arrange & Act
        var cut = Render<Notifications>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar destinatários"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Compor Notificação", "page should display notification form");
        cut.Markup.Should().Contain("Título", "page should display title field");
        cut.Markup.Should().Contain("Mensagem", "page should display content field");
    }

    [Fact]
    public async Task NotificationsPage_DisplaysActionButtons()
    {
        // Arrange & Act
        var cut = Render<Notifications>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar destinatários"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Pré-visualizar", "page should display preview button");
        cut.Markup.Should().Contain("Enviar", "page should display send button");
        cut.Markup.Should().Contain("Limpar", "page should display clear button");
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
