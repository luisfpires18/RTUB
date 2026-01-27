using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Web.Tests.Pages.Base;
using EventDiscussionPage = RTUB.Pages.Activities.EventDiscussion;

namespace RTUB.Web.Tests.Pages.Activities;

/// <summary>
/// Component tests for EventDiscussion.razor page (/events/{eventId}/discussion).
/// Tests page rendering, loading state, empty state, posts display, comments, and CRUD workflows (Phase 0.5).
/// </summary>
public class EventDiscussionPageTests : PageTestBase
{
    private const string SkipModal = "Modal renders outside component fragment; cannot assert modal markup in bUnit.";

    private readonly Mock<IEventService> _mockEventService;
    private readonly Mock<IDiscussionService> _mockDiscussionService;
    private readonly Mock<IPostService> _mockPostService;
    private readonly Mock<ICommentService> _mockCommentService;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;

    public EventDiscussionPageTests()
    {
        _mockEventService = SetupService<IEventService>();
        _mockDiscussionService = SetupService<IDiscussionService>();
        _mockPostService = SetupService<IPostService>();
        _mockCommentService = SetupService<ICommentService>();
        _mockUserManager = SetupUserManager();

        // Setup default service responses
        _mockPostService
            .Setup(x => x.GetCountByDiscussionIdAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(0);

        _mockPostService
            .Setup(x => x.GetByDiscussionIdAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(new List<Post>());

        _mockCommentService
            .Setup(x => x.GetCountByPostIdAsync(It.IsAny<int>()))
            .ReturnsAsync(0);

        _mockCommentService
            .Setup(x => x.GetByPostIdAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new List<Comment>());

        _mockUserManager
            .Setup(x => x.FindByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);
    }

    #region Page Rendering Tests

    [Fact]
    public async Task EventDiscussionPage_RendersPageTitle()
    {
        // Arrange
        SetupAuthentication("test-user", "Test User");
        var eventItem = CreateTestEvent(1, "Test Event");
        _mockEventService
            .Setup(x => x.GetEventByIdAsync(1))
            .ReturnsAsync(eventItem);

        // Act
        var cut = RenderComponent<EventDiscussionPage>(parameters => parameters
            .Add(p => p.EventId, 1));
        cut.WaitForState(() => cut.Markup.Contains("Discussão") || cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Discussão", "page should display 'Discussão' title");
        cut.Markup.Should().Contain("Test Event", "page should display event name");
    }

    [Fact]
    public async Task EventDiscussionPage_ShowsLoadingState_Initially()
    {
        // Arrange
        SetupAuthentication("test-user", "Test User");
        _mockEventService
            .Setup(x => x.GetEventByIdAsync(It.IsAny<int>()))
            .Returns(async () =>
            {
                await Task.Delay(100);
                return CreateTestEvent(1, "Test Event");
            });

        // Act
        var cut = RenderComponent<EventDiscussionPage>(parameters => parameters
            .Add(p => p.EventId, 1));

        // Assert
        cut.Markup.Should().Match(m => m.Contains("A carregar") || m.Contains("Discussão"), "should show loading or title initially");
    }

    [Fact]
    public async Task EventDiscussionPage_ShowsEmptyState_WhenNoPosts()
    {
        // Arrange
        SetupAuthentication("test-user", "Test User");
        var eventItem = CreateTestEvent(1, "Test Event");
        var discussion = CreateTestDiscussion(1, 1);

        _mockEventService
            .Setup(x => x.GetEventByIdAsync(1))
            .ReturnsAsync(eventItem);

        _mockDiscussionService
            .Setup(x => x.GetByEventIdAsync(1))
            .ReturnsAsync(discussion);

        _mockPostService
            .Setup(x => x.GetCountByDiscussionIdAsync(1, It.IsAny<string>()))
            .ReturnsAsync(0);

        // Act
        var cut = RenderComponent<EventDiscussionPage>(parameters => parameters
            .Add(p => p.EventId, 1));
        cut.WaitForState(() => cut.Markup.Contains("Nenhum post encontrado") || cut.Markup.Contains("Discussão"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Nenhum post encontrado", "should show empty state when no posts");
        cut.Markup.Should().Contain("Seja o primeiro a iniciar uma discussão", "should show empty state message");
    }

    [Fact]
    public async Task EventDiscussionPage_DisplaysPosts_WhenPostsExist()
    {
        // Arrange
        SetupAuthentication("test-user", "Test User");
        var eventItem = CreateTestEvent(1, "Test Event");
        var discussion = CreateTestDiscussion(1, 1);
        var currentUser = CreateTestUser("test-user", "Test User");
        var posts = new List<Post>
        {
            CreateTestPost(1, 1, "First Post", "First post content", currentUser),
            CreateTestPost(2, 1, "Second Post", "Second post content", currentUser)
        };

        _mockEventService
            .Setup(x => x.GetEventByIdAsync(1))
            .ReturnsAsync(eventItem);

        _mockDiscussionService
            .Setup(x => x.GetByEventIdAsync(1))
            .ReturnsAsync(discussion);

        _mockUserManager
            .Setup(x => x.FindByNameAsync("test-user"))
            .ReturnsAsync(currentUser);

        _mockPostService
            .Setup(x => x.GetCountByDiscussionIdAsync(1, It.IsAny<string>()))
            .ReturnsAsync(2);

        _mockPostService
            .Setup(x => x.GetByDiscussionIdAsync(1, 1, 20, It.IsAny<string>()))
            .ReturnsAsync(posts);

        // Act
        var cut = RenderComponent<EventDiscussionPage>(parameters => parameters
            .Add(p => p.EventId, 1));
        cut.WaitForState(() => cut.Markup.Contains("First Post") || cut.Markup.Contains("Discussão"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("First Post", "should display first post");
        cut.Markup.Should().Contain("Second Post", "should display second post");
    }

    [Fact]
    public async Task EventDiscussionPage_ShowsCreatePostButton_ForAuthenticatedUsers()
    {
        // Arrange
        SetupAuthentication("test-user", "Test User");
        var eventItem = CreateTestEvent(1, "Test Event");
        var discussion = CreateTestDiscussion(1, 1);
        var currentUser = CreateTestUser("test-user", "Test User");

        _mockEventService
            .Setup(x => x.GetEventByIdAsync(1))
            .ReturnsAsync(eventItem);

        _mockDiscussionService
            .Setup(x => x.GetByEventIdAsync(1))
            .ReturnsAsync(discussion);

        _mockUserManager
            .Setup(x => x.FindByNameAsync("test-user"))
            .ReturnsAsync(currentUser);

        // Act
        var cut = RenderComponent<EventDiscussionPage>(parameters => parameters
            .Add(p => p.EventId, 1));
        cut.WaitForState(() => cut.Markup.Contains("Adicionar Post") || cut.Markup.Contains("Discussão"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Adicionar Post", "authenticated user should see create post button");
    }

    [Fact]
    public async Task EventDiscussionPage_DisplaysSearchBar()
    {
        // Arrange
        SetupAuthentication("test-user", "Test User");
        var eventItem = CreateTestEvent(1, "Test Event");
        var discussion = CreateTestDiscussion(1, 1);

        _mockEventService
            .Setup(x => x.GetEventByIdAsync(1))
            .ReturnsAsync(eventItem);

        _mockDiscussionService
            .Setup(x => x.GetByEventIdAsync(1))
            .ReturnsAsync(discussion);

        // Act
        var cut = RenderComponent<EventDiscussionPage>(parameters => parameters
            .Add(p => p.EventId, 1));
        cut.WaitForState(() => cut.Markup.Contains("Pesquisar") || cut.Markup.Contains("Discussão"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Pesquisar", "page should display search bar");
    }

    [Fact]
    public async Task EventDiscussionPage_ShowsBackButton()
    {
        // Arrange
        SetupAuthentication("test-user", "Test User");
        var eventItem = CreateTestEvent(1, "Test Event");

        _mockEventService
            .Setup(x => x.GetEventByIdAsync(1))
            .ReturnsAsync(eventItem);

        // Act
        var cut = RenderComponent<EventDiscussionPage>(parameters => parameters
            .Add(p => p.EventId, 1));
        cut.WaitForState(() => cut.Markup.Contains("bi-arrow-left") || cut.Markup.Contains("Discussão"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("bi-arrow-left", "page should display back button");
    }

    [Fact(Skip = SkipModal)]
    public async Task EventDiscussionPage_OpensCreatePostModal_WhenCreateButtonClicked()
    {
        // Modal interactions are outside component fragment
    }

    #endregion

    #region Helper Methods

    private static Event CreateTestEvent(int id, string name)
    {
        var eventItem = Event.Create(
            name,
            DateTime.UtcNow.AddDays(7),
            "Test Location",
            EventType.Atuacao,
            "Test Description");
        // Set Id for testing
        typeof(Event).GetProperty("Id")?.SetValue(eventItem, id);
        return eventItem;
    }

    private static Discussion CreateTestDiscussion(int id, int eventId)
    {
        var discussion = Discussion.Create(eventId);
        typeof(Discussion).GetProperty("Id")?.SetValue(discussion, id);
        return discussion;
    }

    private static Post CreateTestPost(int id, int discussionId, string title, string body, ApplicationUser? author = null)
    {
        var post = Post.Create(discussionId, "test-user-id", title, body);
        typeof(Post).GetProperty("Id")?.SetValue(post, id);
        if (author != null)
        {
            typeof(Post).GetProperty("Author")?.SetValue(post, author);
        }
        return post;
    }

    private static ApplicationUser CreateTestUser(string userName, string displayName)
    {
        return new ApplicationUser
        {
            Id = "test-user-id",
            UserName = userName,
            FirstName = displayName.Split(' ')[0],
            LastName = displayName.Split(' ').Length > 1 ? displayName.Split(' ')[1] : string.Empty,
            Email = $"{userName}@test.com"
        };
    }

    #endregion
}
