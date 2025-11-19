using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using RTUB.Application.Data;
using RTUB.Application.Tests.Fixtures;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for PostService
/// Tests business logic and service layer operations for discussion posts
/// </summary>
public class PostServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly PostService _service;

    public PostServiceTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();
        
        _fixture = fixture;
        _context = _fixture.CreateContext();
        _service = new PostService(_context);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingPost_ReturnsPostWithIncludes()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user1", UserName = "testuser", Email = "test@example.com", FirstName = "Test", LastName = "User", Nickname = "Test", PhoneNumber = "123456789" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var eventEntity = Event.Create("Test Event", DateTime.Now, "Location", EventType.Atuacao);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var discussion = Discussion.Create(eventEntity.Id);
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        var post = Post.Create(discussion.Id, user.Id, "Test Post", "Body content");
        _context.Posts.Add(post);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetByIdAsync(post.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(post.Id);
        result.Title.Should().Be("Test Post");
        result.Body.Should().Be("Body content");
        result.Author.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingPost_ReturnsNull()
    {
        // Act
        var result = await _service.GetByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByDiscussionIdAsync_ReturnsPostsPinnedFirstThenByLastActivity()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user1", UserName = "testuser", Email = "test@example.com", FirstName = "Test", LastName = "User", Nickname = "Test", PhoneNumber = "123456789" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var eventEntity = Event.Create("Test Event", DateTime.Now, "Location", EventType.Atuacao);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var discussion = Discussion.Create(eventEntity.Id);
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        var post1 = Post.Create(discussion.Id, user.Id, "Post 1", "Body 1");
        var post2 = Post.Create(discussion.Id, user.Id, "Post 2", "Body 2");
        post2.Pin();
        var post3 = Post.Create(discussion.Id, user.Id, "Post 3", "Body 3");
        
        _context.Posts.AddRange(post1, post2, post3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetByDiscussionIdAsync(discussion.Id);

        // Assert
        result.Should().HaveCount(3);
        var posts = result.ToList();
        posts[0].IsPinned.Should().BeTrue(); // Post 2 should be first
        posts[0].Title.Should().Be("Post 2");
    }

    [Fact]
    public async Task GetByDiscussionIdAsync_WithSearchTerm_FiltersResults()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user1", UserName = "testuser", Email = "test@example.com", FirstName = "Test", LastName = "User", Nickname = "Test", PhoneNumber = "123456789" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var eventEntity = Event.Create("Test Event", DateTime.Now, "Location", EventType.Atuacao);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var discussion = Discussion.Create(eventEntity.Id);
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        var post1 = Post.Create(discussion.Id, user.Id, "Important Post", "Body 1");
        var post2 = Post.Create(discussion.Id, user.Id, "Regular Post", "Body 2");
        var post3 = Post.Create(discussion.Id, user.Id, "Another Post", "Important content");
        
        _context.Posts.AddRange(post1, post2, post3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetByDiscussionIdAsync(discussion.Id, searchTerm: "Important");

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(p => p.Title.Contains("Important"));
        result.Should().Contain(p => p.Body.Contains("Important"));
    }

    [Fact]
    public async Task GetByDiscussionIdAsync_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user1", UserName = "testuser", Email = "test@example.com", FirstName = "Test", LastName = "User", Nickname = "Test", PhoneNumber = "123456789" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var eventEntity = Event.Create("Test Event", DateTime.Now, "Location", EventType.Atuacao);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var discussion = Discussion.Create(eventEntity.Id);
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        for (int i = 1; i <= 25; i++)
        {
            var post = Post.Create(discussion.Id, user.Id, $"Post {i}", $"Body {i}");
            _context.Posts.Add(post);
        }
        await _context.SaveChangesAsync();

        // Act
        var page1 = await _service.GetByDiscussionIdAsync(discussion.Id, page: 1, pageSize: 10);
        var page2 = await _service.GetByDiscussionIdAsync(discussion.Id, page: 2, pageSize: 10);

        // Assert
        page1.Should().HaveCount(10);
        page2.Should().HaveCount(10);
    }

    [Fact]
    public async Task GetByDiscussionIdAsync_ExcludesDeletedPosts()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user1", UserName = "testuser", Email = "test@example.com", FirstName = "Test", LastName = "User", Nickname = "Test", PhoneNumber = "123456789" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var eventEntity = Event.Create("Test Event", DateTime.Now, "Location", EventType.Atuacao);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var discussion = Discussion.Create(eventEntity.Id);
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        var post1 = Post.Create(discussion.Id, user.Id, "Post 1", "Body 1");
        var post2 = Post.Create(discussion.Id, user.Id, "Post 2", "Body 2");
        post2.SoftDelete();
        
        _context.Posts.AddRange(post1, post2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetByDiscussionIdAsync(discussion.Id);

        // Assert
        result.Should().HaveCount(1);
        result.First().Title.Should().Be("Post 1");
    }

    [Fact]
    public async Task GetCountByDiscussionIdAsync_ReturnsCorrectCount()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user1", UserName = "testuser", Email = "test@example.com", FirstName = "Test", LastName = "User", Nickname = "Test", PhoneNumber = "123456789" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var eventEntity = Event.Create("Test Event", DateTime.Now, "Location", EventType.Atuacao);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var discussion = Discussion.Create(eventEntity.Id);
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        var post1 = Post.Create(discussion.Id, user.Id, "Post 1", "Body 1");
        var post2 = Post.Create(discussion.Id, user.Id, "Post 2", "Body 2");
        
        _context.Posts.AddRange(post1, post2);
        await _context.SaveChangesAsync();

        // Act
        var count = await _service.GetCountByDiscussionIdAsync(discussion.Id);

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public async Task CreateAsync_WithValidData_CreatesPost()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user1", UserName = "testuser", Email = "test@example.com", FirstName = "Test", LastName = "User", Nickname = "Test", PhoneNumber = "123456789" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var eventEntity = Event.Create("Test Event", DateTime.Now, "Location", EventType.Atuacao);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var discussion = Discussion.Create(eventEntity.Id);
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CreateAsync(discussion.Id, user.Id, "New Post", "Body content");

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("New Post");
        result.Body.Should().Be("Body content");
        result.DiscussionId.Should().Be(discussion.Id);
        result.AuthorId.Should().Be(user.Id);
    }

    [Fact]
    public async Task CreateAsync_WithMentions_SetsMentions()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user1", UserName = "testuser", Email = "test@example.com", FirstName = "Test", LastName = "User", Nickname = "Test", PhoneNumber = "123456789" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var eventEntity = Event.Create("Test Event", DateTime.Now, "Location", EventType.Atuacao);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var discussion = Discussion.Create(eventEntity.Id);
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        var mentionsJson = "[\"user2\",\"user3\"]";

        // Act
        var result = await _service.CreateAsync(discussion.Id, user.Id, "New Post", "Body", mentionsJson);

        // Assert
        result.MentionsJson.Should().Be(mentionsJson);
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_UpdatesPost()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user1", UserName = "testuser", Email = "test@example.com", FirstName = "Test", LastName = "User", Nickname = "Test", PhoneNumber = "123456789" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var eventEntity = Event.Create("Test Event", DateTime.Now, "Location", EventType.Atuacao);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var discussion = Discussion.Create(eventEntity.Id);
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        var post = Post.Create(discussion.Id, user.Id, "Original Title", "Original Body");
        _context.Posts.Add(post);
        await _context.SaveChangesAsync();

        // Act
        await _service.UpdateAsync(post.Id, "Updated Title", "Updated Body");

        // Assert
        var updated = await _context.Posts.FindAsync(post.Id);
        updated!.Title.Should().Be("Updated Title");
        updated.Body.Should().Be("Updated Body");
    }

    [Fact]
    public async Task UpdateAsync_NonExistingPost_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _service.UpdateAsync(999, "Title", "Body");
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task PinAsync_UnpinnedPost_PinsPost()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user1", UserName = "testuser", Email = "test@example.com", FirstName = "Test", LastName = "User", Nickname = "Test", PhoneNumber = "123456789" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var eventEntity = Event.Create("Test Event", DateTime.Now, "Location", EventType.Atuacao);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var discussion = Discussion.Create(eventEntity.Id);
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        var post = Post.Create(discussion.Id, user.Id, "Test Post", "Body");
        _context.Posts.Add(post);
        await _context.SaveChangesAsync();

        // Act
        await _service.PinAsync(post.Id);

        // Assert
        var updated = await _context.Posts.FindAsync(post.Id);
        updated!.IsPinned.Should().BeTrue();
    }

    [Fact]
    public async Task UnpinAsync_PinnedPost_UnpinsPost()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user1", UserName = "testuser", Email = "test@example.com", FirstName = "Test", LastName = "User", Nickname = "Test", PhoneNumber = "123456789" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var eventEntity = Event.Create("Test Event", DateTime.Now, "Location", EventType.Atuacao);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var discussion = Discussion.Create(eventEntity.Id);
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        var post = Post.Create(discussion.Id, user.Id, "Test Post", "Body");
        post.Pin();
        _context.Posts.Add(post);
        await _context.SaveChangesAsync();

        // Act
        await _service.UnpinAsync(post.Id);

        // Assert
        var updated = await _context.Posts.FindAsync(post.Id);
        updated!.IsPinned.Should().BeFalse();
    }

    [Fact]
    public async Task LockAsync_UnlockedPost_LocksPost()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user1", UserName = "testuser", Email = "test@example.com", FirstName = "Test", LastName = "User", Nickname = "Test", PhoneNumber = "123456789" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var eventEntity = Event.Create("Test Event", DateTime.Now, "Location", EventType.Atuacao);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var discussion = Discussion.Create(eventEntity.Id);
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        var post = Post.Create(discussion.Id, user.Id, "Test Post", "Body");
        _context.Posts.Add(post);
        await _context.SaveChangesAsync();

        // Act
        await _service.LockAsync(post.Id);

        // Assert
        var updated = await _context.Posts.FindAsync(post.Id);
        updated!.IsLocked.Should().BeTrue();
    }

    [Fact]
    public async Task UnlockAsync_LockedPost_UnlocksPost()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user1", UserName = "testuser", Email = "test@example.com", FirstName = "Test", LastName = "User", Nickname = "Test", PhoneNumber = "123456789" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var eventEntity = Event.Create("Test Event", DateTime.Now, "Location", EventType.Atuacao);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var discussion = Discussion.Create(eventEntity.Id);
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        var post = Post.Create(discussion.Id, user.Id, "Test Post", "Body");
        post.Lock();
        _context.Posts.Add(post);
        await _context.SaveChangesAsync();

        // Act
        await _service.UnlockAsync(post.Id);

        // Assert
        var updated = await _context.Posts.FindAsync(post.Id);
        updated!.IsLocked.Should().BeFalse();
    }

    [Fact]
    public async Task SoftDeleteAsync_ExistingPost_SoftDeletesPost()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user1", UserName = "testuser", Email = "test@example.com", FirstName = "Test", LastName = "User", Nickname = "Test", PhoneNumber = "123456789" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var eventEntity = Event.Create("Test Event", DateTime.Now, "Location", EventType.Atuacao);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var discussion = Discussion.Create(eventEntity.Id);
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        var post = Post.Create(discussion.Id, user.Id, "Test Post", "Body");
        _context.Posts.Add(post);
        await _context.SaveChangesAsync();

        // Act
        await _service.SoftDeleteAsync(post.Id);

        // Assert
        var updated = await _context.Posts.FindAsync(post.Id);
        updated!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateLastActivityAsync_ExistingPost_UpdatesLastActivityTime()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user1", UserName = "testuser", Email = "test@example.com", FirstName = "Test", LastName = "User", Nickname = "Test", PhoneNumber = "123456789" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var eventEntity = Event.Create("Test Event", DateTime.Now, "Location", EventType.Atuacao);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var discussion = Discussion.Create(eventEntity.Id);
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        var post = Post.Create(discussion.Id, user.Id, "Test Post", "Body");
        _context.Posts.Add(post);
        await _context.SaveChangesAsync();

        var originalTime = post.LastActivityAt;
        await Task.Delay(10); // Small delay to ensure time difference

        // Act
        await _service.UpdateLastActivityAsync(post.Id);

        // Assert
        var updated = await _context.Posts.FindAsync(post.Id);
        updated!.LastActivityAt.Should().BeAfter(originalTime);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
