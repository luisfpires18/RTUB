using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for GroupConversationSyncService
/// Tests the synchronization of default group conversations
/// </summary>
public class GroupConversationSyncServiceTests
{
    private readonly Mock<IMessagingService> _mockMessagingService;
    private readonly Mock<IConversationRepository> _mockConversationRepository;
    private readonly Mock<ILogger<GroupConversationSyncService>> _mockLogger;

    public GroupConversationSyncServiceTests()
    {
        _mockMessagingService = new Mock<IMessagingService>();
        _mockConversationRepository = new Mock<IConversationRepository>();
        _mockLogger = new Mock<ILogger<GroupConversationSyncService>>();

        // Default setup - groups don't exist
        _mockConversationRepository
            .Setup(r => r.GetGroupByTitleAsync(It.IsAny<string>()))
            .ReturnsAsync((Conversation?)null);

        // Default setup - messaging service creates groups
        _mockMessagingService
            .Setup(m => m.GetOrCreateSystemGroupAsync(It.IsAny<string>(), It.IsAny<List<string>>()))
            .ReturnsAsync(new ConversationDto { Id = 1 });
    }

    [Fact]
    public async Task SyncDefaultGroupsAsync_HandlesException_AndLogsError()
    {
        // Arrange
        var mockUserManager = CreateMockUserManager();
        mockUserManager.Setup(m => m.Users).Throws(new Exception("Test error"));

        var service = new GroupConversationSyncService(
            _mockMessagingService.Object,
            _mockConversationRepository.Object,
            mockUserManager.Object,
            CreateInMemoryDbContext(),
            _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => service.SyncDefaultGroupsAsync());
    }

    [Fact]
    public async Task SyncDefaultGroupsAsync_SkipsGroup_WhenNoParticipantsMatch()
    {
        // Arrange
        var mockUserManager = CreateMockUserManager();
        SetupEmptyUserManager(mockUserManager);
        
        // Mock GetUsersInRoleAsync to return empty list (not null)
        mockUserManager
            .Setup(m => m.GetUsersInRoleAsync("Owner"))
            .ReturnsAsync(new List<ApplicationUser>());

        var service = new GroupConversationSyncService(
            _mockMessagingService.Object,
            _mockConversationRepository.Object,
            mockUserManager.Object,
            CreateInMemoryDbContext(),
            _mockLogger.Object);

        // Act
        await service.SyncDefaultGroupsAsync();

        // Assert - verify role-based groups were NOT created (no users with matching roles)
        _mockMessagingService.Verify(
            m => m.GetOrCreateSystemGroupAsync("VETERANOS", It.IsAny<List<string>>()),
            Times.Never);
        _mockMessagingService.Verify(
            m => m.GetOrCreateSystemGroupAsync("TUNOSSAUROS", It.IsAny<List<string>>()),
            Times.Never);
    }

    private static Mock<UserManager<ApplicationUser>> CreateMockUserManager()
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static void SetupEmptyUserManager(Mock<UserManager<ApplicationUser>> mockUserManager)
    {
        var users = new List<ApplicationUser>().AsQueryable();
        var mockDbSet = new Mock<DbSet<ApplicationUser>>();

        mockDbSet.As<IQueryable<ApplicationUser>>()
            .Setup(m => m.Provider)
            .Returns(new TestAsyncQueryProvider<ApplicationUser>(users.Provider));
        mockDbSet.As<IQueryable<ApplicationUser>>()
            .Setup(m => m.Expression)
            .Returns(users.Expression);
        mockDbSet.As<IQueryable<ApplicationUser>>()
            .Setup(m => m.ElementType)
            .Returns(users.ElementType);
        mockDbSet.As<IQueryable<ApplicationUser>>()
            .Setup(m => m.GetEnumerator())
            .Returns(() => users.GetEnumerator());
        mockDbSet.As<IAsyncEnumerable<ApplicationUser>>()
            .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(new TestAsyncEnumerator<ApplicationUser>(new List<ApplicationUser>().GetEnumerator()));

        mockUserManager.Setup(m => m.Users).Returns(mockDbSet.Object);
    }

    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var httpContextAccessor = new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        var auditContext = new AuditContext();
        return new ApplicationDbContext(options, httpContextAccessor.Object, auditContext, new RTUB.Application.Services.AuditLogAppender());
    }

    // Helper classes for async query testing
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
}
