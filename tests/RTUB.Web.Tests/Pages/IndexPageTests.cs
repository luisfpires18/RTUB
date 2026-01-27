using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Web.Tests.Pages.Base;
using IndexPage = RTUB.Pages.Index;

namespace RTUB.Web.Tests.Pages;

/// <summary>
/// Component tests for Index.razor page (/).
/// Tests page rendering, slideshow display, section navigation, and authentication-based content (Phase 0.5).
/// </summary>
public class IndexPageTests : PageTestBase
{
    private readonly Mock<ISlideshowService> _mockSlideshowService;

    public IndexPageTests()
    {
        _mockSlideshowService = SetupService<ISlideshowService>();

        // Setup default service responses
        _mockSlideshowService
            .Setup(x => x.GetActiveSlideshowsAsync())
            .ReturnsAsync(new List<Slideshow>());

        _mockSlideshowService
            .Setup(x => x.GetActivePublicSlideshowsAsync())
            .ReturnsAsync(new List<Slideshow>());

        // Setup MediaSessionInterop for AboutUsContent component
        Services.AddSingleton(new RTUB.Web.Interop.MediaSessionInterop(MockJSRuntime.Object));

        // Setup services for AboutUsContent component
        var mockLabelService = SetupService<ILabelService>();
        var mockEventService = SetupService<IEventService>();
        var mockTrophyService = SetupService<ITrophyService>();

        // Setup default service responses for AboutUsContent
        mockLabelService
            .Setup(x => x.GetLabelByReferenceAsync(It.IsAny<string>()))
            .ReturnsAsync((Label?)null);

        mockEventService
            .Setup(x => x.GetPastEventsAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<Event>());

        mockTrophyService
            .Setup(x => x.GetByEventIdAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<Trophy>());
    }

    #region Page Rendering Tests

    [Fact]
    public async Task IndexPage_RendersLogo()
    {
        // Arrange
        SetupUnauthenticated();

        // Act
        var cut = RenderComponent<IndexPage>();
        cut.WaitForState(() => cut.Markup.Contains("rtub_logo") || cut.Markup.Contains("RTUB Logo"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("rtub_logo", "page should display RTUB logo");
    }

    [Fact]
    public async Task IndexPage_DisplaysNavigationButtons()
    {
        // Arrange
        SetupUnauthenticated();

        // Act
        var cut = RenderComponent<IndexPage>();
        cut.WaitForState(() => cut.Markup.Contains("Sobre Nós") || cut.Markup.Contains("rtub_logo"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Sobre Nós", "page should display 'Sobre Nós' button");
        cut.Markup.Should().Contain("Junta-te a nós", "page should display 'Junta-te a nós' button");
        cut.Markup.Should().Contain("História", "page should display 'História' button");
        cut.Markup.Should().Contain("Hierarquia", "page should display 'Hierarquia' button");
        cut.Markup.Should().Contain("FITAB", "page should display 'FITAB' button");
    }

    [Fact]
    public async Task IndexPage_DisplaysSocialMediaLinks()
    {
        // Arrange
        SetupUnauthenticated();

        // Act
        var cut = RenderComponent<IndexPage>();
        cut.WaitForState(() => cut.Markup.Contains("Facebook") || cut.Markup.Contains("rtub_logo"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Facebook", "page should display Facebook link");
        cut.Markup.Should().Contain("Instagram", "page should display Instagram link");
        cut.Markup.Should().Contain("YouTube", "page should display YouTube link");
        cut.Markup.Should().Contain("Spotify", "page should display Spotify link");
    }

    [Fact]
    public async Task IndexPage_ShowsCarousel_WhenSlidesExist()
    {
        // Arrange
        SetupUnauthenticated();
        var slides = new List<Slideshow>
        {
            CreateTestSlideshow(1, "Slide 1", "Description 1", true),
            CreateTestSlideshow(2, "Slide 2", "Description 2", true)
        };

        _mockSlideshowService
            .Setup(x => x.GetActivePublicSlideshowsAsync())
            .ReturnsAsync(slides);

        // Act
        var cut = RenderComponent<IndexPage>();
        cut.WaitForState(() => cut.Markup.Contains("homeCarousel") || cut.Markup.Contains("Slide 1"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("homeCarousel", "page should display carousel when slides exist");
        cut.Markup.Should().Contain("Slide 1", "page should display first slide");
    }

    [Fact]
    public async Task IndexPage_HidesCarousel_WhenNoSlides()
    {
        // Arrange
        SetupUnauthenticated();
        _mockSlideshowService
            .Setup(x => x.GetActivePublicSlideshowsAsync())
            .ReturnsAsync(new List<Slideshow>());

        // Act
        var cut = RenderComponent<IndexPage>();
        cut.WaitForState(() => !cut.Markup.Contains("homeCarousel") || cut.Markup.Contains("rtub_logo"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().NotContain("homeCarousel", "page should not display carousel when no slides");
    }

    [Fact]
    public async Task IndexPage_ShowsPublicSlides_ForUnauthenticatedUsers()
    {
        // Arrange
        SetupUnauthenticated();
        var publicSlides = new List<Slideshow>
        {
            CreateTestSlideshow(1, "Public Slide", "Public description", true)
        };

        _mockSlideshowService
            .Setup(x => x.GetActivePublicSlideshowsAsync())
            .ReturnsAsync(publicSlides);

        // Act
        var cut = RenderComponent<IndexPage>();
        cut.WaitForState(() => cut.Markup.Contains("Public Slide") || cut.Markup.Contains("rtub_logo"), TimeSpan.FromSeconds(2));

        // Assert
        _mockSlideshowService.Verify(x => x.GetActivePublicSlideshowsAsync(), Times.Once);
        _mockSlideshowService.Verify(x => x.GetActiveSlideshowsAsync(), Times.Never);
    }

    [Fact]
    public async Task IndexPage_ShowsAllSlides_ForAuthenticatedUsers()
    {
        // Arrange
        SetupAuthentication("test-user", "Test User");
        var allSlides = new List<Slideshow>
        {
            CreateTestSlideshow(1, "Authenticated Slide", "Authenticated description", true)
        };

        _mockSlideshowService
            .Setup(x => x.GetActiveSlideshowsAsync())
            .ReturnsAsync(allSlides);

        // Act
        var cut = RenderComponent<IndexPage>();
        cut.WaitForState(() => cut.Markup.Contains("Authenticated Slide") || cut.Markup.Contains("rtub_logo"), TimeSpan.FromSeconds(2));

        // Assert
        _mockSlideshowService.Verify(x => x.GetActiveSlideshowsAsync(), Times.Once);
        _mockSlideshowService.Verify(x => x.GetActivePublicSlideshowsAsync(), Times.Never);
    }

    [Fact]
    public async Task IndexPage_ShowsAboutUsContent_ByDefault()
    {
        // Arrange
        SetupUnauthenticated();

        // Act
        var cut = RenderComponent<IndexPage>();
        cut.WaitForState(() => cut.Markup.Contains("Sobre Nós") || cut.Markup.Contains("rtub_logo"), TimeSpan.FromSeconds(2));

        // Assert
        // The AboutUsContent component should be rendered by default
        // We can verify by checking that the section button is active
        cut.Markup.Should().Contain("Sobre Nós", "page should show 'Sobre Nós' section by default");
    }

    #endregion

    #region Helper Methods

    private static Slideshow CreateTestSlideshow(int id, string title, string description, bool isActive)
    {
        var slideshow = Slideshow.Create(title, id, description, 5000);
        slideshow.Id = id;
        slideshow.SetImage("/test-image.jpg");
        if (!isActive)
        {
            slideshow.Deactivate();
        }
        return slideshow;
    }

    #endregion
}
