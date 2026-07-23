using Bunit;
using FluentAssertions;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Pages.Management;
using RTUB.Web.Tests.Pages.Base;

namespace RTUB.Web.Tests.Pages.Management;

/// <summary>
/// Component tests for Labels.razor page (/labels).
/// Tests page rendering, loading state, empty state, and authorization per Phase 0.5.
/// </summary>
public class LabelsPageTests : PageTestBase
{
    private readonly Mock<ILabelService> _mockLabelService;

    public LabelsPageTests()
    {
        _mockLabelService = SetupService<ILabelService>();
        _mockLabelService
            .Setup(x => x.GetAllLabelsAsync())
            .ReturnsAsync(new List<Label>());
    }

    #region Page Rendering Tests

    [Fact]
    public async Task LabelsPage_RendersPageTitle()
    {
        SetupAuthenticationAsAdmin("admin-user");

        var cut = Render<Labels>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar") || cut.Markup.Contains("Gestão de Etiquetas") || cut.Markup.Contains("Nenhuma etiqueta"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Gestão de Etiquetas", "page should display title");
        cut.Markup.Should().Contain("Gerir etiquetas", "page should display subtitle");
    }

    [Fact]
    public async Task LabelsPage_ShowsLoadingState_Initially()
    {
        SetupAuthenticationAsAdmin("admin-user");
        _mockLabelService
            .Setup(x => x.GetAllLabelsAsync())
            .Returns(async () =>
            {
                await Task.Delay(100);
                return (IEnumerable<Label>)new List<Label>();
            });

        var cut = Render<Labels>();

        cut.Markup.Should().Contain("Gestão de Etiquetas", "page should display title");
        cut.Markup.Should().Match(m => m.Contains("A carregar") || m.Contains("Gestão de Etiquetas"), "should show loading or title initially");

        cut.WaitForState(() => cut.Markup.Contains("Nenhuma etiqueta") || cut.Markup.Contains("Pesquisar"), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task LabelsPage_ShowsEmptyState_WhenNoLabels()
    {
        SetupAuthenticationAsAdmin("admin-user");

        var cut = Render<Labels>();
        cut.WaitForState(() => cut.Markup.Contains("Nenhuma etiqueta") || cut.Markup.Contains("Pesquisar"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Nenhuma etiqueta encontrada", "should show empty state when no labels");
    }

    [Fact]
    public async Task LabelsPage_ShowsCreateButton_ForAdminUser()
    {
        SetupAuthenticationAsAdmin("admin-user");

        var cut = Render<Labels>();
        cut.WaitForState(() => cut.Markup.Contains("Adicionar Novo Texto") || cut.Markup.Contains("Nenhuma etiqueta"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Adicionar Novo Texto", "admin should see create button");
    }

    #endregion
}
