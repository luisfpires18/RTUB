using Bunit;
using FluentAssertions;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Pages.Activities;
using RTUB.Web.Tests.Pages.Base;

namespace RTUB.Web.Tests.Pages.Activities;

/// <summary>
/// Component tests for NaipesConfig.razor page
/// Tests page rendering, loading state, and naipe configuration display
/// </summary>
public class NaipesConfigPageTests : PageTestBase
{
    private readonly Mock<INaipeService> _mockNaipeService;

    public NaipesConfigPageTests()
    {
        // Setup service mocks
        _mockNaipeService = SetupService<INaipeService>();

        // Setup default service responses
        _mockNaipeService
            .Setup(x => x.GetAllTypeConfigsAsync())
            .ReturnsAsync(new List<RTUB.Application.DTOs.NaipeTypeConfigDto>());

        _mockNaipeService
            .Setup(x => x.InitializeTypeConfigsAsync())
            .Returns(Task.CompletedTask);

        SetupAuthenticationAsAdmin("admin-user");
    }

    #region Page Rendering Tests

    [Fact]
    public async Task NaipesConfigPage_RendersPageTitle()
    {
        // Arrange & Act
        var cut = RenderComponent<NaipesConfig>();
        cut.WaitForState(() => cut.Markup.Contains("Configuração de Naipes") || cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Configuração de Naipes", "page should display 'Configuração de Naipes' title");
    }

    [Fact]
    public async Task NaipesConfigPage_ShowsLoadingState_Initially()
    {
        // Arrange - Setup slow service to test loading state
        var tcs = new TaskCompletionSource<List<RTUB.Application.DTOs.NaipeTypeConfigDto>>();
        _mockNaipeService
            .Setup(x => x.GetAllTypeConfigsAsync())
            .Returns(tcs.Task);

        // Act
        var cut = RenderComponent<NaipesConfig>();

        // Assert - Check loading state before async operations complete
        cut.Markup.Should().Contain("A carregar", "page should show loading state initially");

        // Complete the delayed task to allow test cleanup
        tcs.SetResult(new List<RTUB.Application.DTOs.NaipeTypeConfigDto>());
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task NaipesConfigPage_DisplaysBackButton()
    {
        // Arrange & Act
        var cut = RenderComponent<NaipesConfig>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Voltar aos Naipes", "page should display back button");
    }

    [Fact]
    public async Task NaipesConfigPage_DisplaysInfoAlert()
    {
        // Arrange & Act
        var cut = RenderComponent<NaipesConfig>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Dica", "page should display info alert");
    }

    #endregion
}
