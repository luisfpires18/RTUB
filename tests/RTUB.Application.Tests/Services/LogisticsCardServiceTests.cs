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
/// Unit tests for LogisticsCardService
/// Tests business logic and service layer operations for logistics cards
/// </summary>
public class LogisticsCardServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly LogisticsCardService _service;

    public LogisticsCardServiceTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _context = _fixture.CreateContext();
        var cardRepo = new LogisticsCardRepository(_fixture.CreateContextFactory());
        var eventRepo = new EventRepository(_fixture.CreateContextFactory());
        var documentStorageService = new Mock<RTUB.Application.Interfaces.IDocumentStorageService>().Object;
        var assignmentRepo = new Mock<RTUB.Application.Interfaces.IRepository<LogisticsCardAssignment>>().Object;
        var reminderRepo = new Mock<RTUB.Application.Interfaces.IRepository<LogisticsCardReminder>>().Object;
        _service = new LogisticsCardService(cardRepo, eventRepo, documentStorageService, assignmentRepo, reminderRepo);
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
        _context.ChangeTracker.Clear();
        var updated = await _context.LogisticsCards.FindAsync(card.Id);
        updated!.Title.Should().Be("Updated Title");
        updated.Description.Should().Be("Updated Description");
    }

    [Fact]
    public async Task UpdateCardAsync_NonExistingCard_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _service.UpdateCardAsync(999, "Title", "Description");
        await act.Should().ThrowAsync<RTUB.Core.Exceptions.EntityNotFoundException>();
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
        _context.ChangeTracker.Clear();
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
        _context.ChangeTracker.Clear();
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
        _context.ChangeTracker.Clear();
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
        _context.ChangeTracker.Clear();
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

        var user = new ApplicationUser { Id = "user123", UserName = "testuser", Email = "test@example.com", FirstName = "Test", LastName = "User", Nickname = "Test", PhoneNumber = "123456789" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        await _service.AssignCardToUserAsync(card.Id, user.Id);

        // Assert
        _context.ChangeTracker.Clear();
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

        var user = new ApplicationUser { Id = "user123", UserName = "testuser", Email = "test@example.com", FirstName = "Test", LastName = "User", Nickname = "Test", PhoneNumber = "123456789" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var card = LogisticsCard.Create("Test Card", list.Id, 0);
        card.AssignToUser(user.Id);
        _context.LogisticsCards.Add(card);
        await _context.SaveChangesAsync();

        // Act
        await _service.AssignCardToUserAsync(card.Id, null);

        // Assert
        _context.ChangeTracker.Clear();
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
        _context.ChangeTracker.Clear();
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
        _context.ChangeTracker.Clear();
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
        _context.ChangeTracker.Clear();
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
        _context.ChangeTracker.Clear();
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
        _context.ChangeTracker.Clear();
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
        _context.ChangeTracker.Clear();
        var updated = await _context.LogisticsCards.FindAsync(card.Id);
        updated!.AttachmentsJson.Should().Be(attachmentsJson);
    }

    [Fact]
    public async Task UploadCardAttachmentAsync_WithValidFile_UploadsFile()
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

        var mockDocumentStorage = new Mock<RTUB.Application.Interfaces.IDocumentStorageService>();
        var expectedPath = "docs/TestEnvironment/2024-2025/Logistics/Test Board/test-file.pdf";
        mockDocumentStorage.Setup(s => s.UploadDocumentAsync(
            It.IsAny<string>(),
            "test-file.pdf",
            It.IsAny<Stream>(),
            "application/pdf"))
            .ReturnsAsync(expectedPath);

        var service = new LogisticsCardService(
            new LogisticsCardRepository(_fixture.CreateContextFactory()),
            new EventRepository(_fixture.CreateContextFactory()),
            mockDocumentStorage.Object,
            new Repository<LogisticsCardAssignment>(_fixture.CreateContextFactory()),
            new Repository<LogisticsCardReminder>(_fixture.CreateContextFactory()));

        var fileStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var boardName = "Test Board";
        var fileName = "test-file.pdf";
        var contentType = "application/pdf";
        var environmentName = "TestEnvironment";

        // Act
        var result = await service.UploadCardAttachmentAsync(
            card.Id, boardName, fileName, fileStream, contentType, environmentName);

        // Assert
        result.Should().Be(expectedPath);
        mockDocumentStorage.Verify(s => s.UploadDocumentAsync(
            It.Is<string>(path => path.Contains("docs/TestEnvironment") && path.Contains("Logistics/Test Board")),
            fileName,
            It.IsAny<Stream>(),
            contentType), Times.Once);
    }

    [Fact]
    public async Task UploadCardAttachmentAsync_WithInvalidCard_ThrowsException()
    {
        // Arrange
        var mockDocumentStorage = new Mock<RTUB.Application.Interfaces.IDocumentStorageService>();
        var service = new LogisticsCardService(
            new LogisticsCardRepository(_fixture.CreateContextFactory()),
            new EventRepository(_fixture.CreateContextFactory()),
            mockDocumentStorage.Object,
            new Repository<LogisticsCardAssignment>(_fixture.CreateContextFactory()),
            new Repository<LogisticsCardReminder>(_fixture.CreateContextFactory()));

        var fileStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var boardName = "Test Board";
        var fileName = "test-file.pdf";
        var contentType = "application/pdf";
        var environmentName = "TestEnvironment";

        // Act
        var act = async () => await service.UploadCardAttachmentAsync(
            999, boardName, fileName, fileStream, contentType, environmentName);

        // Assert
        await act.Should().ThrowAsync<RTUB.Core.Exceptions.EntityNotFoundException>();
        mockDocumentStorage.Verify(s => s.UploadDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task UploadCardAttachmentAsync_SanitizesBoardName()
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

        var mockDocumentStorage = new Mock<RTUB.Application.Interfaces.IDocumentStorageService>();
        var expectedPath = "docs/TestEnvironment/2024-2025/Logistics/TestBoard/test-file.pdf";
        mockDocumentStorage.Setup(s => s.UploadDocumentAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Stream>(),
            It.IsAny<string>()))
            .ReturnsAsync(expectedPath);

        var service = new LogisticsCardService(
            new LogisticsCardRepository(_fixture.CreateContextFactory()),
            new EventRepository(_fixture.CreateContextFactory()),
            mockDocumentStorage.Object,
            new Repository<LogisticsCardAssignment>(_fixture.CreateContextFactory()),
            new Repository<LogisticsCardReminder>(_fixture.CreateContextFactory()));

        var fileStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var boardName = "Test/Board..Name"; // Contains path traversal attempts
        var fileName = "test-file.pdf";
        var contentType = "application/pdf";
        var environmentName = "TestEnvironment";

        // Act
        var result = await service.UploadCardAttachmentAsync(
            card.Id, boardName, fileName, fileStream, contentType, environmentName);

        // Assert
        result.Should().Be(expectedPath);
        // Verify that the path contains sanitized board name (no slashes or dots)
        mockDocumentStorage.Verify(s => s.UploadDocumentAsync(
            It.Is<string>(path => !path.Contains("../") && !path.Contains("//")),
            It.IsAny<string>(),
            It.IsAny<Stream>(),
            It.IsAny<string>()), Times.Once);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
