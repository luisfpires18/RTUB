using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for MentionService
/// </summary>
public class MentionServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly MentionService _mentionService;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;

    public MentionServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options, Mock.Of<Microsoft.AspNetCore.Http.IHttpContextAccessor>(), new AuditContext());
        
        // Mock UserManager
        var mockUserStore = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            mockUserStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _mentionService = new MentionService(_mockUserManager.Object);
    }

    [Fact]
    public async Task ParseAndResolveAsync_WithValidMention_ReturnsJson()
    {
        // Arrange
        var text = "Hello @john, how are you?";
        var user = new ApplicationUser
        {
            Id = "user1",
            UserName = "john",
            Nickname = "John Doe"
        };

        _mockUserManager.Setup(x => x.FindByNameAsync("john"))
            .ReturnsAsync(user);

        // Act
        var result = await _mentionService.ParseAndResolveAsync(text);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("john");
        result.Should().Contain("user1");
    }

    [Fact]
    public async Task ParseAndResolveAsync_WithNoMentions_ReturnsNull()
    {
        // Arrange
        var text = "Hello, how are you?";

        // Act
        var result = await _mentionService.ParseAndResolveAsync(text);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ParseAndResolveAsync_WithInvalidMention_ReturnsNull()
    {
        // Arrange
        var text = "Hello @invaliduser, how are you?";

        _mockUserManager.Setup(x => x.FindByNameAsync("invaliduser"))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _mentionService.ParseAndResolveAsync(text);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ParseAndResolveAsync_WithDuplicateMentions_ReturnsUniqueUsers()
    {
        // Arrange
        var text = "Hello @john and @john again!";
        var user = new ApplicationUser
        {
            Id = "user1",
            UserName = "john",
            Nickname = "John Doe"
        };

        _mockUserManager.Setup(x => x.FindByNameAsync("john"))
            .ReturnsAsync(user);

        // Act
        var result = await _mentionService.ParseAndResolveAsync(text);

        // Assert
        result.Should().NotBeNull();
        // Should only contain one instance of john in the JSON dictionary
        result.Should().Contain("john");
        result.Should().Contain("user1");
        // Count how many times "john" appears - should be twice in JSON: once as key, once referenced
        var jsonOccurrences = result!.Split("\"john\"").Length - 1;
        jsonOccurrences.Should().Be(1); // Only one key "john" in JSON
    }

    [Fact]
    public async Task GetSuggestionsAsync_WithEmptyQuery_ReturnsEmpty()
    {
        // Arrange
        var query = "";

        // Act
        var result = await _mentionService.GetSuggestionsAsync(query, 10);

        // Assert
        result.Should().BeEmpty();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
