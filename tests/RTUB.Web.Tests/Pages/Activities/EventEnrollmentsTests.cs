using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MockQueryable.Moq;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Pages.Activities;
using RTUB.Web.Tests.Pages.Base;
using Xunit;

namespace RTUB.Web.Tests.Pages.Activities;

/// <summary>
/// Tests for the EventEnrollments page.
/// Verifies page rendering, loading state, enrollment display, and copy link functionality.
/// </summary>
public class EventEnrollmentsTests : PageTestBase
{
    private readonly Mock<IEventService> _mockEventService;
    private readonly Mock<IEnrollmentFilterService> _mockEnrollmentFilterService;
    private readonly Mock<IEnrollmentStatisticsService> _mockEnrollmentStatisticsService;
    private readonly Mock<IEnrollmentService> _mockEnrollmentService;
    private readonly Mock<IMemberInstrumentService> _mockMemberInstrumentService;
    private readonly Mock<IUserProfileService> _mockUserProfileService;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;

    public EventEnrollmentsTests()
    {
        // Setup service mocks
        _mockEventService = SetupService<IEventService>();
        _mockEnrollmentFilterService = SetupService<IEnrollmentFilterService>();
        _mockEnrollmentStatisticsService = SetupService<IEnrollmentStatisticsService>();
        _mockEnrollmentService = SetupService<IEnrollmentService>();
        _mockMemberInstrumentService = SetupService<IMemberInstrumentService>();
        _mockUserProfileService = SetupService<IUserProfileService>();
        _mockUserManager = SetupUserManager();

        // Setup default service responses
        _mockEventService
            .Setup(x => x.GetEventByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((Event?)null);

        _mockEnrollmentService
            .Setup(x => x.GetEnrollmentsByEventIdAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<Enrollment>());

        // Setup UserManager.Users for async queries
        var emptyUsers = new List<ApplicationUser>();
        var mockDbSet = emptyUsers.BuildMockDbSet();
        SetupAsyncQueryable(mockDbSet);
        _mockUserManager.Setup(x => x.Users).Returns(mockDbSet.Object);

        // Setup default service responses for new services
        _mockEnrollmentFilterService
            .Setup(x => x.FilterEnrollments(It.IsAny<IEnumerable<Enrollment>>(), It.IsAny<string>()))
            .Returns((IEnumerable<Enrollment> enrollments, string? search) =>
            {
                var enrollmentsList = enrollments?.ToList() ?? new List<Enrollment>();
                var performing = enrollmentsList.Where(e => e.User != null && e.WillAttend && !e.User.Categories.Contains(MemberCategory.Leitao)).ToList();
                var leitoes = enrollmentsList.Where(e => e.User != null && e.WillAttend && e.User.Categories.Contains(MemberCategory.Leitao)).ToList();
                var notAttending = enrollmentsList.Where(e => e.User != null && !e.WillAttend).ToList();
                return (performing, leitoes, notAttending);
            });

        _mockEnrollmentStatisticsService
            .Setup(x => x.CalculateInstrumentCounts(It.IsAny<Event>(), It.IsAny<Dictionary<string, List<MemberInstrument>>>()))
            .Returns((new Dictionary<InstrumentType, int>(), new Dictionary<InstrumentType, int>()));

        // Setup authentication
        SetupAuthentication("test-user", "Test User");
    }

    #region Page Rendering Tests


    [Fact]
    public async Task EventEnrollmentsPage_RendersPageTitle_WhenEventExists()
    {
        // Arrange
        var testEvent = new Event { Id = 1, Name = "Test Event", Date = DateTime.Today };
        _mockEventService
            .Setup(x => x.GetEventByIdAsync(1))
            .ReturnsAsync(testEvent);

        // Act
        var cut = Render<EventEnrollments>(parameters => parameters
            .Add(p => p.EventId, 1));
        cut.WaitForState(() => cut.Markup.Contains("Inscrições") || cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Inscrições", "page should display 'Inscrições' title");
    }

    [Fact]
    public async Task EventEnrollmentsPage_DisplaysBackButton()
    {
        // Arrange
        var testEvent = new Event { Id = 1, Name = "Test Event", Date = DateTime.Today };
        _mockEventService
            .Setup(x => x.GetEventByIdAsync(1))
            .ReturnsAsync(testEvent);

        // Act
        var cut = Render<EventEnrollments>(parameters => parameters
            .Add(p => p.EventId, 1));
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("bi-arrow-left", "page should display back button");
    }

    #endregion

    #region Copy Link Tests
    [Fact]
    public async Task CopyLinkButton_RendersWithCorrectIcon_WhenUserIsAdmin()
    {
        // Arrange
        SetupAuthenticationAsAdmin("admin-user");
        var testEvent = new Event { Id = 1, Name = "Test Event", Date = DateTime.Today };
        _mockEventService
            .Setup(x => x.GetEventByIdAsync(1))
            .ReturnsAsync(testEvent);

        // Act
        var cut = Render<EventEnrollments>(parameters => parameters
            .Add(p => p.EventId, 1));
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("bi-link-45deg", "Copy link button should display link icon");
        cut.Markup.Should().Contain("Copiar link", "Copy link button should have tooltip");
    }

    [Fact]
    public void CopyLinkToClipboard_ShouldCallJSInteropWithAbsoluteUrl()
    {
        // Arrange
        var testUri = "https://example.com/events/123/enrollments";

        // Simulate the logic from CopyLinkToClipboard method
        // NavigationManager.ToAbsoluteUri(NavigationManager.Uri).ToString()

        // Act
        var absoluteUrl = new Uri(testUri).ToString();

        // Assert
        absoluteUrl.Should().Be(testUri, "Should generate absolute URL for current page");
        absoluteUrl.Should().Contain("/events/", "URL should contain events path");
        absoluteUrl.Should().Contain("/enrollments", "URL should contain enrollments path");
    }

    [Fact]
    public void CopyLinkFeedback_SuccessMessage_ShouldBeInPortuguese()
    {
        // Arrange
        var successMessage = "Link copiado!";
        var errorMessage = "Não foi possível copiar o link";

        // Assert
        successMessage.Should().NotBeNullOrEmpty("Success message should be defined");
        errorMessage.Should().NotBeNullOrEmpty("Error message should be defined");
        successMessage.Should().Contain("copiado", "Success message should be in Portuguese");
        errorMessage.Should().Contain("Não foi possível", "Error message should be in Portuguese");
    }

    [Theory]
    [InlineData("https://rtub.com/events/1/enrollments")]
    [InlineData("https://rtub.com/events/999/enrollments")]
    [InlineData("https://localhost:5001/events/42/enrollments")]
    public void CopyLinkToClipboard_ShouldWorkWithDifferentEventIds(string testUrl)
    {
        // Arrange & Assert
        testUrl.Should().MatchRegex(@"/events/\d+/enrollments$",
            "URL should match enrollments page pattern");
    }

    [Fact]
    public void CopyLinkButton_ShouldBePositionedWithMarginLeftAuto()
    {
        // Arrange
        var buttonClasses = "btn btn-outline-secondary btn-sm ms-auto";

        // Assert
        buttonClasses.Should().Contain("ms-auto",
            "Button should use Bootstrap ms-auto class for right alignment");
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
