using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for LogisticsCardService
/// Tests business logic and service layer operations for logistics cards
/// </summary>
public class LogisticsCardServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly LogisticsCardService _service;

    public LogisticsCardServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options, Mock.Of<IHttpContextAccessor>(), new AuditContext());
        _service = new LogisticsCardService(_context);
    }

    [Fact]
    public async Task GetCardByIdAsync_ExistingCard_ReturnsCardWithIncludes()
    {
        // Arrange
        var board = LogisticsBoard.Create("Test Board");
        _context.LogisticsBoards.Add(board);
        await _context.SaveChangesAsync();

        var list = LogisticsList.Create("Test List", board.Id, 0);
        _context.LogisticsLists.Add(list);
        await _context.SaveChangesAsync();

        var card = LogisticsCard.Create("Test Card", list.Id, 0, "Description");
        _context.LogisticsCards.Add(card);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetCardByIdAsync(card.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(card.Id);
        result.Title.Should().Be("Test Card");
        result.Description.Should().Be("Description");
    }

    [Fact]
    public async Task GetCardByIdAsync_NonExistingCard_ReturnsNull()
    {
        // Act
        var result = await _service.GetCardByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCardsByListIdAsync_ReturnsCardsOrderedByPosition()
    {
        // Arrange
        var board = LogisticsBoard.Create("Test Board");
        _context.LogisticsBoards.Add(board);
        await _context.SaveChangesAsync();

        var list = LogisticsList.Create("Test List", board.Id, 0);
        _context.LogisticsLists.Add(list);
        await _context.SaveChangesAsync();

        var card1 = LogisticsCard.Create("Card 1", list.Id, 2);
        var card2 = LogisticsCard.Create("Card 2", list.Id, 0);
        var card3 = LogisticsCard.Create("Card 3", list.Id, 1);
        _context.LogisticsCards.AddRange(card1, card2, card3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetCardsByListIdAsync(list.Id);

        // Assert
        result.Should().HaveCount(3);
        var cards = result.ToList();
        cards[0].Title.Should().Be("Card 2"); // Position 0
        cards[1].Title.Should().Be("Card 3"); // Position 1
        cards[2].Title.Should().Be("Card 1"); // Position 2
    }

    [Fact]
    public async Task CreateCardAsync_WithValidData_CreatesCard()
    {
        // Arrange
        var board = LogisticsBoard.Create("Test Board");
        _context.LogisticsBoards.Add(board);
        await _context.SaveChangesAsync();

        var list = LogisticsList.Create("Test List", board.Id, 0);
        _context.LogisticsLists.Add(list);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CreateCardAsync("New Card", list.Id, 0, "Test Description");

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("New Card");
        result.Description.Should().Be("Test Description");
        result.ListId.Should().Be(list.Id);
        result.Position.Should().Be(0);
    }

    [Fact]
    public async Task UpdateCardAsync_WithValidData_UpdatesCard()
    {
        // Arrange
        var board = LogisticsBoard.Create("Test Board");
        _context.LogisticsBoards.Add(board);
        await _context.SaveChangesAsync();

        var list = LogisticsList.Create("Test List", board.Id, 0);
        _context.LogisticsLists.Add(list);
        await _context.SaveChangesAsync();

        var card = LogisticsCard.Create("Original Title", list.Id, 0);
        _context.LogisticsCards.Add(card);
        await _context.SaveChangesAsync();

        // Act
        await _service.UpdateCardAsync(card.Id, "Updated Title", "Updated Description");

        // Assert
        var updated = await _context.LogisticsCards.FindAsync(card.Id);
        updated!.Title.Should().Be("Updated Title");
        updated.Description.Should().Be("Updated Description");
    }

    [Fact]
    public async Task UpdateCardAsync_NonExistingCard_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _service.UpdateCardAsync(999, "Title", "Description");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*não encontrado*");
    }

    [Fact]
    public async Task MoveCardAsync_WithValidData_MovesCard()
    {
        // Arrange
        var board = LogisticsBoard.Create("Test Board");
        _context.LogisticsBoards.Add(board);
        await _context.SaveChangesAsync();

        var list1 = LogisticsList.Create("List 1", board.Id, 0);
        var list2 = LogisticsList.Create("List 2", board.Id, 1);
        _context.LogisticsLists.AddRange(list1, list2);
        await _context.SaveChangesAsync();

        var card = LogisticsCard.Create("Test Card", list1.Id, 0);
        _context.LogisticsCards.Add(card);
        await _context.SaveChangesAsync();

        // Act
        await _service.MoveCardAsync(card.Id, list2.Id, 5);

        // Assert
        var moved = await _context.LogisticsCards.FindAsync(card.Id);
        moved!.ListId.Should().Be(list2.Id);
        moved.Position.Should().Be(5);
    }

    [Fact]
    public async Task UpdateCardPositionAsync_WithValidData_UpdatesPosition()
    {
        // Arrange
        var board = LogisticsBoard.Create("Test Board");
        _context.LogisticsBoards.Add(board);
        await _context.SaveChangesAsync();

        var list = LogisticsList.Create("Test List", board.Id, 0);
        _context.LogisticsLists.Add(list);
        await _context.SaveChangesAsync();

        var card = LogisticsCard.Create("Test Card", list.Id, 0);
        _context.LogisticsCards.Add(card);
        await _context.SaveChangesAsync();

        // Act
        await _service.UpdateCardPositionAsync(card.Id, 10);

        // Assert
        var updated = await _context.LogisticsCards.FindAsync(card.Id);
        updated!.Position.Should().Be(10);
    }

    [Fact]
    public async Task AssociateCardWithEventAsync_WithValidEvent_AssociatesEvent()
    {
        // Arrange
        var board = LogisticsBoard.Create("Test Board");
        _context.LogisticsBoards.Add(board);
        await _context.SaveChangesAsync();

        var list = LogisticsList.Create("Test List", board.Id, 0);
        _context.LogisticsLists.Add(list);
        await _context.SaveChangesAsync();

        var card = LogisticsCard.Create("Test Card", list.Id, 0);
        _context.LogisticsCards.Add(card);

        var eventEntity = Event.Create("Test Event", DateTime.Now, "Location", EventType.Atuacao);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // Act
        await _service.AssociateCardWithEventAsync(card.Id, eventEntity.Id);

        // Assert
        var updated = await _context.LogisticsCards.FindAsync(card.Id);
        updated!.EventId.Should().Be(eventEntity.Id);
    }

    [Fact]
    public async Task AssociateCardWithEventAsync_WithNullEvent_RemovesAssociation()
    {
        // Arrange
        var board = LogisticsBoard.Create("Test Board");
        _context.LogisticsBoards.Add(board);
        await _context.SaveChangesAsync();

        var list = LogisticsList.Create("Test List", board.Id, 0);
        _context.LogisticsLists.Add(list);
        await _context.SaveChangesAsync();

        var eventEntity = Event.Create("Test Event", DateTime.Now, "Location", EventType.Atuacao);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var card = LogisticsCard.Create("Test Card", list.Id, 0);
        card.AssociateWithEvent(eventEntity.Id);
        _context.LogisticsCards.Add(card);
        await _context.SaveChangesAsync();

        // Act
        await _service.AssociateCardWithEventAsync(card.Id, null);

        // Assert
        var updated = await _context.LogisticsCards.FindAsync(card.Id);
        updated!.EventId.Should().BeNull();
    }

    [Fact]
    public async Task AssociateCardWithEventAsync_WithNonExistingEvent_ThrowsException()
    {
        // Arrange
        var board = LogisticsBoard.Create("Test Board");
        _context.LogisticsBoards.Add(board);
        await _context.SaveChangesAsync();

        var list = LogisticsList.Create("Test List", board.Id, 0);
        _context.LogisticsLists.Add(list);
        await _context.SaveChangesAsync();

        var card = LogisticsCard.Create("Test Card", list.Id, 0);
        _context.LogisticsCards.Add(card);
        await _context.SaveChangesAsync();

        // Act & Assert
        var act = async () => await _service.AssociateCardWithEventAsync(card.Id, 999);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Evento*não encontrado*");
    }

    [Fact]
    public async Task AssignCardToUserAsync_WithValidUser_AssignsUser()
    {
        // Arrange
        var board = LogisticsBoard.Create("Test Board");
        _context.LogisticsBoards.Add(board);
        await _context.SaveChangesAsync();

        var list = LogisticsList.Create("Test List", board.Id, 0);
        _context.LogisticsLists.Add(list);
        await _context.SaveChangesAsync();

        var card = LogisticsCard.Create("Test Card", list.Id, 0);
        _context.LogisticsCards.Add(card);

        var user = new ApplicationUser { Id = "user123", UserName = "testuser", Email = "test@example.com", FirstName = "Test", LastName = "User", Nickname = "Test", PhoneContact = "123456789" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        await _service.AssignCardToUserAsync(card.Id, user.Id);

        // Assert
        var updated = await _context.LogisticsCards.FindAsync(card.Id);
        updated!.AssignedToUserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task AssignCardToUserAsync_WithNullUser_RemovesAssignment()
    {
        // Arrange
        var board = LogisticsBoard.Create("Test Board");
        _context.LogisticsBoards.Add(board);
        await _context.SaveChangesAsync();

        var list = LogisticsList.Create("Test List", board.Id, 0);
        _context.LogisticsLists.Add(list);
        await _context.SaveChangesAsync();

        var user = new ApplicationUser { Id = "user123", UserName = "testuser", Email = "test@example.com", FirstName = "Test", LastName = "User", Nickname = "Test", PhoneContact = "123456789" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var card = LogisticsCard.Create("Test Card", list.Id, 0);
        card.AssignToUser(user.Id);
        _context.LogisticsCards.Add(card);
        await _context.SaveChangesAsync();

        // Act
        await _service.AssignCardToUserAsync(card.Id, null);

        // Assert
        var updated = await _context.LogisticsCards.FindAsync(card.Id);
        updated!.AssignedToUserId.Should().BeNull();
    }

    [Fact]
    public async Task DeleteCardAsync_ExistingCard_DeletesCard()
    {
        // Arrange
        var board = LogisticsBoard.Create("Test Board");
        _context.LogisticsBoards.Add(board);
        await _context.SaveChangesAsync();

        var list = LogisticsList.Create("Test List", board.Id, 0);
        _context.LogisticsLists.Add(list);
        await _context.SaveChangesAsync();

        var card = LogisticsCard.Create("Test Card", list.Id, 0);
        _context.LogisticsCards.Add(card);
        await _context.SaveChangesAsync();

        // Act
        await _service.DeleteCardAsync(card.Id);

        // Assert
        var deleted = await _context.LogisticsCards.FindAsync(card.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task SetCardStatusAsync_WithValidStatus_UpdatesStatus()
    {
        // Arrange
        var board = LogisticsBoard.Create("Test Board");
        _context.LogisticsBoards.Add(board);
        await _context.SaveChangesAsync();

        var list = LogisticsList.Create("Test List", board.Id, 0);
        _context.LogisticsLists.Add(list);
        await _context.SaveChangesAsync();

        var card = LogisticsCard.Create("Test Card", list.Id, 0);
        _context.LogisticsCards.Add(card);
        await _context.SaveChangesAsync();

        // Act
        await _service.SetCardStatusAsync(card.Id, CardStatus.InProgress);

        // Assert
        var updated = await _context.LogisticsCards.FindAsync(card.Id);
        updated!.Status.Should().Be(CardStatus.InProgress);
    }

    [Fact]
    public async Task SetCardLabelsAsync_WithValidLabels_UpdatesLabels()
    {
        // Arrange
        var board = LogisticsBoard.Create("Test Board");
        _context.LogisticsBoards.Add(board);
        await _context.SaveChangesAsync();

        var list = LogisticsList.Create("Test List", board.Id, 0);
        _context.LogisticsLists.Add(list);
        await _context.SaveChangesAsync();

        var card = LogisticsCard.Create("Test Card", list.Id, 0);
        _context.LogisticsCards.Add(card);
        await _context.SaveChangesAsync();

        // Act
        await _service.SetCardLabelsAsync(card.Id, "urgent,important");

        // Assert
        var updated = await _context.LogisticsCards.FindAsync(card.Id);
        updated!.Labels.Should().Be("urgent,important");
    }

    [Fact]
    public async Task SetCardDatesAsync_WithValidDates_UpdatesDates()
    {
        // Arrange
        var board = LogisticsBoard.Create("Test Board");
        _context.LogisticsBoards.Add(board);
        await _context.SaveChangesAsync();

        var list = LogisticsList.Create("Test List", board.Id, 0);
        _context.LogisticsLists.Add(list);
        await _context.SaveChangesAsync();

        var card = LogisticsCard.Create("Test Card", list.Id, 0);
        _context.LogisticsCards.Add(card);
        await _context.SaveChangesAsync();

        var startDate = DateTime.Now;
        var dueDate = DateTime.Now.AddDays(7);
        var reminderDate = DateTime.Now.AddDays(5);

        // Act
        await _service.SetCardDatesAsync(card.Id, startDate, dueDate, reminderDate);

        // Assert
        var updated = await _context.LogisticsCards.FindAsync(card.Id);
        updated!.StartDate.Should().BeCloseTo(startDate, TimeSpan.FromSeconds(1));
        updated.DueDate.Should().BeCloseTo(dueDate, TimeSpan.FromSeconds(1));
        updated.ReminderDate.Should().BeCloseTo(reminderDate, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task SetCardChecklistAsync_WithValidChecklist_UpdatesChecklist()
    {
        // Arrange
        var board = LogisticsBoard.Create("Test Board");
        _context.LogisticsBoards.Add(board);
        await _context.SaveChangesAsync();

        var list = LogisticsList.Create("Test List", board.Id, 0);
        _context.LogisticsLists.Add(list);
        await _context.SaveChangesAsync();

        var card = LogisticsCard.Create("Test Card", list.Id, 0);
        _context.LogisticsCards.Add(card);
        await _context.SaveChangesAsync();

        var checklistJson = "[{\"item\":\"Task 1\",\"done\":false}]";

        // Act
        await _service.SetCardChecklistAsync(card.Id, checklistJson);

        // Assert
        var updated = await _context.LogisticsCards.FindAsync(card.Id);
        updated!.ChecklistJson.Should().Be(checklistJson);
    }

    [Fact]
    public async Task SetCardAttachmentsAsync_WithValidAttachments_UpdatesAttachments()
    {
        // Arrange
        var board = LogisticsBoard.Create("Test Board");
        _context.LogisticsBoards.Add(board);
        await _context.SaveChangesAsync();

        var list = LogisticsList.Create("Test List", board.Id, 0);
        _context.LogisticsLists.Add(list);
        await _context.SaveChangesAsync();

        var card = LogisticsCard.Create("Test Card", list.Id, 0);
        _context.LogisticsCards.Add(card);
        await _context.SaveChangesAsync();

        var attachmentsJson = "[{\"name\":\"file.pdf\",\"url\":\"http://example.com/file.pdf\"}]";

        // Act
        await _service.SetCardAttachmentsAsync(card.Id, attachmentsJson);

        // Assert
        var updated = await _context.LogisticsCards.FindAsync(card.Id);
        updated!.AttachmentsJson.Should().Be(attachmentsJson);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
