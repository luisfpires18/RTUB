using AutoFixture;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Core.Entities;
using RTUB.Shared.Components.Cards;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the ProductCard component to ensure product cards display correctly
/// and handle different states (with/without reservations, admin/non-admin, member/non-member)
/// </summary>
public class ProductCardTests : TestContext
{
    private readonly Fixture _fixture;

    public ProductCardTests()
    {
        _fixture = new Fixture();
    }

    [Fact]
    public void ProductCard_RendersProductName()
    {
        // Arrange
        var product = Product.Create("Test T-Shirt", "Roupa", 25.00m, 10);

        // Act
        var cut = RenderComponent<ProductCard>(parameters => parameters
            .Add(p => p.Product, product)
            .Add(p => p.IsAuthenticated, true)
            .Add(p => p.IsMember, true));

        // Assert
        cut.Markup.Should().Contain("Test T-Shirt", "card should display product name");
    }

    [Fact]
    public void ProductCard_RendersProductType()
    {
        // Arrange
        var product = Product.Create("Test Album", "Álbum", 15.00m, 5);

        // Act
        var cut = RenderComponent<ProductCard>(parameters => parameters
            .Add(p => p.Product, product)
            .Add(p => p.IsAuthenticated, true)
            .Add(p => p.IsMember, true));

        // Assert
        cut.Markup.Should().Contain("Álbum", "card should display product type");
        cut.Markup.Should().Contain("bg-purple", "type badge should have purple background");
    }

    [Fact]
    public void ProductCard_RendersProductPrice()
    {
        // Arrange
        var product = Product.Create("Test Pin", "Pin", 5.50m, 20);

        // Act
        var cut = RenderComponent<ProductCard>(parameters => parameters
            .Add(p => p.Product, product)
            .Add(p => p.IsAuthenticated, true)
            .Add(p => p.IsMember, true));

        // Assert
        cut.Markup.Should().Contain("5.50 €", "card should display product price");
        cut.Markup.Should().Contain("text-success", "price should be green");
    }

    [Fact]
    public void ProductCard_RendersStockInfo_WhenInStock()
    {
        // Arrange
        var product = Product.Create("Test Product", "Tipo", 10.00m, 8);

        // Act
        var cut = RenderComponent<ProductCard>(parameters => parameters
            .Add(p => p.Product, product)
            .Add(p => p.IsAuthenticated, true)
            .Add(p => p.IsMember, true));

        // Assert
        cut.Markup.Should().Contain("Em stock: 8 unidades", "card should display stock count");
        cut.Markup.Should().Contain("bi-box-seam", "stock row should have icon");
    }

    [Fact]
    public void ProductCard_RendersOutOfStock_WhenStockIsZero()
    {
        // Arrange
        var product = Product.Create("Test Product", "Tipo", 10.00m, 0);

        // Act
        var cut = RenderComponent<ProductCard>(parameters => parameters
            .Add(p => p.Product, product)
            .Add(p => p.IsAuthenticated, true)
            .Add(p => p.IsMember, true));

        // Assert
        cut.Markup.Should().Contain("Sem Stock", "card should display out of stock message");
    }

    [Fact]
    public void ProductCard_ShowsMembersOnlyBadge_WhenNotPublic()
    {
        // Arrange
        var product = Product.Create("Test Product", "Tipo", 10.00m, 5);
        product.SetPublicVisibility(false);

        // Act
        var cut = RenderComponent<ProductCard>(parameters => parameters
            .Add(p => p.Product, product)
            .Add(p => p.IsAuthenticated, true)
            .Add(p => p.IsMember, true));

        // Assert
        cut.Markup.Should().Contain("Apenas Membros", "card should display members-only badge");
        cut.Markup.Should().Contain("bg-warning", "members-only badge should have warning background");
        cut.Markup.Should().Contain("bi-lock-fill", "members-only badge should have lock icon");
    }

    [Fact]
    public void ProductCard_ShowsReserveButton_WhenUserHasNoReservation()
    {
        // Arrange
        var product = Product.Create("Test Product", "Tipo", 10.00m, 5);

        // Act
        var cut = RenderComponent<ProductCard>(parameters => parameters
            .Add(p => p.Product, product)
            .Add(p => p.HasReservation, false)
            .Add(p => p.IsAuthenticated, true)
            .Add(p => p.IsMember, true));

        // Assert
        cut.Markup.Should().Contain("Reservar", "card should show reserve button");
        cut.Markup.Should().Contain("bi-bookmark-plus", "reserve button should have bookmark-plus icon");
    }

    [Fact]
    public void ProductCard_ShowsViewAndCancelButtons_WhenUserHasReservation()
    {
        // Arrange
        var product = Product.Create("Test Product", "Tipo", 10.00m, 5);

        // Act
        var cut = RenderComponent<ProductCard>(parameters => parameters
            .Add(p => p.Product, product)
            .Add(p => p.HasReservation, true)
            .Add(p => p.IsAuthenticated, true)
            .Add(p => p.IsMember, true));

        // Assert
        cut.Markup.Should().Contain("Ver Reserva", "card should show view reservation button");
        cut.Markup.Should().Contain("bi-bookmark-check", "view reservation button should have bookmark-check icon");
        cut.Markup.Should().Contain("Anular", "card should show cancel button");
        cut.Markup.Should().Contain("bi-x-circle", "cancel button should have x-circle icon");
        cut.Markup.Should().NotContain("Reservar", "reserve button should be hidden when user has reservation");
    }

    [Fact]
    public void ProductCard_HidesReserveButton_WhenOutOfStock()
    {
        // Arrange
        var product = Product.Create("Test Product", "Tipo", 10.00m, 0);

        // Act
        var cut = RenderComponent<ProductCard>(parameters => parameters
            .Add(p => p.Product, product)
            .Add(p => p.HasReservation, false)
            .Add(p => p.IsAuthenticated, true)
            .Add(p => p.IsMember, true));

        // Assert
        cut.Markup.Should().NotContain("Reservar", "reserve button should be hidden when out of stock");
    }

    [Fact]
    public void ProductCard_ShowsAdminButtons_WhenUserIsAdmin()
    {
        // Arrange
        var product = Product.Create("Test Product", "Tipo", 10.00m, 5);

        // Act
        var cut = RenderComponent<ProductCard>(parameters => parameters
            .Add(p => p.Product, product)
            .Add(p => p.IsAdmin, true)
            .Add(p => p.IsAuthenticated, true)
            .Add(p => p.IsMember, true));

        // Assert
        cut.Markup.Should().Contain("product-btn-edit", "card should show edit button for admin");
        cut.Markup.Should().Contain("product-btn-delete", "card should show delete button for admin");
        cut.Markup.Should().Contain("bi-pencil", "edit button should have pencil icon");
        cut.Markup.Should().Contain("bi-trash", "delete button should have trash icon");
    }

    [Fact]
    public void ProductCard_HidesAdminButtons_WhenUserIsNotAdmin()
    {
        // Arrange
        var product = Product.Create("Test Product", "Tipo", 10.00m, 5);

        // Act
        var cut = RenderComponent<ProductCard>(parameters => parameters
            .Add(p => p.Product, product)
            .Add(p => p.IsAdmin, false)
            .Add(p => p.IsAuthenticated, true)
            .Add(p => p.IsMember, true));

        // Assert
        cut.Markup.Should().NotContain("product-btn-edit", "edit button should be hidden for non-admin");
        cut.Markup.Should().NotContain("product-btn-delete", "delete button should be hidden for non-admin");
    }

    [Fact]
    public void ProductCard_ShowsViewReservationsButton_WhenUserIsAdmin()
    {
        // Arrange
        var product = Product.Create("Test Product", "Tipo", 10.00m, 5);

        // Act
        var cut = RenderComponent<ProductCard>(parameters => parameters
            .Add(p => p.Product, product)
            .Add(p => p.IsAdmin, true)
            .Add(p => p.HasReservation, false)
            .Add(p => p.IsAuthenticated, true)
            .Add(p => p.IsMember, true));

        // Assert
        cut.Markup.Should().Contain("Ver Reservas", "card should show view reservations button for admin");
        cut.Markup.Should().Contain("bi-collection", "view reservations button should have collection icon");
    }

    [Fact]
    public void ProductCard_HidesActions_WhenNotPublicAndUserNotMember()
    {
        // Arrange
        var product = Product.Create("Test Product", "Tipo", 10.00m, 5);
        product.SetPublicVisibility(false);

        // Act
        var cut = RenderComponent<ProductCard>(parameters => parameters
            .Add(p => p.Product, product)
            .Add(p => p.IsAuthenticated, true)
            .Add(p => p.IsMember, false));

        // Assert
        cut.Markup.Should().NotContain("Reservar", "actions should be hidden for non-members when product is not public");
        cut.Markup.Should().NotContain("product-actions", "action bar should be hidden for non-members when product is not public");
    }

    [Fact]
    public void ProductCard_ShowsActions_WhenNotPublicAndUserIsMember()
    {
        // Arrange
        var product = Product.Create("Test Product", "Tipo", 10.00m, 5);
        product.SetPublicVisibility(false);

        // Act
        var cut = RenderComponent<ProductCard>(parameters => parameters
            .Add(p => p.Product, product)
            .Add(p => p.HasReservation, false)
            .Add(p => p.IsAuthenticated, true)
            .Add(p => p.IsMember, true));

        // Assert
        cut.Markup.Should().Contain("Reservar", "actions should be shown for members even when product is not public");
    }

    [Fact]
    public void ProductCard_KeepsCancelButtonEnabled_WhenHasReservationAndOutOfStock()
    {
        // Arrange
        var product = Product.Create("Test Product", "Tipo", 10.00m, 0);

        // Act
        var cut = RenderComponent<ProductCard>(parameters => parameters
            .Add(p => p.Product, product)
            .Add(p => p.HasReservation, true)
            .Add(p => p.IsAuthenticated, true)
            .Add(p => p.IsMember, true));

        // Assert
        cut.Markup.Should().Contain("Anular", "cancel button should be visible even when out of stock");
        // The button should not have disabled attribute
        var cancelButton = cut.Find("button:contains('Anular')");
        cancelButton.Should().NotBeNull("cancel button should exist");
    }

    [Fact]
    public void ProductCard_HasCorrectCssClasses()
    {
        // Arrange
        var product = Product.Create("Test Product", "Tipo", 10.00m, 5);

        // Act
        var cut = RenderComponent<ProductCard>(parameters => parameters
            .Add(p => p.Product, product)
            .Add(p => p.IsAuthenticated, true)
            .Add(p => p.IsMember, true));

        // Assert
        cut.Markup.Should().Contain("card bg-dark", "card should have dark background");
        cut.Markup.Should().Contain("border-purple-700", "card should have purple border");
        cut.Markup.Should().Contain("rounded-3", "card should have rounded corners");
        cut.Markup.Should().Contain("shadow-sm", "card should have small shadow");
    }

    [Fact]
    public void ProductCard_MediaHeaderHas16To9AspectRatio()
    {
        // Arrange
        var product = Product.Create("Test Product", "Tipo", 10.00m, 5);

        // Act
        var cut = RenderComponent<ProductCard>(parameters => parameters
            .Add(p => p.Product, product)
            .Add(p => p.IsAuthenticated, true)
            .Add(p => p.IsMember, true));

        // Assert
        cut.Markup.Should().Contain("product-card-media", "card should have media container");
    }
}
