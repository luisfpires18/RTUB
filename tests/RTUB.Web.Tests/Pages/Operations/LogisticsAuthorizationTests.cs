using System.Security.Claims;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using MockQueryable.Moq;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Web.Tests.Pages.Base;
using BoardEntity = RTUB.Core.Entities.LogisticsBoard;
using BoardPage = RTUB.Pages.Management.LogisticsBoard;
using BoardsPage = RTUB.Pages.Management.Logistics;

namespace RTUB.Web.Tests.Pages.Operations;

/// <summary>
/// Logistics authorization, rendered for real: Admin and Mod manage boards, lists and cards;
/// every other role only reads and creates reminders. The member cases assert the service was
/// never called, not merely that a button is hidden, and include the JS-invokable drag handler
/// a member could call from the browser console.
/// </summary>
public class LogisticsAuthorizationTests : PageTestBase
{
    private const int BoardId = 7;
    private const int ListId = 3;
    private const int CardId = 11;

    public static TheoryData<string[]> Managers => new() { new[] { "Admin" }, new[] { "Mod" } };

    public static TheoryData<string[]> NonManagers => new()
    {
        new[] { "Member" },
        new[] { "Owner" },
        Array.Empty<string>()
    };

    private readonly Mock<ILogisticsBoardService> _boards;
    private readonly Mock<ILogisticsListService> _lists;
    private readonly Mock<ILogisticsCardService> _cards;

    public LogisticsAuthorizationTests()
    {
        _boards = SetupService<ILogisticsBoardService>();
        _lists = SetupService<ILogisticsListService>();
        _cards = SetupService<ILogisticsCardService>();

        SetupService<IEventService>()
            .Setup(s => s.GetAllEventsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Event>());
        _cards.Setup(s => s.GetCardAttachmentsAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new List<DocumentMetadata>());
        _cards.Setup(s => s.GetCardAssignmentsAsync(It.IsAny<int>()))
            .ReturnsAsync(Array.Empty<LogisticsCardAssignment>());

        var environment = new Mock<IHostEnvironment>();
        environment.Setup(e => e.EnvironmentName).Returns("Test");
        Services.AddSingleton(environment.Object);

        SetupUserManager()
            .Setup(m => m.Users)
            .Returns(new List<ApplicationUser>().BuildMockDbSet().Object);
    }

    // ---------- the rule ----------

    [Theory]
    [InlineData(true, "Admin")]
    [InlineData(true, "Mod")]
    [InlineData(true, "Admin", "Mod")]
    [InlineData(false, "Member")]
    [InlineData(false, "Owner")]
    [InlineData(false)]
    public void CanManage_IsAdminOrModOnly(bool expected, params string[] roles)
    {
        var identity = new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)), "test");

        LogisticsAuthorization.CanManage(new ClaimsPrincipal(identity)).Should().Be(expected);
    }

    // ---------- board page: /logistics/{id} ----------

    [Theory]
    [MemberData(nameof(Managers))]
    public void Board_Manager_SeesEveryManagementControl(string[] roles)
    {
        SignIn(roles);
        GivenBoard(ListWithOneCard());

        var cut = RenderBoard();

        cut.FindAll("button").Should().Contain(b => b.TextContent.Contains("Adicionar Lista"));
        cut.FindAll("[title='Adicionar Cartão']").Should().ContainSingle();
        cut.FindAll(".kanban-list-header [title='Editar']").Should().ContainSingle();
        cut.FindAll(".kanban-list-header [title='Eliminar']").Should().ContainSingle();
        MobileNavLabels(cut).Should().Contain("Adicionar");

        var card = cut.Find(".kanban-card");
        card.GetAttribute("draggable").Should().Be("true");
        card.ClassList.Should().Contain("clickable");
        DragAndDropInitialized().Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(NonManagers))]
    public void Board_NonManager_SeesNoManagementControl_ButKeepsReminders(string[] roles)
    {
        SignIn(roles);
        GivenBoard(ListWithOneCard());

        var cut = RenderBoard();

        cut.FindAll("button").Should().NotContain(b => b.TextContent.Contains("Adicionar Lista"));
        cut.FindAll("[title='Adicionar Cartão']").Should().BeEmpty();
        cut.FindAll(".kanban-list-header [title='Editar']").Should().BeEmpty();
        cut.FindAll(".kanban-list-header [title='Eliminar']").Should().BeEmpty();
        MobileNavLabels(cut).Should().NotContain("Adicionar");

        var card = cut.Find(".kanban-card");
        card.GetAttribute("draggable").Should().Be("false");
        card.ClassList.Should().NotContain("clickable");
        DragAndDropInitialized().Should().Be(0,
            "a member must never receive the .NET reference the drag handler is called through");

        cut.FindAll("button").Should().Contain(b => b.TextContent.Contains("Criar Lembrete"),
            "reminders are open to every member and are not Logistics management");
    }

    [Theory]
    [MemberData(nameof(Managers))]
    public void Board_Manager_CanCreateTheFirstListOnAnEmptyBoard(string[] roles)
    {
        SignIn(roles);
        GivenBoard();

        var cut = RenderBoard();
        cut.FindAll("button").Single(b => b.TextContent.Contains("Criar Lista")).Click();
        cut.Find("input[placeholder='Nome da lista']").Change("Montagem");
        cut.FindAll("button").Single(b => b.TextContent.Contains("Guardar")).Click();

        _lists.Verify(s => s.CreateListAsync("Montagem", BoardId, 0), Times.Once);
    }

    [Theory]
    [MemberData(nameof(NonManagers))]
    public void Board_NonManager_GetsNoCreateActionOnAnEmptyBoard(string[] roles)
    {
        SignIn(roles);
        GivenBoard();

        var cut = RenderBoard();

        cut.FindAll("button").Should().NotContain(b => b.TextContent.Contains("Criar Lista"));
        cut.Find(".empty-state-card").Click();
        cut.FindAll("input[placeholder='Nome da lista']").Should().BeEmpty("the create-list modal must not open");
        _lists.VerifyNoOtherCalls();
    }

    [Theory]
    [MemberData(nameof(Managers))]
    public void Board_Manager_CanOpenACardEditItAndSeeDelete(string[] roles)
    {
        SignIn(roles);
        GivenBoard(ListWithOneCard());

        var cut = RenderBoard();
        cut.Find(".kanban-card").Click();

        cut.FindAll("[aria-label='Eliminar cartão']").Should().ContainSingle();
        cut.Find("[aria-label='Marcar como Concluído']").Click();

        _cards.Verify(s => s.SetCardStatusAsync(CardId, CardStatus.Done), Times.Once);
    }

    [Theory]
    [MemberData(nameof(NonManagers))]
    public void Board_NonManager_CannotOpenACard(string[] roles)
    {
        SignIn(roles);
        GivenBoard(ListWithOneCard());

        var cut = RenderBoard();
        cut.Find(".kanban-card").Click();

        cut.FindAll("[aria-label='Eliminar cartão']").Should().BeEmpty();
        cut.FindAll("[aria-label='Marcar como Concluído']").Should().BeEmpty();
        _cards.Verify(s => s.GetCardAssignmentsAsync(It.IsAny<int>()), Times.Never,
            "the card details modal must not even load");
    }

    [Theory]
    [MemberData(nameof(Managers))]
    public async Task Board_Manager_CanMoveACard(string[] roles)
    {
        SignIn(roles);
        GivenBoard(ListWithOneCard());
        var cut = RenderBoard();

        await cut.InvokeAsync(() => cut.Instance.OnCardMoved(CardId, ListId + 1, 0));

        _cards.Verify(s => s.MoveCardAsync(CardId, ListId + 1, 0), Times.Once);
    }

    [Theory]
    [MemberData(nameof(NonManagers))]
    public async Task Board_NonManager_CannotMoveACard_EvenThroughTheJsEntryPoint(string[] roles)
    {
        SignIn(roles);
        GivenBoard(ListWithOneCard());
        var cut = RenderBoard();

        var move = () => cut.InvokeAsync(() => cut.Instance.OnCardMoved(CardId, ListId + 1, 0));

        await move.Should().ThrowAsync<UnauthorizedAccessException>();
        _cards.Verify(s => s.MoveCardAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    // ---------- board list page: /logistics ----------

    [Theory]
    [MemberData(nameof(Managers))]
    public void Boards_Manager_SeesBoardManagement(string[] roles)
    {
        SignIn(roles);
        GivenActiveBoards(new BoardEntity { Id = BoardId, Name = "Arraial" });

        var cut = Render<BoardsPage>();

        cut.FindAll("button").Should().Contain(b => b.TextContent.Contains("Criar Quadro"));
        cut.FindAll(".logistics-board-card [title='Editar']").Should().ContainSingle();
        cut.FindAll(".logistics-board-card [title='Eliminar']").Should().ContainSingle();
        cut.FindAll(".logistics-board-card [title='Concluir Quadro']").Should().ContainSingle();
    }

    [Theory]
    [MemberData(nameof(NonManagers))]
    public void Boards_NonManager_SeesNoBoardManagement(string[] roles)
    {
        SignIn(roles);
        GivenActiveBoards(new BoardEntity { Id = BoardId, Name = "Arraial" });

        var cut = Render<BoardsPage>();

        cut.FindAll("button").Should().NotContain(b => b.TextContent.Contains("Criar Quadro"));
        cut.FindAll(".logistics-board-card [title='Editar']").Should().BeEmpty();
        cut.FindAll(".logistics-board-card [title='Eliminar']").Should().BeEmpty();
        cut.FindAll(".logistics-board-card [title='Concluir Quadro']").Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(Managers))]
    public void Boards_Manager_CanCreateTheFirstBoard(string[] roles)
    {
        SignIn(roles);
        GivenActiveBoards();

        var cut = Render<BoardsPage>();
        cut.FindAll(".empty-state-card button").Single(b => b.TextContent.Contains("Criar Quadro")).Click();
        cut.Find("input[placeholder='Nome do quadro']").Change("Arraial");
        cut.FindAll("button").Single(b => b.TextContent.Contains("Guardar")).Click();

        _boards.Verify(s => s.CreateBoardAsync("Arraial", It.IsAny<string>()), Times.Once);
    }

    [Theory]
    [MemberData(nameof(NonManagers))]
    public void Boards_NonManager_GetsNoCreateActionWhenThereAreNoBoards(string[] roles)
    {
        SignIn(roles);
        GivenActiveBoards();

        var cut = Render<BoardsPage>();

        cut.FindAll("button").Should().NotContain(b => b.TextContent.Contains("Criar Quadro"));
        cut.Find(".empty-state-card").Click();
        cut.FindAll("input[placeholder='Nome do quadro']").Should().BeEmpty("the create-board modal must not open");
        _boards.Verify(s => s.CreateBoardAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ---------- helpers ----------

    private void SignIn(string[] roles) => SetupAuthentication("logistics-user", "Logistics User", roles);

    private IRenderedComponent<BoardPage> RenderBoard() =>
        Render<BoardPage>(p => p.Add(x => x.BoardId, BoardId));

    private static LogisticsList ListWithOneCard() => new()
    {
        Id = ListId,
        Name = "Material",
        BoardId = BoardId,
        Position = 0,
        Cards = new List<LogisticsCard> { new() { Id = CardId, Title = "Levar colunas", ListId = ListId } }
    };

    private void GivenBoard(params LogisticsList[] lists) =>
        _boards.Setup(s => s.GetBoardWithListsAndCardsAsync(BoardId))
            .ReturnsAsync(new BoardEntity { Id = BoardId, Name = "Arraial", Lists = lists.ToList() });

    private void GivenActiveBoards(params BoardEntity[] active)
    {
        _boards.Setup(s => s.GetBoardsPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), false))
            .ReturnsAsync((active.AsEnumerable(), active.Length));
        _boards.Setup(s => s.GetBoardsPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), true))
            .ReturnsAsync((Enumerable.Empty<BoardEntity>(), 0));
    }

    // Pages get PageTestBase's IJSRuntime mock, so interop is asserted on that mock.
    private int DragAndDropInitialized() =>
        MockJSRuntime.Invocations.Count(i =>
            i.Method.Name == nameof(IJSRuntime.InvokeAsync) &&
            i.Method.GetGenericArguments().SingleOrDefault() == typeof(IJSVoidResult) &&
            (string)i.Arguments[0] == "initializeKanbanDragDrop");

    private static IEnumerable<string> MobileNavLabels<T>(IRenderedComponent<T> cut) where T : Microsoft.AspNetCore.Components.IComponent =>
        cut.FindAll(".mobile-bottom-nav__btn").Select(b => b.TextContent.Trim());
}
