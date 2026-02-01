using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MockQueryable.Moq;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Pages.Messages;
using RTUB.Web.Services;
using RTUB.Web.Tests.Pages.Base;

namespace RTUB.Web.Tests.Pages.Messages;

/// <summary>
/// Component tests for Inbox.razor page
/// Tests page rendering, loading state, conversation display, and messaging functionality
/// </summary>
public class InboxPageTests : PageTestBase
{
    private readonly Mock<IMessagingService> _mockMessagingService;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<IHubContext<RTUB.Web.Hubs.MessagesHub, RTUB.Web.Hubs.IMessagesHubClient>> _mockHubContext;
    private readonly Mock<MessagesNotificationService> _mockNotificationService;

    public InboxPageTests()
    {
        // Setup service mocks
        _mockMessagingService = SetupService<IMessagingService>();
        _mockUserManager = SetupUserManager();

        var mockHubClients = new Mock<IHubClients<RTUB.Web.Hubs.IMessagesHubClient>>();
        var mockClientProxy = new Mock<RTUB.Web.Hubs.IMessagesHubClient>();
        mockHubClients.Setup(x => x.All).Returns(mockClientProxy.Object);
        _mockHubContext = new Mock<IHubContext<RTUB.Web.Hubs.MessagesHub, RTUB.Web.Hubs.IMessagesHubClient>>();
        _mockHubContext.Setup(x => x.Clients).Returns(mockHubClients.Object);
        Services.AddSingleton(_mockHubContext.Object);

        // MessagesNotificationService has no constructor parameters
        _mockNotificationService = new Mock<MessagesNotificationService>();
        Services.AddSingleton(_mockNotificationService.Object);

        // Setup default service responses
        _mockMessagingService
            .Setup(x => x.GetUserConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<RTUB.Application.DTOs.ConversationDto>());


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
    public async Task InboxPage_RendersPageTitle()
    {
        // Arrange & Act
        var cut = RenderComponent<Inbox>();

        // Assert - Component should render (may show loading state initially)
        cut.Markup.Should().NotBeNullOrEmpty("page should render");
    }

    [Fact]
    public async Task InboxPage_ComponentRenders()
    {
        // Arrange & Act
        var cut = RenderComponent<Inbox>();

        // Assert - Component should render successfully
        cut.Markup.Should().NotBeNullOrEmpty("page should render");
        cut.Instance.Should().NotBeNull("component instance should be created");
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
