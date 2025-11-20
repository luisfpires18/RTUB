using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using RTUB.Application.Data;
using RTUB.Application.Tests.Fixtures;
using RTUB.Application.Services;
using RTUB.Application.Repositories;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for CommentService
/// Tests business logic and service layer operations
/// </summary>
public class CommentServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly Mock<IPostService> _postServiceMock;
    private readonly CommentService _service;

    public CommentServiceTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();
        
        _fixture = fixture;
        _context = _fixture.CreateContext();
        _postServiceMock = new Mock<IPostService>();
        _service = new CommentService(new CommentRepository(_context), _postServiceMock.Object);
    }

    [Fact]
    public async Task CreateAsync_WithValidData_CreatesComment()
    {
        // Arrange
        var user = new ApplicationUser { Id = "author1", UserName = "testuser", Email = "test@test.com", FirstName = "Test", LastName = "User", Nickname = "Test", PhoneNumber = "123456789" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var discussion = Discussion.Create(1);
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        var post = Post.Create(discussion.Id, "author1", "Test Title", "Test Body");
        _context.Posts.Add(post);
        await _context.SaveChangesAsync();

        var body = "Test comment body";
        var authorId = "author1";

        // Act
        var result = await _service.CreateAsync(post.Id, authorId, body);

        // Assert
        result.Should().NotBeNull();
        result.Body.Should().Be(body);
        result.AuthorId.Should().Be(authorId);
        result.PostId.Should().Be(post.Id);
        _postServiceMock.Verify(p => p.UpdateLastActivityAsync(post.Id), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingComment_ReturnsComment()
    {
        // Arrange
        var user = new ApplicationUser { Id = "author1", UserName = "testuser", Email = "test@test.com", FirstName = "Test", LastName = "User", Nickname = "Test", PhoneNumber = "123456789" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var discussion = Discussion.Create(1);
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        var post = Post.Create(discussion.Id, "author1", "Test Title", "Test Body");
        _context.Posts.Add(post);
        await _context.SaveChangesAsync();

        var comment = Comment.Create(post.Id, "author1", "Test comment");
        _context.Comments.Add(comment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetByIdAsync(comment.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(comment.Id);
        result.Body.Should().Be("Test comment");
        result.AuthorId.Should().Be("author1");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingComment_ReturnsNull()
    {
        // Act
        var result = await _service.GetByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByPostIdAsync_ReturnsCommentsForPost()
    {
        // Arrange
        var user = new ApplicationUser { Id = "author1", UserName = "testuser", Email = "test@test.com", FirstName = "Test", LastName = "User", Nickname = "Test", PhoneNumber = "123456789" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var discussion = Discussion.Create(1);
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        var post = Post.Create(discussion.Id, "author1", "Test Title", "Test Body");
        _context.Posts.Add(post);
        await _context.SaveChangesAsync();

        var comment1 = Comment.Create(post.Id, "author1", "Comment 1");
        var comment2 = Comment.Create(post.Id, "author1", "Comment 2");
        _context.Comments.AddRange(comment1, comment2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetByPostIdAsync(post.Id);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(c => c.PostId == post.Id);
    }

    [Fact]
    public async Task GetByPostIdAsync_ExcludesDeletedComments()
    {
        // Arrange
        var user = new ApplicationUser { Id = "author1", UserName = "testuser", Email = "test@test.com", FirstName = "Test", LastName = "User", Nickname = "Test", PhoneNumber = "123456789" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var discussion = Discussion.Create(1);
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        var post = Post.Create(discussion.Id, "author1", "Test Title", "Test Body");
        _context.Posts.Add(post);
        await _context.SaveChangesAsync();

        var comment1 = Comment.Create(post.Id, "author1", "Comment 1");
        var comment2 = Comment.Create(post.Id, "author1", "Comment 2");
        comment2.SoftDelete();
        _context.Comments.AddRange(comment1, comment2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetByPostIdAsync(post.Id);

        // Assert
        result.Should().HaveCount(1);
        result.Should().NotContain(c => c.IsDeleted);
    }

    [Fact]
    public async Task GetCountByPostIdAsync_ReturnsCorrectCount()
    {
        // Arrange
        var discussion = Discussion.Create(1);
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        var post = Post.Create(discussion.Id, "author1", "Test Title", "Test Body");
        _context.Posts.Add(post);
        await _context.SaveChangesAsync();

        var comment1 = Comment.Create(post.Id, "author1", "Comment 1");
        var comment2 = Comment.Create(post.Id, "author1", "Comment 2");
        _context.Comments.AddRange(comment1, comment2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetCountByPostIdAsync(post.Id);

        // Assert
        result.Should().Be(2);
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_UpdatesComment()
    {
        // Arrange
        var discussion = Discussion.Create(1);
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        var post = Post.Create(discussion.Id, "author1", "Test Title", "Test Body");
        _context.Posts.Add(post);
        await _context.SaveChangesAsync();

        var comment = Comment.Create(post.Id, "author1", "Original body");
        _context.Comments.Add(comment);
        await _context.SaveChangesAsync();

        // Act
        await _service.UpdateAsync(comment.Id, "Updated body");

        // Assert
        var updated = await _context.Comments.FindAsync(comment.Id);
        updated!.Body.Should().Be("Updated body");
        updated.IsEdited.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_NonExistingComment_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _service.UpdateAsync(999, "Updated body");
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task SoftDeleteAsync_ExistingComment_MarksAsDeleted()
    {
        // Arrange
        var discussion = Discussion.Create(1);
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        var post = Post.Create(discussion.Id, "author1", "Test Title", "Test Body");
        _context.Posts.Add(post);
        await _context.SaveChangesAsync();

        var comment = Comment.Create(post.Id, "author1", "Test comment");
        _context.Comments.Add(comment);
        await _context.SaveChangesAsync();

        // Act
        await _service.SoftDeleteAsync(comment.Id);

        // Assert
        var deleted = await _context.Comments.FindAsync(comment.Id);
        deleted!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task SoftDeleteAsync_NonExistingComment_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _service.SoftDeleteAsync(999);
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("*not found*");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
