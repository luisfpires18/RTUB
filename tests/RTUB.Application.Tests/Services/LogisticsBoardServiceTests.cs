using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for LogisticsBoardService
/// Tests business logic and service layer operations
/// </summary>
public class LogisticsBoardServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly LogisticsBoardService _service;

    public LogisticsBoardServiceTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _context = _fixture.CreateContext();
        var boardRepo = new LogisticsBoardRepository(_context);
        var listRepo = new LogisticsListRepository(_context);
        var cardRepo = new LogisticsCardRepository(_context);
        _service = new LogisticsBoardService(boardRepo, listRepo, cardRepo);
    }

    [Fact]
    public async Task CreateBoardAsync_WithValidData_CreatesBoard()
    {
        // Arrange
        var name = "Test Board";
        var description = "Test Description";

        // Act
        var result = await _service.CreateBoardAsync(name, description);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be(name);
        result.Description.Should().Be(description);
    }

    [Fact]
    public async Task GetBoardByIdAsync_ExistingBoard_ReturnsBoard()
    {
        // Arrange
        var board = LogisticsBoard.Create("Test Board", "Description");
        _context.LogisticsBoards.Add(board);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetBoardByIdAsync(board.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(board.Id);
        result.Name.Should().Be("Test Board");
    }

    [Fact]
    public async Task GetBoardByIdAsync_NonExistingBoard_ReturnsNull()
    {
        // Act
        var result = await _service.GetBoardByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllBoardsAsync_ReturnsAllBoards()
    {
        // Arrange
        var board1 = LogisticsBoard.Create("Board 1", "Description 1");
        var board2 = LogisticsBoard.Create("Board 2", "Description 2");
        _context.LogisticsBoards.AddRange(board1, board2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAllBoardsAsync();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetBoardsPagedAsync_ReturnsPagedResults()
    {
        // Arrange
        var board1 = LogisticsBoard.Create("Board 1", "Description 1");
        var board2 = LogisticsBoard.Create("Board 2", "Description 2");
        var board3 = LogisticsBoard.Create("Board 3", "Description 3");
        _context.LogisticsBoards.AddRange(board1, board2, board3);
        await _context.SaveChangesAsync();

        // Act
        var (boards, totalCount) = await _service.GetBoardsPagedAsync(1, 2);

        // Assert
        boards.Should().HaveCount(2);
        totalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetBoardsPagedAsync_WithSearchTerm_FiltersResults()
    {
        // Arrange
        var board1 = LogisticsBoard.Create("Test Board", "Description 1");
        var board2 = LogisticsBoard.Create("Other Board", "Description 2");
        var board3 = LogisticsBoard.Create("Test Another", "Description 3");
        _context.LogisticsBoards.AddRange(board1, board2, board3);
        await _context.SaveChangesAsync();

        // Act
        var (boards, totalCount) = await _service.GetBoardsPagedAsync(1, 10, "Test");

        // Assert
        boards.Should().HaveCount(2);
        totalCount.Should().Be(2);
        boards.Should().OnlyContain(b => b.Name.Contains("Test"));
    }

    [Fact]
    public async Task UpdateBoardAsync_WithValidData_UpdatesBoard()
    {
        // Arrange
        var board = LogisticsBoard.Create("Original Name", "Original Description");
        _context.LogisticsBoards.Add(board);
        await _context.SaveChangesAsync();

        // Act
        await _service.UpdateBoardAsync(board.Id, "Updated Name", "Updated Description");

        // Assert
        var updated = await _context.LogisticsBoards.FindAsync(board.Id);
        updated!.Name.Should().Be("Updated Name");
        updated.Description.Should().Be("Updated Description");
    }

    [Fact]
    public async Task UpdateBoardAsync_NonExistingBoard_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _service.UpdateBoardAsync(999, "Name", "Description");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*não encontrado*");
    }

    [Fact]
    public async Task AssociateBoardWithEventAsync_WithValidData_AssociatesEvent()
    {
        // Arrange
        var board = LogisticsBoard.Create("Test Board", "Description");
        _context.LogisticsBoards.Add(board);

        var eventEntity = Event.Create("Test Event", DateTime.Now.AddDays(7), "Test Location", EventType.Festival);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // Act
        await _service.AssociateBoardWithEventAsync(board.Id, eventEntity.Id);

        // Assert
        var updated = await _context.LogisticsBoards.FindAsync(board.Id);
        updated!.EventId.Should().Be(eventEntity.Id);
    }

    [Fact]
    public async Task AssociateBoardWithEventAsync_WithNull_RemovesAssociation()
    {
        // Arrange
        var board = LogisticsBoard.Create("Test Board", "Description");
        var eventEntity = Event.Create("Test Event", DateTime.Now.AddDays(7), "Test Location", EventType.Festival);
        _context.Events.Add(eventEntity);
        _context.LogisticsBoards.Add(board);
        await _context.SaveChangesAsync();

        board.AssociateWithEvent(eventEntity.Id);
        await _context.SaveChangesAsync();

        // Act
        await _service.AssociateBoardWithEventAsync(board.Id, null);

        // Assert
        var updated = await _context.LogisticsBoards.FindAsync(board.Id);
        updated!.EventId.Should().BeNull();
    }

    [Fact]
    public async Task DeleteBoardAsync_ExistingBoard_DeletesBoard()
    {
        // Arrange
        var board = LogisticsBoard.Create("Test Board", "Description");
        _context.LogisticsBoards.Add(board);
        await _context.SaveChangesAsync();

        // Act
        await _service.DeleteBoardAsync(board.Id);

        // Assert
        var deleted = await _context.LogisticsBoards.FindAsync(board.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteBoardAsync_NonExistingBoard_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _service.DeleteBoardAsync(999);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*não encontrado*");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
