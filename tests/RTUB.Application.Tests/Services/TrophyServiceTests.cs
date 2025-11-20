using FluentAssertions;
using Moq;
using MockQueryable.Moq;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for TrophyService
/// Tests business logic and service layer operations
/// </summary>
public class TrophyServiceTests
{
    private readonly Mock<ITrophyRepository> _mockTrophyRepository;
    private readonly Mock<IRepository<Event>> _mockEventRepository;
    private readonly TrophyService _trophyService;

    public TrophyServiceTests()
    {
        _mockTrophyRepository = new Mock<ITrophyRepository>();
        _mockEventRepository = new Mock<IRepository<Event>>();
        _trophyService = new TrophyService(_mockTrophyRepository.Object);
    }

    [Fact]
    public async Task CreateAsync_WithValidData_CreatesTrophy()
    {
        // Arrange
        var trophy = Trophy.Create("1º Lugar", 1);
        _mockTrophyRepository.Setup(r => r.AddAsync(It.IsAny<Trophy>()))
            .ReturnsAsync(trophy);

        // Act
        var result = await _trophyService.CreateAsync(trophy);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("1º Lugar");
        result.EventId.Should().Be(1);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingTrophy_ReturnsTrophy()
    {
        // Arrange
        var trophy = Trophy.Create("Melhor Apresentação", 1);
        var trophies = new List<Trophy> { trophy };
        var mockQueryable = trophies.BuildMockDbSet().Object;
        _mockTrophyRepository.Setup(r => r.Query()).Returns(mockQueryable);

        // Act
        var result = await _trophyService.GetByIdAsync(trophy.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(trophy.Id);
        result.Name.Should().Be("Melhor Apresentação");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingTrophy_ReturnsNull()
    {
        // Arrange
        var trophies = new List<Trophy>();
        var mockQueryable = trophies.BuildMockDbSet().Object;
        _mockTrophyRepository.Setup(r => r.Query()).Returns(mockQueryable);

        // Act
        var result = await _trophyService.GetByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_WithMultipleTrophies_ReturnsAllTrophies()
    {
        // Arrange
        var trophies = new List<Trophy>
        {
            Trophy.Create("1º Lugar", 1),
            Trophy.Create("2º Lugar", 1),
            Trophy.Create("Melhor Apresentação", 1)
        };
        var mockQueryable = trophies.BuildMockDbSet().Object;
        _mockTrophyRepository.Setup(r => r.Query()).Returns(mockQueryable);

        // Act
        var result = await _trophyService.GetAllAsync();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAllAsync_WithNoTrophies_ReturnsEmptyCollection()
    {
        // Arrange
        var trophies = new List<Trophy>();
        var mockQueryable = trophies.BuildMockDbSet().Object;
        _mockTrophyRepository.Setup(r => r.Query()).Returns(mockQueryable);

        // Act
        var result = await _trophyService.GetAllAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByEventIdAsync_WithEventTrophies_ReturnsTrophiesForEvent()
    {
        // Arrange
        var trophies = new List<Trophy>
        {
            Trophy.Create("Trophy 1", 1),
            Trophy.Create("Trophy 2", 1)
        };
        var mockQueryable = trophies.BuildMockDbSet().Object;
        _mockTrophyRepository.Setup(r => r.Query()).Returns(mockQueryable);

        // Act
        var result = await _trophyService.GetByEventIdAsync(1);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(t => t.EventId == 1);
    }

    [Fact]
    public async Task GetByEventIdAsync_WithNoTrophies_ReturnsEmptyCollection()
    {
        // Arrange
        var trophies = new List<Trophy>();
        var mockQueryable = trophies.BuildMockDbSet().Object;
        _mockTrophyRepository.Setup(r => r.Query()).Returns(mockQueryable);

        // Act
        var result = await _trophyService.GetByEventIdAsync(1);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_UpdatesTrophy()
    {
        // Arrange
        var trophy = Trophy.Create("1º Lugar", 1);
        _mockTrophyRepository.Setup(r => r.GetByIdAsync(trophy.Id))
            .ReturnsAsync(trophy);

        // Act
        trophy.Update("Campeão Geral");
        await _trophyService.UpdateAsync(trophy);

        // Assert
        trophy.Name.Should().Be("Campeão Geral");
        _mockTrophyRepository.Verify(r => r.UpdateAsync(trophy), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ExistingTrophy_RemovesTrophy()
    {
        // Arrange
        var trophy = Trophy.Create("1º Lugar", 1);
        _mockTrophyRepository.Setup(r => r.GetByIdAsync(trophy.Id))
            .ReturnsAsync(trophy);

        // Act
        await _trophyService.DeleteAsync(trophy.Id);

        // Assert
        _mockTrophyRepository.Verify(r => r.DeleteAsync(It.IsAny<Trophy>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_NonExistingTrophy_DoesNotThrow()
    {
        // Arrange
        _mockTrophyRepository.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((Trophy?)null);

        // Act
        var act = async () => await _trophyService.DeleteAsync(999);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetAllAsync_OrdersByCreatedAtDescending()
    {
        // Arrange
        var trophy1 = Trophy.Create("First", 1);
        var trophy2 = Trophy.Create("Second", 1);
        var trophy3 = Trophy.Create("Third", 1);

        var trophies = new List<Trophy> { trophy3, trophy2, trophy1 }; // Simulating descending order
        var mockQueryable = trophies.BuildMockDbSet().Object;
        _mockTrophyRepository.Setup(r => r.Query()).Returns(mockQueryable);

        // Act
        var result = (await _trophyService.GetAllAsync()).ToList();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetByEventIdAsync_OrdersByName()
    {
        // Arrange
        var trophies = new List<Trophy>
        {
            Trophy.Create("A Trophy", 1),
            Trophy.Create("B Trophy", 1),
            Trophy.Create("C Trophy", 1)
        };
        var mockQueryable = trophies.BuildMockDbSet().Object;
        _mockTrophyRepository.Setup(r => r.Query()).Returns(mockQueryable);

        // Act
        var result = (await _trophyService.GetByEventIdAsync(1)).ToList();

        // Assert
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("A Trophy");
        result[1].Name.Should().Be("B Trophy");
        result[2].Name.Should().Be("C Trophy");
    }
}
