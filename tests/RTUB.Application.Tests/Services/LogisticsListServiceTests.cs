using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for LogisticsListService
/// Tests business logic and service layer operations for logistics lists
/// </summary>
public class LogisticsListServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly LogisticsListService _service;

    public LogisticsListServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options, Mock.Of<IHttpContextAccessor>(), new AuditContext());
        _service = new LogisticsListService(_context);
    }

    [Fact]
    public async Task GetListByIdAsync_ExistingList_ReturnsList()
    {
        // Arrange
        var board = LogisticsBoard.Create("Test Board");
        _context.LogisticsBoards.Add(board);
        await _context.SaveChangesAsync();

        var list = LogisticsList.Create("Test List", board.Id, 0);
        _context.LogisticsLists.Add(list);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetListByIdAsync(list.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(list.Id);
        result.Name.Should().Be("Test List");
    }

    [Fact]
    public async Task GetListByIdAsync_NonExistingList_ReturnsNull()
    {
        // Act
        var result = await _service.GetListByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllListsAsync_ReturnsAllListsOrderedByPosition()
    {
        // Arrange
        var board = LogisticsBoard.Create("Test Board");
        _context.LogisticsBoards.Add(board);
        await _context.SaveChangesAsync();

        var list1 = LogisticsList.Create("List 1", board.Id, 2);
        var list2 = LogisticsList.Create("List 2", board.Id, 0);
        var list3 = LogisticsList.Create("List 3", board.Id, 1);
        _context.LogisticsLists.AddRange(list1, list2, list3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAllListsAsync();

        // Assert
        result.Should().HaveCount(3);
        var lists = result.ToList();
        lists[0].Name.Should().Be("List 2"); // Position 0
        lists[1].Name.Should().Be("List 3"); // Position 1
        lists[2].Name.Should().Be("List 1"); // Position 2
    }

    [Fact]
    public async Task GetListsWithCardsAsync_ReturnsListsWithCardsIncluded()
    {
        // Arrange
        var board = LogisticsBoard.Create("Test Board");
        _context.LogisticsBoards.Add(board);
        await _context.SaveChangesAsync();

        var list = LogisticsList.Create("Test List", board.Id, 0);
        _context.LogisticsLists.Add(list);
        await _context.SaveChangesAsync();

        var card1 = LogisticsCard.Create("Card 1", list.Id, 0);
        var card2 = LogisticsCard.Create("Card 2", list.Id, 1);
        _context.LogisticsCards.AddRange(card1, card2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetListsWithCardsAsync();

        // Assert
        result.Should().HaveCount(1);
        var resultList = result.First();
        resultList.Cards.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetListsByBoardIdAsync_ReturnsListsForBoard()
    {
        // Arrange
        var board1 = LogisticsBoard.Create("Board 1");
        var board2 = LogisticsBoard.Create("Board 2");
        _context.LogisticsBoards.AddRange(board1, board2);
        await _context.SaveChangesAsync();

        var list1 = LogisticsList.Create("List 1", board1.Id, 0);
        var list2 = LogisticsList.Create("List 2", board1.Id, 1);
        var list3 = LogisticsList.Create("List 3", board2.Id, 0);
        _context.LogisticsLists.AddRange(list1, list2, list3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetListsByBoardIdAsync(board1.Id);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(l => l.BoardId == board1.Id);
    }

    [Fact]
    public async Task GetListsWithCardsByBoardIdAsync_ReturnsListsWithCardsForBoard()
    {
        // Arrange
        var board1 = LogisticsBoard.Create("Board 1");
        var board2 = LogisticsBoard.Create("Board 2");
        _context.LogisticsBoards.AddRange(board1, board2);
        await _context.SaveChangesAsync();

        var list1 = LogisticsList.Create("List 1", board1.Id, 0);
        var list2 = LogisticsList.Create("List 2", board2.Id, 0);
        _context.LogisticsLists.AddRange(list1, list2);
        await _context.SaveChangesAsync();

        var card1 = LogisticsCard.Create("Card 1", list1.Id, 0);
        var card2 = LogisticsCard.Create("Card 2", list1.Id, 1);
        var card3 = LogisticsCard.Create("Card 3", list2.Id, 0);
        _context.LogisticsCards.AddRange(card1, card2, card3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetListsWithCardsByBoardIdAsync(board1.Id);

        // Assert
        result.Should().HaveCount(1);
        var resultList = result.First();
        resultList.BoardId.Should().Be(board1.Id);
        resultList.Cards.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateListAsync_WithValidData_CreatesList()
    {
        // Arrange
        var board = LogisticsBoard.Create("Test Board");
        _context.LogisticsBoards.Add(board);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CreateListAsync("New List", board.Id, 0);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("New List");
        result.BoardId.Should().Be(board.Id);
        result.Position.Should().Be(0);
    }

    [Fact]
    public async Task UpdateListAsync_WithValidData_UpdatesList()
    {
        // Arrange
        var board = LogisticsBoard.Create("Test Board");
        _context.LogisticsBoards.Add(board);
        await _context.SaveChangesAsync();

        var list = LogisticsList.Create("Original Name", board.Id, 0);
        _context.LogisticsLists.Add(list);
        await _context.SaveChangesAsync();

        // Act
        await _service.UpdateListAsync(list.Id, "Updated Name");

        // Assert
        var updated = await _context.LogisticsLists.FindAsync(list.Id);
        updated!.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task UpdateListAsync_NonExistingList_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _service.UpdateListAsync(999, "Name");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*não encontrada*");
    }

    [Fact]
    public async Task UpdateListPositionAsync_WithValidPosition_UpdatesPosition()
    {
        // Arrange
        var board = LogisticsBoard.Create("Test Board");
        _context.LogisticsBoards.Add(board);
        await _context.SaveChangesAsync();

        var list = LogisticsList.Create("Test List", board.Id, 0);
        _context.LogisticsLists.Add(list);
        await _context.SaveChangesAsync();

        // Act
        await _service.UpdateListPositionAsync(list.Id, 5);

        // Assert
        var updated = await _context.LogisticsLists.FindAsync(list.Id);
        updated!.Position.Should().Be(5);
    }

    [Fact]
    public async Task DeleteListAsync_ExistingListWithCards_DeletesListAndCards()
    {
        // Arrange
        var board = LogisticsBoard.Create("Test Board");
        _context.LogisticsBoards.Add(board);
        await _context.SaveChangesAsync();

        var list = LogisticsList.Create("Test List", board.Id, 0);
        _context.LogisticsLists.Add(list);
        await _context.SaveChangesAsync();

        var card1 = LogisticsCard.Create("Card 1", list.Id, 0);
        var card2 = LogisticsCard.Create("Card 2", list.Id, 1);
        _context.LogisticsCards.AddRange(card1, card2);
        await _context.SaveChangesAsync();

        // Act
        await _service.DeleteListAsync(list.Id);

        // Assert
        var deletedList = await _context.LogisticsLists.FindAsync(list.Id);
        deletedList.Should().BeNull();

        var cards = await _context.LogisticsCards
            .Where(c => c.ListId == list.Id)
            .ToListAsync();
        cards.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteListAsync_NonExistingList_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _service.DeleteListAsync(999);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*não encontrada*");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
