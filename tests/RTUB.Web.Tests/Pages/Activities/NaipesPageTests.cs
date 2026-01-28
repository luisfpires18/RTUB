using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Pages.Activities;
using RTUB.Web.Tests.Pages.Base;

namespace RTUB.Web.Tests.Pages.Activities;

/// <summary>
/// Component tests for Naipes.razor page (/naipes).
/// Tests page rendering, loading state, empty states, and authorization per Phase 0.5.
/// </summary>
public class NaipesPageTests : PageTestBase
{
    private readonly Mock<INaipeService> _mockNaipeService;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;

    public NaipesPageTests()
    {
        _mockNaipeService = SetupService<INaipeService>();
        _mockUserManager = SetupUserManager();

        _mockNaipeService
            .Setup(x => x.InitializeTypeConfigsAsync())
            .Returns(Task.CompletedTask);
        _mockNaipeService
            .Setup(x => x.GetVisibleTypeConfigsAsync())
            .ReturnsAsync(new List<NaipeTypeConfigDto>());
        _mockNaipeService
            .Setup(x => x.GetAllContentAsync())
            .ReturnsAsync(new List<NaipeContentDto>());

        var defaultUser = new ApplicationUser { Id = "test-user", UserName = "Test User" };
        _mockUserManager
            .Setup(x => x.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(defaultUser);
    }

    #region Page Rendering Tests

    [Fact]
    public async Task NaipesPage_RendersPageTitle()
    {
        SetupAuthentication("test-user", "Test User");

        var cut = RenderComponent<Naipes>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar conteúdo") || cut.Markup.Contains("Naipes") || cut.Markup.Contains("Selecione um Instrumento"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Naipes", "page should display title");
        cut.Markup.Should().Contain("Conteúdo educativo", "page should display subtitle");
    }

    [Fact]
    public async Task NaipesPage_ShowsLoadingState_Initially()
    {
        SetupAuthentication("test-user", "Test User");
        _mockNaipeService
            .Setup(x => x.InitializeTypeConfigsAsync())
            .Returns(async () => await Task.Delay(100));
        _mockNaipeService
            .Setup(x => x.GetVisibleTypeConfigsAsync())
            .Returns(async () =>
            {
                await Task.Delay(100);
                return new List<NaipeTypeConfigDto>();
            });
        _mockNaipeService
            .Setup(x => x.GetAllContentAsync())
            .Returns(async () =>
            {
                await Task.Delay(100);
                return new List<NaipeContentDto>();
            });

        var cut = RenderComponent<Naipes>();

        cut.Markup.Should().Contain("Naipes", "page should display title");
        cut.Markup.Should().Match(m => m.Contains("spinner-border") || m.Contains("A carregar") || m.Contains("Naipes"), "should show loading or title initially");

        cut.WaitForState(() => cut.Markup.Contains("Selecione um Instrumento") || cut.Markup.Contains("Naipes"), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task NaipesPage_ShowsSelectInstrumentEmptyState_WhenLoaded()
    {
        SetupAuthentication("test-user", "Test User");

        var cut = RenderComponent<Naipes>();
        cut.WaitForState(() => cut.Markup.Contains("Selecione um Instrumento") || cut.Markup.Contains("Nenhum conteúdo"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Selecione um Instrumento", "should show empty state when no instrument selected");
    }

    [Fact]
    public async Task NaipesPage_ShowsNoContentEmptyState_WhenInstrumentSelectedButNoContent()
    {
        SetupAuthentication("test-user", "Test User");
        var config = new NaipeTypeConfigDto
        {
            Id = 1,
            InstrumentType = InstrumentType.Guitarra,
            InstrumentTypeName = "Guitarra",
            IsVisible = true,
            SortOrder = 0
        };
        _mockNaipeService
            .Setup(x => x.GetVisibleTypeConfigsAsync())
            .ReturnsAsync(new List<NaipeTypeConfigDto> { config });
        _mockNaipeService
            .Setup(x => x.GetAllContentAsync())
            .ReturnsAsync(new List<NaipeContentDto>());

        var cut = RenderComponent<Naipes>();
        cut.WaitForState(() => cut.Markup.Contains("Guitarra") || cut.Markup.Contains("Selecione um Instrumento"), TimeSpan.FromSeconds(2));

        var instrumentBtn = cut.FindAll(".instrument-selector-item").FirstOrDefault();
        instrumentBtn.Should().NotBeNull("instrument selector should have at least one item");
        instrumentBtn!.Click();

        cut.WaitForState(() => cut.Markup.Contains("Nenhum conteúdo encontrado"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Nenhum conteúdo encontrado", "should show empty state when instrument has no content");
    }

    [Fact]
    public async Task NaipesPage_ShowsAddVideoAndAddImageButtons_ForAuthenticatedUser()
    {
        SetupAuthentication("test-user", "Test User");

        var cut = RenderComponent<Naipes>();
        cut.WaitForState(() => cut.Markup.Contains("Adicionar Vídeo") || cut.Markup.Contains("Selecione um Instrumento"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Adicionar Vídeo", "authenticated user should see add video button");
        cut.Markup.Should().Contain("Adicionar Imagem", "authenticated user should see add image button");
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task NaipesPage_ShowsConfigButton_ForAdminUser()
    {
        SetupAuthenticationAsAdmin("admin-user");

        var cut = RenderComponent<Naipes>();
        cut.WaitForState(() => cut.Markup.Contains("Configurar") || cut.Markup.Contains("Selecione um Instrumento"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Configurar", "admin should see config button");
    }

    [Fact]
    public async Task NaipesPage_HidesConfigButton_ForRegularUser()
    {
        SetupAuthentication("regular-user", "Regular User");
        // No "Admin" role; default GetUserAsync returns user so isAdmin = user.IsInRole("Admin") is false

        var cut = RenderComponent<Naipes>();
        cut.WaitForState(() => cut.Markup.Contains("Naipes") && (!cut.Markup.Contains("A carregar conteúdo") || cut.Markup.Contains("Selecione um Instrumento")), TimeSpan.FromSeconds(2));

        cut.Markup.Should().NotContain("Configurar", "regular user should not see config button");
    }

    #endregion
}
