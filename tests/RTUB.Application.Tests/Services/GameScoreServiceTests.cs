using FluentAssertions;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for GameScoreService
/// Tests leaderboard ranking logic and score submission
/// </summary>
public class GameScoreServiceTests
{
    private readonly Mock<IGameScoreRepository> _mockGameScoreRepository;
    private readonly GameScoreService _service;

    public GameScoreServiceTests()
    {
        _mockGameScoreRepository = new Mock<IGameScoreRepository>();
        _service = new GameScoreService(_mockGameScoreRepository.Object);
    }

    [Fact]
    public async Task GetLeaderboardAsync_ReturnsScoresInCorrectOrder()
    {
        // Arrange
        var user1 = CreateTestUser("user1", "Player1", "Nick1");
        var user2 = CreateTestUser("user2", "Player2", "Nick2");
        var user3 = CreateTestUser("user3", "Player3", "Nick3");

        // Repository returns scores sorted by Points DESC, MaxLevel DESC
        var scores = new List<GameScore>
        {
            CreateScore("user2", 150, 3, user2),  // Highest points - Position 1
            CreateScore("user3", 100, 7, user3),  // Same points as user1, higher level - Position 2
            CreateScore("user1", 100, 5, user1)   // Same points as user3, lower level - Position 3
        };

        _mockGameScoreRepository
            .Setup(r => r.GetTopScoresAsync("test-game", 10))
            .ReturnsAsync(scores);

        // Act
        var result = await _service.GetLeaderboardAsync("test-game", 10);

        // Assert
        result.Should().HaveCount(3);
        result[0].Position.Should().Be(1);
        result[0].Points.Should().Be(150);
        result[0].UserId.Should().Be("user2");
        result[1].Position.Should().Be(2);
        result[1].Points.Should().Be(100);
        result[1].MaxLevel.Should().Be(7);
        result[1].UserId.Should().Be("user3");
        result[2].Position.Should().Be(3);
        result[2].Points.Should().Be(100);
        result[2].MaxLevel.Should().Be(5);
        result[2].UserId.Should().Be("user1");
    }

    [Fact]
    public async Task GetLeaderboardAsync_AssignsCorrectPositions()
    {
        // Arrange
        var users = Enumerable.Range(1, 5)
            .Select(i => CreateTestUser($"user{i}", $"Player{i}", $"Nick{i}"))
            .ToList();

        var scores = users.Select((u, i) => CreateScore(u.Id, (5 - i) * 10, i + 1, u)).ToList();

        _mockGameScoreRepository
            .Setup(r => r.GetTopScoresAsync("test-game", 10))
            .ReturnsAsync(scores);

        // Act
        var result = await _service.GetLeaderboardAsync("test-game", 10);

        // Assert
        for (int i = 0; i < result.Count; i++)
        {
            result[i].Position.Should().Be(i + 1, $"position at index {i} should be {i + 1}");
        }
    }

    [Fact]
    public async Task GetLeaderboardAsync_MapsUserDataCorrectly()
    {
        // Arrange
        var user = CreateTestUser("user1", "TestPlayer", "TestNick");
        user.ImageUrl = "https://example.com/pic.jpg";
        
        var score = CreateScore("user1", 100, 5, user);

        _mockGameScoreRepository
            .Setup(r => r.GetTopScoresAsync("test-game", 10))
            .ReturnsAsync(new List<GameScore> { score });

        // Act
        var result = await _service.GetLeaderboardAsync("test-game", 10);

        // Assert
        result.Should().HaveCount(1);
        result[0].UserName.Should().Be("TestPlayer");
        result[0].UserNickname.Should().Be("TestNick");
        result[0].ProfilePictureSrc.Should().Be("https://example.com/pic.jpg");
    }

    [Fact]
    public async Task GetLeaderboardAsync_HandlesNullUser()
    {
        // Arrange
        var score = CreateScore("user1", 100, 5, null);

        _mockGameScoreRepository
            .Setup(r => r.GetTopScoresAsync("test-game", 10))
            .ReturnsAsync(new List<GameScore> { score });

        // Act
        var result = await _service.GetLeaderboardAsync("test-game", 10);

        // Assert
        result.Should().HaveCount(1);
        result[0].UserName.Should().Be("Unknown");
        result[0].UserNickname.Should().BeNull();
    }

    [Fact]
    public async Task GetLeaderboardAsync_EmptyResults_ReturnsEmptyList()
    {
        // Arrange
        _mockGameScoreRepository
            .Setup(r => r.GetTopScoresAsync("test-game", 10))
            .ReturnsAsync(new List<GameScore>());

        // Act
        var result = await _service.GetLeaderboardAsync("test-game", 10);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SubmitScoreAsync_CreatesNewScore_WhenNoExistingScore()
    {
        // Arrange
        var userId = "test-user-id";
        var gameKey = "avoid-questions";
        var points = 50;
        var maxLevel = 3;
        var timeSurvived = TimeSpan.FromMinutes(2);

        // No existing score
        _mockGameScoreRepository
            .Setup(r => r.GetUserScoreAsync(userId, gameKey))
            .ReturnsAsync((GameScore?)null);

        GameScore? capturedScore = null;
        _mockGameScoreRepository
            .Setup(r => r.AddAsync(It.IsAny<GameScore>()))
            .Callback<GameScore>(s => capturedScore = s)
            .ReturnsAsync((GameScore s) => s);
        _mockGameScoreRepository
            .Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _service.SubmitScoreAsync(userId, gameKey, points, maxLevel, timeSurvived);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(userId);
        result.GameKey.Should().Be(gameKey);
        result.Points.Should().Be(points);
        result.MaxLevel.Should().Be(maxLevel);
        result.TimeSurvived.Should().Be(timeSurvived);
        
        _mockGameScoreRepository.Verify(r => r.AddAsync(It.IsAny<GameScore>()), Times.Once);
        _mockGameScoreRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task SubmitScoreAsync_UpdatesExistingScore_WhenNewScoreIsBetter()
    {
        // Arrange
        var userId = "test-user-id";
        var gameKey = "test-game";
        var existingScore = CreateScore(userId, 50, 2, null);
        
        _mockGameScoreRepository
            .Setup(r => r.GetUserScoreAsync(userId, gameKey))
            .ReturnsAsync(existingScore);
        _mockGameScoreRepository
            .Setup(r => r.UpdateAsync(It.IsAny<GameScore>()))
            .Returns(Task.CompletedTask);
        _mockGameScoreRepository
            .Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act - submit a better score (more points)
        var result = await _service.SubmitScoreAsync(userId, gameKey, 100, 5, TimeSpan.FromMinutes(3));

        // Assert
        result.Points.Should().Be(100);
        result.MaxLevel.Should().Be(5);
        _mockGameScoreRepository.Verify(r => r.UpdateAsync(existingScore), Times.Once);
        _mockGameScoreRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
        _mockGameScoreRepository.Verify(r => r.AddAsync(It.IsAny<GameScore>()), Times.Never);
    }

    [Fact]
    public async Task SubmitScoreAsync_DoesNotUpdate_WhenNewScoreIsWorse()
    {
        // Arrange
        var userId = "test-user-id";
        var gameKey = "test-game";
        var existingScore = CreateScore(userId, 100, 5, null);
        
        _mockGameScoreRepository
            .Setup(r => r.GetUserScoreAsync(userId, gameKey))
            .ReturnsAsync(existingScore);

        // Act - submit a worse score (fewer points)
        var result = await _service.SubmitScoreAsync(userId, gameKey, 50, 2, TimeSpan.FromMinutes(1));

        // Assert - score should remain unchanged
        result.Points.Should().Be(100);
        result.MaxLevel.Should().Be(5);
        _mockGameScoreRepository.Verify(r => r.UpdateAsync(It.IsAny<GameScore>()), Times.Never);
        _mockGameScoreRepository.Verify(r => r.SaveChangesAsync(), Times.Never);
        _mockGameScoreRepository.Verify(r => r.AddAsync(It.IsAny<GameScore>()), Times.Never);
    }

    [Fact]
    public async Task SubmitScoreAsync_UpdatesWhenSamePointsButHigherLevel()
    {
        // Arrange
        var userId = "test-user-id";
        var gameKey = "test-game";
        var existingScore = CreateScore(userId, 100, 3, null);
        
        _mockGameScoreRepository
            .Setup(r => r.GetUserScoreAsync(userId, gameKey))
            .ReturnsAsync(existingScore);
        _mockGameScoreRepository
            .Setup(r => r.UpdateAsync(It.IsAny<GameScore>()))
            .Returns(Task.CompletedTask);
        _mockGameScoreRepository
            .Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act - same points but higher level
        var result = await _service.SubmitScoreAsync(userId, gameKey, 100, 7, TimeSpan.FromMinutes(2));

        // Assert
        result.Points.Should().Be(100);
        result.MaxLevel.Should().Be(7);
        _mockGameScoreRepository.Verify(r => r.UpdateAsync(existingScore), Times.Once);
        _mockGameScoreRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task SubmitScoreAsync_CallsRepositoryMethods_WhenNoExistingScore()
    {
        // Arrange
        var userId = "user1";
        var gameKey = "test-game";
        var points = 100;
        var maxLevel = 5;
        var timeSurvived = TimeSpan.FromMinutes(3);

        _mockGameScoreRepository
            .Setup(r => r.GetUserScoreAsync(userId, gameKey))
            .ReturnsAsync((GameScore?)null);
        _mockGameScoreRepository
            .Setup(r => r.AddAsync(It.IsAny<GameScore>()))
            .ReturnsAsync((GameScore s) => s);
        _mockGameScoreRepository
            .Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        await _service.SubmitScoreAsync(userId, gameKey, points, maxLevel, timeSurvived);

        // Assert
        _mockGameScoreRepository.Verify(r => r.AddAsync(It.Is<GameScore>(s => 
            s.UserId == userId && 
            s.GameKey == gameKey && 
            s.Points == points && 
            s.MaxLevel == maxLevel &&
            s.TimeSurvived == timeSurvived
        )), Times.Once);
        _mockGameScoreRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetUserBestScoreAsync_ReturnsNull_WhenNoScoreExists()
    {
        // Arrange
        _mockGameScoreRepository
            .Setup(r => r.GetUserBestScoreAsync("user-id", "test-game"))
            .ReturnsAsync((GameScore?)null);

        // Act
        var result = await _service.GetUserBestScoreAsync("user-id", "test-game");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserBestScoreAsync_ReturnsScore_WhenExists()
    {
        // Arrange
        var user = CreateTestUser("user-id", "TestPlayer", "TestNick");
        var score = CreateScore("user-id", 100, 5, user);

        _mockGameScoreRepository
            .Setup(r => r.GetUserBestScoreAsync("user-id", "test-game"))
            .ReturnsAsync(score);

        // Act
        var result = await _service.GetUserBestScoreAsync("user-id", "test-game");

        // Assert
        result.Should().NotBeNull();
        result!.Points.Should().Be(100);
        result.MaxLevel.Should().Be(5);
        result.UserId.Should().Be("user-id");
    }

    [Fact]
    public async Task GetUserBestScoreAsync_MapsUserDataCorrectly()
    {
        // Arrange
        var user = CreateTestUser("user-id", "TestPlayer", "TestNick");
        user.ImageUrl = "https://example.com/avatar.png";
        var score = CreateScore("user-id", 200, 10, user);

        _mockGameScoreRepository
            .Setup(r => r.GetUserBestScoreAsync("user-id", "test-game"))
            .ReturnsAsync(score);

        // Act
        var result = await _service.GetUserBestScoreAsync("user-id", "test-game");

        // Assert
        result.Should().NotBeNull();
        result!.UserName.Should().Be("TestPlayer");
        result.UserNickname.Should().Be("TestNick");
        result.ProfilePictureSrc.Should().Be("https://example.com/avatar.png");
        result.TimeSurvived.Should().Be(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task GetUserBestScoreAsync_HandlesNullUser()
    {
        // Arrange
        var score = CreateScore("user-id", 100, 5, null);

        _mockGameScoreRepository
            .Setup(r => r.GetUserBestScoreAsync("user-id", "test-game"))
            .ReturnsAsync(score);

        // Act
        var result = await _service.GetUserBestScoreAsync("user-id", "test-game");

        // Assert
        result.Should().NotBeNull();
        result!.UserName.Should().Be("Unknown");
        result.UserNickname.Should().BeNull();
    }

    private static ApplicationUser CreateTestUser(string id, string userName, string nickname)
    {
        return new ApplicationUser
        {
            Id = id,
            UserName = userName,
            Nickname = nickname,
            Email = $"{userName}@test.com",
            FirstName = "Test",
            LastName = "User",
            PhoneNumber = "123456789"
        };
    }

    private static GameScore CreateScore(string userId, int points, int maxLevel, ApplicationUser? user)
    {
        var score = GameScore.Create(userId, "test-game", points, maxLevel, TimeSpan.FromMinutes(1));
        if (user != null)
        {
            // Use reflection to set the User navigation property for testing
            typeof(GameScore).GetProperty("User")!.SetValue(score, user);
        }
        return score;
    }
}
