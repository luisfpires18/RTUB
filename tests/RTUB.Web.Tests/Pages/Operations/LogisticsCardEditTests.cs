using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MockQueryable.Moq;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Web.Tests.Pages.Base;
using BoardEntity = RTUB.Core.Entities.LogisticsBoard;
using BoardPage = RTUB.Pages.Management.LogisticsBoard;

namespace RTUB.Web.Tests.Pages.Operations;

/// <summary>
/// Card edits in the details modal must persist and the board must be reloaded, so reopening a
/// card shows what was saved and one edit never writes back another edit's stale value. The board
/// service returns a fresh copy of a stored card on every call, like the AsNoTracking query does.
/// </summary>
public class LogisticsCardEditTests : PageTestBase
{
    private const int BoardId = 7;
    private const int ListId = 3;
    private const int CardId = 11;

    private readonly LogisticsCard _stored = new() { Id = CardId, Title = "Levar colunas", ListId = ListId };
    private readonly Mock<ILogisticsCardService> _cards;

    public LogisticsCardEditTests()
    {
        SetupService<ILogisticsListService>();
        _cards = SetupService<ILogisticsCardService>();

        SetupService<ILogisticsBoardService>()
            .Setup(s => s.GetBoardWithListsAndCardsAsync(BoardId))
            .ReturnsAsync(() => new BoardEntity
            {
                Id = BoardId,
                Name = "Arraial",
                Lists = new List<LogisticsList>
                {
                    new() { Id = ListId, Name = "Material", BoardId = BoardId, Cards = new List<LogisticsCard> { Copy(_stored) } }
                }
            });
        SetupService<IEventService>()
            .Setup(s => s.GetAllEventsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Event>());

        _cards.Setup(s => s.GetCardAttachmentsAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new List<DocumentMetadata>());
        _cards.Setup(s => s.GetCardAssignmentsAsync(It.IsAny<int>()))
            .ReturnsAsync(Array.Empty<LogisticsCardAssignment>());
        _cards.Setup(s => s.UpdateCardAsync(CardId, It.IsAny<string>(), It.IsAny<string>()))
            .Callback<int, string, string>((_, t, d) => _stored.UpdateContent(t, d)).Returns(Task.CompletedTask);
        _cards.Setup(s => s.SetCardDatesAsync(CardId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .Callback<int, DateTime?, DateTime?, DateTime?>((_, s, d, r) => _stored.SetDates(s, d, r)).Returns(Task.CompletedTask);
        _cards.Setup(s => s.SetCardLabelsAsync(CardId, It.IsAny<string?>()))
            .Callback<int, string?>((_, l) => _stored.SetLabels(l)).Returns(Task.CompletedTask);
        _cards.Setup(s => s.SetCardChecklistAsync(CardId, It.IsAny<string?>()))
            .Callback<int, string?>((_, c) => _stored.SetChecklist(c)).Returns(Task.CompletedTask);
        _cards.Setup(s => s.SetCardStatusAsync(CardId, It.IsAny<CardStatus>()))
            .Callback<int, CardStatus>((_, st) => _stored.SetStatus(st)).Returns(Task.CompletedTask);

        var environment = new Mock<IHostEnvironment>();
        environment.Setup(e => e.EnvironmentName).Returns("Test");
        Services.AddSingleton(environment.Object);

        SetupUserManager()
            .Setup(m => m.Users)
            .Returns(new List<ApplicationUser>().BuildMockDbSet().Object);

        SetupAuthentication("logistics-admin", "Logistics Admin", "Admin");
    }

    [Fact]
    public void EditedCard_ShowsEveryUpdatedField_WhenReopened()
    {
        var cut = Render<BoardPage>(p => p.Add(x => x.BoardId, BoardId));
        cut.Find(".kanban-card").Click();

        cut.Find("[aria-label='Marcar como Concluído']").Click();
        cut.Find("input[placeholder='Título do cartão']").Input("Levar colunas e cabos");
        cut.Find("[aria-label='Guardar título']").Click();
        var details = cut.Find("textarea[placeholder='Adicionar detalhes...']");
        details.Change("Pedir a carrinha");
        details.Blur();
        cut.FindAll("input[type='date']")[1].Change("2026-10-15");
        cut.Find("input[placeholder='Nome da etiqueta']").Change("Sonoplastia");
        cut.Find("[aria-label='Adicionar etiqueta']").Click();
        cut.Find("input[placeholder='Nova tarefa...']").Change("Afinar guitarras");
        cut.Find("[aria-label='Adicionar tarefa']").Click();

        _stored.Title.Should().Be("Levar colunas e cabos");
        _stored.Description.Should().Be("Pedir a carrinha", "saving details must not be overwritten by a stale card");
        _stored.DueDate.Should().Be(new DateTime(2026, 10, 15));
        _stored.Labels.Should().Contain("Sonoplastia");
        _stored.ChecklistJson.Should().Contain("Afinar guitarras");
        _stored.Status.Should().Be(CardStatus.Done);

        cut.Find("[aria-label='Voltar Atrás']").Click();
        cut.Find(".kanban-card").TextContent.Should().Contain("Levar colunas e cabos", "the board refreshes after edits");
        cut.Find(".kanban-card").Click();

        cut.Find("input[placeholder='Título do cartão']").GetAttribute("value").Should().Be("Levar colunas e cabos");
        cut.Find("textarea[placeholder='Adicionar detalhes...']").GetAttribute("value").Should().Be("Pedir a carrinha");
        cut.FindAll("input[type='date']")[1].GetAttribute("value").Should().Be("2026-10-15");
        cut.Markup.Should().Contain("Sonoplastia").And.Contain("Afinar guitarras");
    }

    [Fact]
    public void SavingTitle_AfterDetails_KeepsTheNewDetails()
    {
        var cut = Render<BoardPage>(p => p.Add(x => x.BoardId, BoardId));
        cut.Find(".kanban-card").Click();

        var details = cut.Find("textarea[placeholder='Adicionar detalhes...']");
        details.Change("Pedir a carrinha");
        details.Blur();
        cut.Find("input[placeholder='Título do cartão']").Input("Levar colunas e cabos");
        cut.Find("[aria-label='Guardar título']").Click();

        _cards.Verify(s => s.UpdateCardAsync(CardId, "Levar colunas e cabos", "Pedir a carrinha"), Times.Once);
        _stored.Description.Should().Be("Pedir a carrinha");
    }

    private static LogisticsCard Copy(LogisticsCard c) => new()
    {
        Id = c.Id, Title = c.Title, Description = c.Description, ListId = c.ListId, Position = c.Position,
        Status = c.Status, Labels = c.Labels, StartDate = c.StartDate, DueDate = c.DueDate,
        ChecklistJson = c.ChecklistJson, AttachmentsJson = c.AttachmentsJson
    };
}
