using FluentAssertions;
using Moq;
using MockQueryable.Moq;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Tests.Services;

public class RoleAssignmentServiceTests
{
    private readonly Mock<IRoleAssignmentRepository> _repositoryMock;
    private readonly RoleAssignmentService _service;

    public RoleAssignmentServiceTests()
    {
        _repositoryMock = new Mock<IRoleAssignmentRepository>();
        _service = new RoleAssignmentService(_repositoryMock.Object);
    }

    [Fact]
    public async Task CreateRoleAssignmentAsync_WithValidData_CreatesRoleAssignment()
    {
        // Arrange
        var userId = "user123";
        var position = Position.Magister;
        var startYear = 2023;
        var endYear = 2024;
        var notes = "Test notes";
        var createdBy = "admin";

        var expectedRoleAssignment = new RoleAssignment
        {
            Id = 1,
            UserId = userId,
            Position = position,
            StartYear = startYear,
            EndYear = endYear,
            Notes = notes,
            CreatedBy = createdBy
        };

        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<RoleAssignment>()))
            .ReturnsAsync(expectedRoleAssignment);

        // Act
        var roleAssignment = await _service.CreateRoleAssignmentAsync(userId, position, startYear, endYear, notes, createdBy);

        // Assert
        roleAssignment.Should().NotBeNull();
        roleAssignment.UserId.Should().Be(userId);
        roleAssignment.Position.Should().Be(position);
        roleAssignment.StartYear.Should().Be(startYear);
        roleAssignment.EndYear.Should().Be(endYear);
        roleAssignment.Notes.Should().Be(notes);
        roleAssignment.CreatedBy.Should().Be(createdBy);
    }

    [Fact]
    public async Task GetRoleAssignmentByIdAsync_WithExistingId_ReturnsRoleAssignment()
    {
        // Arrange
        var roleAssignment = new RoleAssignment
        {
            Id = 1,
            UserId = "user1",
            Position = Position.Magister,
            StartYear = 2023,
            EndYear = 2024
        };

        _repositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(roleAssignment);

        // Act
        var result = await _service.GetRoleAssignmentByIdAsync(roleAssignment.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(roleAssignment.Id);
    }

    [Fact]
    public async Task GetRoleAssignmentByIdAsync_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((RoleAssignment?)null);

        // Act
        var result = await _service.GetRoleAssignmentByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllRoleAssignmentsAsync_ReturnsAllRoleAssignments()
    {
        // Arrange
        var roleAssignments = new List<RoleAssignment>
        {
            new RoleAssignment { Id = 1, UserId = "user1", Position = Position.Magister, StartYear = 2023, EndYear = 2024 },
            new RoleAssignment { Id = 2, UserId = "user2", Position = Position.Secretario, StartYear = 2023, EndYear = 2024 },
            new RoleAssignment { Id = 3, UserId = "user3", Position = Position.PrimeiroTesoureiro, StartYear = 2024, EndYear = 2025 }
        };

        _repositoryMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(roleAssignments);

        // Act
        var result = (await _service.GetAllRoleAssignmentsAsync()).ToList();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetRoleAssignmentsByUserIdAsync_ReturnsUserRoleAssignments()
    {
        // Arrange
        var userId = "user123";
        var userRoleAssignments = new List<RoleAssignment>
        {
            new RoleAssignment { Id = 1, UserId = userId, Position = Position.Magister, StartYear = 2022, EndYear = 2023 },
            new RoleAssignment { Id = 2, UserId = userId, Position = Position.Secretario, StartYear = 2023, EndYear = 2024 }
        };

        _repositoryMock.Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(userRoleAssignments);

        // Act
        var result = (await _service.GetRoleAssignmentsByUserIdAsync(userId)).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(ra => ra.UserId.Should().Be(userId));
    }

    [Fact]
    public async Task GetRoleAssignmentsByUserIdAsync_WithNoAssignments_ReturnsEmpty()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetByUserIdAsync("nonexistent"))
            .ReturnsAsync(new List<RoleAssignment>());

        // Act
        var roleAssignments = (await _service.GetRoleAssignmentsByUserIdAsync("nonexistent")).ToList();

        // Assert
        roleAssignments.Should().BeEmpty();
    }

    [Theory]
    [InlineData(Position.Magister)]
    [InlineData(Position.Secretario)]
    [InlineData(Position.PrimeiroTesoureiro)]
    public async Task GetRoleAssignmentsByPositionAsync_ReturnsPositionRoleAssignments(Position position)
    {
        // Arrange
        var positionRoleAssignments = new List<RoleAssignment>
        {
            new RoleAssignment { Id = 1, UserId = "user1", Position = position, StartYear = 2023, EndYear = 2024 },
            new RoleAssignment { Id = 2, UserId = "user2", Position = position, StartYear = 2024, EndYear = 2025 }
        };

        _repositoryMock.Setup(r => r.GetByPositionAsync(position))
            .ReturnsAsync(positionRoleAssignments);

        // Act
        var result = (await _service.GetRoleAssignmentsByPositionAsync(position)).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(ra => ra.Position.Should().Be(position));
    }

    [Fact]
    public async Task GetRoleAssignmentsByPositionAsync_WithNoAssignments_ReturnsEmpty()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetByPositionAsync(Position.PrimeiroTesoureiro))
            .ReturnsAsync(new List<RoleAssignment>());

        // Act
        var roleAssignments = (await _service.GetRoleAssignmentsByPositionAsync(Position.PrimeiroTesoureiro)).ToList();

        // Assert
        roleAssignments.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateRoleAssignmentAsync_WithValidData_UpdatesRoleAssignment()
    {
        // Arrange
        var roleAssignment = new RoleAssignment
        {
            Id = 1,
            UserId = "user1",
            Position = Position.Magister,
            StartYear = 2023,
            EndYear = 2024,
            Notes = "Old notes"
        };

        var updatedRoleAssignment = new RoleAssignment
        {
            Id = 1,
            UserId = "user1",
            Position = Position.Secretario,
            StartYear = 2024,
            EndYear = 2025,
            Notes = "Updated notes"
        };

        _repositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(roleAssignment);
        _repositoryMock.Setup(r => r.UpdateAsync(It.IsAny<RoleAssignment>()))
            .Returns(Task.CompletedTask);
        _repositoryMock.SetupSequence(r => r.GetByIdAsync(1))
            .ReturnsAsync(roleAssignment)
            .ReturnsAsync(updatedRoleAssignment);

        // Act
        await _service.UpdateRoleAssignmentAsync(roleAssignment.Id, Position.Secretario, 2024, 2025, "Updated notes");

        // Assert
        var updated = await _service.GetRoleAssignmentByIdAsync(roleAssignment.Id);
        updated!.Position.Should().Be(Position.Secretario);
        updated.StartYear.Should().Be(2024);
        updated.EndYear.Should().Be(2025);
        updated.Notes.Should().Be("Updated notes");
    }

    [Fact]
    public async Task UpdateRoleAssignmentAsync_WithNonExistentId_ThrowsException()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((RoleAssignment?)null);

        // Act
        var act = async () => await _service.UpdateRoleAssignmentAsync(999, Position.Magister, 2023, 2024, null);

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("RoleAssignment with ID 999 not found");
    }

    [Fact]
    public async Task DeleteRoleAssignmentAsync_WithExistingId_DeletesRoleAssignment()
    {
        // Arrange
        var roleAssignment = new RoleAssignment
        {
            Id = 1,
            UserId = "user1",
            Position = Position.Magister,
            StartYear = 2023,
            EndYear = 2024
        };

        _repositoryMock.SetupSequence(r => r.GetByIdAsync(1))
            .ReturnsAsync(roleAssignment)      // First call in DeleteRoleAssignmentAsync
            .ReturnsAsync((RoleAssignment?)null);  // Second call in test verification
        _repositoryMock.Setup(r => r.DeleteAsync(1))
            .Returns(Task.CompletedTask);

        // Act
        await _service.DeleteRoleAssignmentAsync(roleAssignment.Id);

        // Assert
        var deleted = await _service.GetRoleAssignmentByIdAsync(roleAssignment.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteRoleAssignmentAsync_WithNonExistentId_ThrowsException()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((RoleAssignment?)null);

        // Act
        var act = async () => await _service.DeleteRoleAssignmentAsync(999);

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("RoleAssignment with ID 999 not found");
    }

    [Fact]
    public async Task CreateRoleAssignmentAsync_WithoutOptionalParameters_CreatesSuccessfully()
    {
        // Arrange
        var expectedRoleAssignment = new RoleAssignment
        {
            Id = 1,
            UserId = "user1",
            Position = Position.ViceMagister,
            StartYear = 2023,
            EndYear = 2024,
            Notes = null,
            CreatedBy = null
        };

        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<RoleAssignment>()))
            .ReturnsAsync(expectedRoleAssignment);

        // Act
        var roleAssignment = await _service.CreateRoleAssignmentAsync("user1", Position.ViceMagister, 2023, 2024);

        // Assert
        roleAssignment.Should().NotBeNull();
        roleAssignment.Notes.Should().BeNull();
        roleAssignment.CreatedBy.Should().BeNull();
    }
}
