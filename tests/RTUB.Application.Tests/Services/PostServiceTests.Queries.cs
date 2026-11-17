// PostService: queries.
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public partial class PostServiceTests
{
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
}
