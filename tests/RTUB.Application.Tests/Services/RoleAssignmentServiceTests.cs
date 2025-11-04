using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public class RoleAssignmentServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly RoleAssignmentService _service;

    public RoleAssignmentServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _service = new RoleAssignmentService(_context);
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
        var roleAssignment = await _service.CreateRoleAssignmentAsync("user1", Position.Magister, 2023, 2024);

        // Act
        var result = await _service.GetRoleAssignmentByIdAsync(roleAssignment.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(roleAssignment.Id);
    }

    [Fact]
    public async Task GetRoleAssignmentByIdAsync_WithNonExistentId_ReturnsNull()
    {
        // Act
        var result = await _service.GetRoleAssignmentByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllRoleAssignmentsAsync_ReturnsAllRoleAssignments()
    {
        // Arrange
        await _service.CreateRoleAssignmentAsync("user1", Position.Magister, 2023, 2024);
        await _service.CreateRoleAssignmentAsync("user2", Position.Secretario, 2023, 2024);
        await _service.CreateRoleAssignmentAsync("user3", Position.PrimeiroTesoureiro, 2024, 2025);

        // Act
        var roleAssignments = (await _service.GetAllRoleAssignmentsAsync()).ToList();

        // Assert
        roleAssignments.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetRoleAssignmentsByUserIdAsync_ReturnsUserRoleAssignments()
    {
        // Arrange
        var userId = "user123";
        await _service.CreateRoleAssignmentAsync(userId, Position.Magister, 2022, 2023);
        await _service.CreateRoleAssignmentAsync(userId, Position.Secretario, 2023, 2024);
        await _service.CreateRoleAssignmentAsync("otherUser", Position.PrimeiroTesoureiro, 2023, 2024);

        // Act
        var userRoleAssignments = (await _service.GetRoleAssignmentsByUserIdAsync(userId)).ToList();

        // Assert
        userRoleAssignments.Should().HaveCount(2);
        userRoleAssignments.Should().AllSatisfy(ra => ra.UserId.Should().Be(userId));
    }

    [Fact]
    public async Task GetRoleAssignmentsByUserIdAsync_WithNoAssignments_ReturnsEmpty()
    {
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
        await _service.CreateRoleAssignmentAsync("user1", position, 2023, 2024);
        await _service.CreateRoleAssignmentAsync("user2", position, 2024, 2025);
        await _service.CreateRoleAssignmentAsync("user3", Position.ViceMagister, 2023, 2024);

        // Act
        var positionRoleAssignments = (await _service.GetRoleAssignmentsByPositionAsync(position)).ToList();

        // Assert
        positionRoleAssignments.Should().HaveCount(2);
        positionRoleAssignments.Should().AllSatisfy(ra => ra.Position.Should().Be(position));
    }

    [Fact]
    public async Task GetRoleAssignmentsByPositionAsync_WithNoAssignments_ReturnsEmpty()
    {
        // Arrange
        await _service.CreateRoleAssignmentAsync("user1", Position.Magister, 2023, 2024);

        // Act
        var roleAssignments = (await _service.GetRoleAssignmentsByPositionAsync(Position.PrimeiroTesoureiro)).ToList();

        // Assert
        roleAssignments.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateRoleAssignmentAsync_WithValidData_UpdatesRoleAssignment()
    {
        // Arrange
        var roleAssignment = await _service.CreateRoleAssignmentAsync("user1", Position.Magister, 2023, 2024, "Old notes");

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
        // Act
        var act = async () => await _service.UpdateRoleAssignmentAsync(999, Position.Magister, 2023, 2024, null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("RoleAssignment with ID 999 not found");
    }

    [Fact]
    public async Task DeleteRoleAssignmentAsync_WithExistingId_DeletesRoleAssignment()
    {
        // Arrange
        var roleAssignment = await _service.CreateRoleAssignmentAsync("user1", Position.Magister, 2023, 2024);

        // Act
        await _service.DeleteRoleAssignmentAsync(roleAssignment.Id);

        // Assert
        var deleted = await _service.GetRoleAssignmentByIdAsync(roleAssignment.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteRoleAssignmentAsync_WithNonExistentId_ThrowsException()
    {
        // Act
        var act = async () => await _service.DeleteRoleAssignmentAsync(999);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("RoleAssignment with ID 999 not found");
    }

    [Fact]
    public async Task CreateRoleAssignmentAsync_WithoutOptionalParameters_CreatesSuccessfully()
    {
        // Act
        var roleAssignment = await _service.CreateRoleAssignmentAsync("user1", Position.ViceMagister, 2023, 2024);

        // Assert
        roleAssignment.Should().NotBeNull();
        roleAssignment.Notes.Should().BeNull();
        roleAssignment.CreatedBy.Should().BeNull();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
