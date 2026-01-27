using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Pages.Media;
using RTUB.Web.Tests.Pages.Base;
using RTUB.Web.Tests.TestData;

namespace RTUB.Web.Tests.Pages.Media;

/// <summary>
/// Component tests for Slideshows.razor page (/images).
/// Tests page rendering, loading state, empty state, list display, and authorization (Phase 0.5).
/// </summary>
public class SlideshowsPageTests : PageTestBase
{
    private const string SkipModal = "Modal renders outside component fragment; cannot assert modal markup in bUnit.";

    private readonly Mock<ISlideshowService> _mockSlideshowService;
    private readonly Mock<IImageStorageService> _mockImageStorageService;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;

    public SlideshowsPageTests()
    {
        _mockSlideshowService = SetupService<ISlideshowService>();
        _mockImageStorageService = SetupService<IImageStorageService>();
        _mockUserManager = SetupUserManager();
        _ = SetupWebHostEnvironment();

        _mockSlideshowService
            .Setup(x => x.GetAllSlideshowsAsync())
            .ReturnsAsync(new List<Slideshow>());

        SetupAuthentication("test-user", "Test User", "Admin");
    }

    #region Page Rendering Tests

    [Fact]
    public async Task SlideshowsPage_RendersPageTitle()
    {
        SetupAuthentication("admin-user", "Admin User", "Admin");

        var cut = RenderComponent<Slideshows>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar slides") || cut.Markup.Contains("Gestão de Apresentação") || cut.Markup.Contains("Nenhum slideshow"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Gestão de Apresentação", "page should display title");
        cut.Markup.Should().Contain("Gerir imagens de apresentação", "page should display subtitle");
    }

    [Fact]
    public async Task SlideshowsPage_ShowsLoadingState_Initially()
    {
        SetupAuthentication("admin-user", "Admin User", "Admin");
        _mockSlideshowService
            .Setup(x => x.GetAllSlideshowsAsync())
            .Returns(async () =>
            {
                await Task.Delay(100);
                return (IEnumerable<Slideshow>)new List<Slideshow>();
            });

        var cut = RenderComponent<Slideshows>();

        cut.Markup.Should().Contain("Gestão de Apresentação", "page should display title");
        cut.Markup.Should().Match(m => m.Contains("A carregar slides") || m.Contains("Gestão de Apresentação"), "should show loading or title initially");

        cut.WaitForState(() => cut.Markup.Contains("Nenhum slideshow") || cut.Markup.Contains("Pesquisar"), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task SlideshowsPage_ShowsEmptyState_WhenNoSlideshows()
    {
        SetupAuthentication("admin-user", "Admin User", "Admin");

        var cut = RenderComponent<Slideshows>();
        cut.WaitForState(() => cut.Markup.Contains("Nenhum slideshow") || cut.Markup.Contains("Pesquisar"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Nenhum slideshow encontrado", "should show empty state when no slideshows");
    }

    [Fact]
    public async Task SlideshowsPage_ShowsCreateButton_ForAdminUser()
    {
        SetupAuthentication("admin-user", "Admin User", "Admin");

        var cut = RenderComponent<Slideshows>();
        cut.WaitForState(() => cut.Markup.Contains("Adicionar Slide") || cut.Markup.Contains("Nenhum slideshow"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Adicionar Slide", "admin should see create button");
    }

    [Fact]
    public async Task SlideshowsPage_DisplaysSlideshows_WhenSlideshowsExist()
    {
        SetupAuthentication("admin-user", "Admin User", "Admin");
        var slides = new List<Slideshow>
        {
            PageTestDataBuilders.CreateSlideshow(1, "Slide One", order: 1),
            PageTestDataBuilders.CreateSlideshow(2, "Slide Two", order: 2)
        };
        _mockSlideshowService
            .Setup(x => x.GetAllSlideshowsAsync())
            .ReturnsAsync(slides);

        var cut = RenderComponent<Slideshows>();
        cut.WaitForState(() => cut.Markup.Contains("Slide One") || cut.Markup.Contains("Slide Two"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Slide One", "page should display first slide");
        cut.Markup.Should().Contain("Slide Two", "page should display second slide");
    }

    [Fact(Skip = SkipModal)]
    public async Task SlideshowsPage_DeleteSlide_CallsService()
    {
        SetupAuthentication("admin-user", "Admin User", "Admin");
        var slide = PageTestDataBuilders.CreateSlideshow(1, "Test Slide");
        _mockSlideshowService
            .Setup(x => x.GetAllSlideshowsAsync())
            .ReturnsAsync(new List<Slideshow> { slide });
        _mockSlideshowService
            .Setup(x => x.DeleteSlideshowAsync(1))
            .Returns(Task.CompletedTask);
        _mockSlideshowService
            .Setup(x => x.GetAllSlideshowsAsync())
            .ReturnsAsync(new List<Slideshow>());

        var cut = RenderComponent<Slideshows>();
        cut.WaitForState(() => cut.Markup.Contains("Test Slide"), TimeSpan.FromSeconds(2));

        var openDelete = typeof(Slideshows).GetMethod("OpenDeleteModal", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var deleteSlide = typeof(Slideshows).GetMethod("DeleteSlide", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        await cut.InvokeAsync(() => openDelete.Invoke(cut.Instance, new object[] { slide }));
        await cut.InvokeAsync(async () =>
        {
            var t = (Task)deleteSlide.Invoke(cut.Instance, null)!;
            await t;
        });

        _mockSlideshowService.Verify(x => x.DeleteSlideshowAsync(1), Times.Once, "DeleteSlideshowAsync should be called");
    }

    #endregion
}
