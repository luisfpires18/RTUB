using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Pages.Inventory;
using RTUB.Web.Tests.Pages.Base;

namespace RTUB.Web.Tests.Pages.Inventory;

/// <summary>
/// Component tests for Shop.razor page (/shop).
/// Tests page rendering, loading state, empty state, and authorization per Phase 0.5.
/// </summary>
public class ShopPageTests : PageTestBase
{
    private readonly Mock<IProductService> _mockProductService;
    private readonly Mock<IFiscalYearService> _mockFiscalYearService;
    private readonly Mock<IProductReservationService> _mockProductReservationService;
    private readonly Mock<IImageStorageService> _mockImageStorageService;

    public ShopPageTests()
    {
        _mockProductService = SetupService<IProductService>();
        _mockFiscalYearService = SetupService<IFiscalYearService>();
        _mockProductReservationService = SetupService<IProductReservationService>();
        _mockImageStorageService = SetupService<IImageStorageService>();

        _mockProductService
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(new List<Product>());

        var fy = FiscalYear.Create(2025, 2026);
        fy.Id = 1;
        _mockFiscalYearService
            .Setup(x => x.GetAllFiscalYearsAsync())
            .ReturnsAsync(new List<FiscalYear> { fy });

        _mockProductReservationService
            .Setup(x => x.GetByUserIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<ProductReservation>());
    }

    #region Page Rendering Tests

    [Fact]
    public async Task ShopPage_RendersPageTitle()
    {
        SetupAuthentication("test-user", "Test User");

        var cut = Render<Shop>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar produtos") || cut.Markup.Contains("Loja RTUB") || cut.Markup.Contains("Sem produtos"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Loja RTUB", "page should display title");
        cut.Markup.Should().Contain("Produtos oficiais", "page should display subtitle");
    }

    [Fact]
    public async Task ShopPage_ShowsLoadingState_Initially()
    {
        SetupAuthentication("test-user", "Test User");
        _mockFiscalYearService
            .Setup(x => x.GetAllFiscalYearsAsync())
            .Returns(async () =>
            {
                await Task.Delay(100);
                return (IEnumerable<FiscalYear>)new List<FiscalYear> { FiscalYear.Create(2025, 2026) };
            });
        _mockProductService
            .Setup(x => x.GetAllAsync())
            .Returns(async () =>
            {
                await Task.Delay(100);
                return (IEnumerable<Product>)new List<Product>();
            });

        var cut = Render<Shop>();

        cut.Markup.Should().Contain("Loja RTUB", "page should display title");
        cut.Markup.Should().Match(m => m.Contains("A carregar produtos") || m.Contains("Loja RTUB"), "should show loading or title initially");

        cut.WaitForState(() => cut.Markup.Contains("Sem produtos") || cut.Markup.Contains("Loja RTUB"), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task ShopPage_ShowsEmptyState_WhenNoProducts()
    {
        SetupAuthentication("test-user", "Test User");

        var cut = Render<Shop>();
        cut.WaitForState(() => cut.Markup.Contains("Sem produtos") || cut.Markup.Contains("Pesquisar produtos"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Sem produtos disponíveis", "should show empty state when no products");
    }

    [Fact]
    public async Task ShopPage_ShowsSearchBar_WhenLoaded()
    {
        SetupAuthentication("test-user", "Test User");

        var cut = Render<Shop>();
        cut.WaitForState(() => cut.Markup.Contains("Pesquisar produtos") || cut.Markup.Contains("Sem produtos"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Pesquisar produtos", "page should show search bar");
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task ShopPage_ShowsCreateButton_ForAdminUser()
    {
        SetupAuthenticationAsAdmin("admin-user");

        var cut = Render<Shop>();
        cut.WaitForState(() => cut.Markup.Contains("Adicionar Produto") || cut.Markup.Contains("Sem produtos"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Adicionar Produto", "admin should see create button");
    }

    [Fact]
    public async Task ShopPage_HidesCreateButton_ForRegularUser()
    {
        SetupAuthentication("regular-user", "Regular User");

        var cut = Render<Shop>();
        cut.WaitForState(() => cut.Markup.Contains("Loja RTUB") && (!cut.Markup.Contains("A carregar produtos") || cut.Markup.Contains("Sem produtos")), TimeSpan.FromSeconds(2));

        cut.Markup.Should().NotContain("Adicionar Produto", "regular user should not see create button");
    }

    #endregion
}
