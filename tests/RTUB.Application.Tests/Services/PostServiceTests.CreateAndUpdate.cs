// PostService: create and update.
using FluentAssertions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Tests.Services;

public partial class PostServiceTests
{
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
        _context.ChangeTracker.Clear();
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
}
