using FluentAssertions;
using Moq;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Tests.Extensions;

/// <summary>
/// Test entity for RepositoryExtensions tests
/// Must be public for Moq's Castle proxy generator to work
/// </summary>
public class RepositoryTestEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>
/// Unit tests for RepositoryExtensions
/// Tests entity retrieval with automatic exception handling
/// </summary>
public class RepositoryExtensionsTests
{
    [Fact]
    public async Task GetByIdOrThrowAsync_EntityExists_ReturnsEntity()
    {
        // Arrange
        var expectedEntity = new RepositoryTestEntity { Id = 1, Name = "Test" };
        var mockRepository = new Mock<IRepository<RepositoryTestEntity>>();
        mockRepository
            .Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(expectedEntity);

        // Act
        var result = await mockRepository.Object.GetByIdOrThrowAsync(1);

        // Assert
        result.Should().Be(expectedEntity);
        result.Id.Should().Be(1);
        result.Name.Should().Be("Test");
    }

    [Fact]
    public async Task GetByIdOrThrowAsync_EntityNotFound_ThrowsEntityNotFoundException()
    {
        // Arrange
        var mockRepository = new Mock<IRepository<RepositoryTestEntity>>();
        mockRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((RepositoryTestEntity?)null);

        // Act
        var act = async () => await mockRepository.Object.GetByIdOrThrowAsync(999);

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task GetByIdOrThrowAsync_EntityNotFound_ExceptionContainsCorrectEntityType()
    {
        // Arrange
        var mockRepository = new Mock<IRepository<RepositoryTestEntity>>();
        mockRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((RepositoryTestEntity?)null);

        // Act
        var act = async () => await mockRepository.Object.GetByIdOrThrowAsync(42);

        // Assert
        var exception = await act.Should().ThrowAsync<EntityNotFoundException>();
        exception.Which.EntityType.Should().Be("RepositoryTestEntity");
    }

    [Fact]
    public async Task GetByIdOrThrowAsync_EntityNotFound_ExceptionContainsCorrectId()
    {
        // Arrange
        const int entityId = 123;
        var mockRepository = new Mock<IRepository<RepositoryTestEntity>>();
        mockRepository
            .Setup(r => r.GetByIdAsync(entityId))
            .ReturnsAsync((RepositoryTestEntity?)null);

        // Act
        var act = async () => await mockRepository.Object.GetByIdOrThrowAsync(entityId);

        // Assert
        var exception = await act.Should().ThrowAsync<EntityNotFoundException>();
        exception.Which.EntityId.Should().Be(entityId);
    }
}
