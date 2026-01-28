using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Pages.Management;
using RTUB.Web.Tests.Pages.Base;

namespace RTUB.Web.Tests.Pages.Management;

/// <summary>
/// Component tests for Questions.razor page (/questions).
/// Tests page rendering, loading state, empty state, and authorization per Phase 0.5.
/// </summary>
public class QuestionsPageTests : PageTestBase
{
    private readonly Mock<IQuestionService> _mockQuestionService;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;

    public QuestionsPageTests()
    {
        _mockQuestionService = SetupService<IQuestionService>();
        _mockUserManager = SetupUserManager();

        _mockQuestionService
            .Setup(x => x.GetAllWithRepliesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<string?>()))
            .ReturnsAsync(new List<Question>());
        _mockQuestionService
            .Setup(x => x.GetCountAsync(It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<string?>()))
            .ReturnsAsync(0);
        _mockQuestionService
            .Setup(x => x.GetAllOrgaoSocialMembersAsync())
            .ReturnsAsync(new List<(ApplicationUser Member, OrgaoSocialGroup Group, Position Position)>());

        _mockUserManager
            .Setup(x => x.FindByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);
    }

    #region Page Rendering Tests

    [Fact]
    public async Task QuestionsPage_RendersPageTitle()
    {
        SetupAuthentication("test-user", "Test User");

        var cut = RenderComponent<Questions>();
        cut.WaitForState(() => !cut.Markup.Contains("spinner-border") || cut.Markup.Contains("Perguntas") || cut.Markup.Contains("Nenhuma pergunta"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Perguntas", "page should display title");
        cut.Markup.Should().Contain("Órgãos Sociais", "page should display subtitle");
    }

    [Fact]
    public async Task QuestionsPage_ShowsLoadingState_Initially()
    {
        SetupAuthentication("test-user", "Test User");
        var tcs = new TaskCompletionSource<IEnumerable<Question>>();
        _mockQuestionService
            .Setup(x => x.GetAllWithRepliesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<string?>()))
            .Returns(tcs.Task);

        var cut = RenderComponent<Questions>();

        // Assert - Check loading state before async operations complete
        cut.Markup.Should().Contain("spinner-border", "should show loading initially");
        cut.Markup.Should().Contain("A carregar", "should show loading message");

        // Complete the delayed task to allow test cleanup
        tcs.SetResult(new List<Question>());
        cut.WaitForState(() => cut.Markup.Contains("Nenhuma pergunta") || cut.Markup.Contains("Pesquisar"), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task QuestionsPage_ShowsEmptyState_WhenNoOpenQuestions()
    {
        SetupAuthentication("test-user", "Test User");

        var cut = RenderComponent<Questions>();
        cut.WaitForState(() => cut.Markup.Contains("Nenhuma pergunta") || cut.Markup.Contains("Pesquisar perguntas"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Nenhuma pergunta aberta encontrada", "should show empty state when no open questions");
    }

    [Fact]
    public async Task QuestionsPage_ShowsSearchBar_WhenLoaded()
    {
        SetupAuthentication("test-user", "Test User");

        var cut = RenderComponent<Questions>();
        cut.WaitForState(() => cut.Markup.Contains("Pesquisar perguntas") || cut.Markup.Contains("Nenhuma pergunta"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Pesquisar perguntas", "page should show search bar");
    }

    #endregion
}
