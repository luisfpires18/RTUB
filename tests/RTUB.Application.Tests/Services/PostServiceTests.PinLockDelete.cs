// PostService: pin lock delete.
using FluentAssertions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public partial class PostServiceTests
{
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
        _context.ChangeTracker.Clear();
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
        _context.ChangeTracker.Clear();
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
        _context.ChangeTracker.Clear();
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
        _context.ChangeTracker.Clear();
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
        _context.ChangeTracker.Clear();
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
        _context.ChangeTracker.Clear();
        var updated = await _context.Posts.FindAsync(post.Id);
        updated!.LastActivityAt.Should().BeAfter(originalTime);
    }
}
