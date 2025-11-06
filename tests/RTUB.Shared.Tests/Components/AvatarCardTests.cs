using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the AvatarCard component to ensure member cards display correctly
/// with proper actions, badges, and accessibility features
/// </summary>
public class AvatarCardTests : TestContext
{
    [Fact]
    public void AvatarCard_RendersWithBasicProperties()
    {
        // Arrange & Act
        var cut = RenderComponent<AvatarCard>(parameters => parameters
            .Add(p => p.AvatarUrl, "/images/avatar.jpg")
            .Add(p => p.TunaName, "Tuninho")
            .Add(p => p.FullName, "João Silva")
            .Add(p => p.InstrumentText, "Guitarra")
            .Add(p => p.AltText, "Tuninho"));

        // Assert
        cut.Markup.Should().Contain("avatar-card", "should have avatar-card class");
        cut.Markup.Should().Contain("Tuninho", "should display tuna name");
        cut.Markup.Should().Contain("João Silva", "should display full name");
        cut.Markup.Should().Contain("Guitarra", "should display instrument");
        cut.Markup.Should().Contain("/images/avatar.jpg", "should have avatar URL");
    }

    [Fact]
    public void AvatarCard_DisplaysViewButton()
    {
        // Arrange & Act
        var cut = RenderComponent<AvatarCard>(parameters => parameters
            .Add(p => p.AvatarUrl, "/images/avatar.jpg")
            .Add(p => p.ViewTooltip, "Ver Detalhes"));

        // Assert
        cut.Markup.Should().Contain("Ver Detalhes", "should display view button text");
        cut.Markup.Should().Contain("bi-eye", "should have eye icon");
    }

    [Fact]
    public void AvatarCard_ShowsEditButton_WhenShowEditButtonIsTrue()
    {
        // Arrange & Act
        var cut = RenderComponent<AvatarCard>(parameters => parameters
            .Add(p => p.AvatarUrl, "/images/avatar.jpg")
            .Add(p => p.ShowEditButton, true)
            .Add(p => p.EditTooltip, "Editar"));

        // Assert
        cut.Markup.Should().Contain("avatar-card-edit-btn", "should have edit button");
        cut.Markup.Should().Contain("bi-pencil", "should have pencil icon");
        cut.Markup.Should().Contain("Editar", "should have edit tooltip");
    }

    [Fact]
    public void AvatarCard_HidesEditButton_WhenShowEditButtonIsFalse()
    {
        // Arrange & Act
        var cut = RenderComponent<AvatarCard>(parameters => parameters
            .Add(p => p.AvatarUrl, "/images/avatar.jpg")
            .Add(p => p.ShowEditButton, false));

        // Assert
        cut.Markup.Should().NotContain("avatar-card-edit-btn", "should not have edit button");
    }

    [Fact]
    public void AvatarCard_ShowsDeleteButton_WhenShowDeleteButtonIsTrue()
    {
        // Arrange & Act
        var cut = RenderComponent<AvatarCard>(parameters => parameters
            .Add(p => p.AvatarUrl, "/images/avatar.jpg")
            .Add(p => p.ShowDeleteButton, true)
            .Add(p => p.DeleteTooltip, "Eliminar"));

        // Assert
        cut.Markup.Should().Contain("avatar-card-delete-btn", "should have delete button");
        cut.Markup.Should().Contain("bi-trash", "should have trash icon");
        cut.Markup.Should().Contain("Eliminar", "should have delete tooltip");
    }

    [Fact]
    public void AvatarCard_HidesDeleteButton_WhenShowDeleteButtonIsFalse()
    {
        // Arrange & Act
        var cut = RenderComponent<AvatarCard>(parameters => parameters
            .Add(p => p.AvatarUrl, "/images/avatar.jpg")
            .Add(p => p.ShowDeleteButton, false));

        // Assert
        cut.Markup.Should().NotContain("avatar-card-delete-btn", "should not have delete button");
    }

    [Fact]
    public void AvatarCard_InvokesOnView_WhenViewButtonClicked()
    {
        // Arrange
        var viewClicked = false;
        var cut = RenderComponent<AvatarCard>(parameters => parameters
            .Add(p => p.AvatarUrl, "/images/avatar.jpg")
            .Add(p => p.OnView, EventCallback.Factory.Create(this, () => viewClicked = true)));

        // Act
        var button = cut.Find(".btn-purple");
        button.Click();

        // Assert
        viewClicked.Should().BeTrue("view button click should invoke OnView callback");
    }

    [Fact]
    public void AvatarCard_InvokesOnEdit_WhenEditButtonClicked()
    {
        // Arrange
        var editClicked = false;
        var cut = RenderComponent<AvatarCard>(parameters => parameters
            .Add(p => p.AvatarUrl, "/images/avatar.jpg")
            .Add(p => p.ShowEditButton, true)
            .Add(p => p.OnEdit, EventCallback.Factory.Create(this, () => editClicked = true)));

        // Act
        var button = cut.Find(".avatar-card-edit-btn");
        button.Click();

        // Assert
        editClicked.Should().BeTrue("edit button click should invoke OnEdit callback");
    }

    [Fact]
    public void AvatarCard_InvokesOnDelete_WhenDeleteButtonClicked()
    {
        // Arrange
        var deleteClicked = false;
        var cut = RenderComponent<AvatarCard>(parameters => parameters
            .Add(p => p.AvatarUrl, "/images/avatar.jpg")
            .Add(p => p.ShowDeleteButton, true)
            .Add(p => p.OnDelete, EventCallback.Factory.Create(this, () => deleteClicked = true)));

        // Act
        var button = cut.Find(".avatar-card-delete-btn");
        button.Click();

        // Assert
        deleteClicked.Should().BeTrue("delete button click should invoke OnDelete callback");
    }

    [Fact]
    public void AvatarCard_InvokesOnView_WhenEnterKeyPressed()
    {
        // Arrange
        var viewClicked = false;
        var cut = RenderComponent<AvatarCard>(parameters => parameters
            .Add(p => p.AvatarUrl, "/images/avatar.jpg")
            .Add(p => p.OnView, EventCallback.Factory.Create(this, () => viewClicked = true)));

        // Act
        var card = cut.Find(".avatar-card");
        card.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        // Assert
        viewClicked.Should().BeTrue("pressing Enter should invoke OnView callback");
    }

    [Fact]
    public void AvatarCard_DoesNotInvokeOnView_WhenOtherKeyPressed()
    {
        // Arrange
        var viewClicked = false;
        var cut = RenderComponent<AvatarCard>(parameters => parameters
            .Add(p => p.AvatarUrl, "/images/avatar.jpg")
            .Add(p => p.OnView, EventCallback.Factory.Create(this, () => viewClicked = true)));

        // Act
        var card = cut.Find(".avatar-card");
        card.KeyDown(new KeyboardEventArgs { Key = "Space" });

        // Assert
        viewClicked.Should().BeFalse("pressing other keys should not invoke OnView callback");
    }

    [Fact]
    public void AvatarCard_HasTabIndex_ForKeyboardAccessibility()
    {
        // Arrange & Act
        var cut = RenderComponent<AvatarCard>(parameters => parameters
            .Add(p => p.AvatarUrl, "/images/avatar.jpg"));

        // Assert
        cut.Markup.Should().Contain("tabindex=\"0\"", "should have tabindex for keyboard navigation");
    }

    [Fact]
    public void AvatarCard_DisplaysSkeletonLoader_WhenLazyLoadEnabled()
    {
        // Arrange & Act
        var cut = RenderComponent<AvatarCard>(parameters => parameters
            .Add(p => p.AvatarUrl, "/images/avatar.jpg")
            .Add(p => p.LazyLoad, true));

        // Assert - initially skeleton should be visible
        cut.Markup.Should().Contain("avatar-card-skeleton", "should show skeleton loader initially");
    }

    [Fact]
    public void AvatarCard_UsesLazyLoading_WhenLazyLoadEnabled()
    {
        // Arrange & Act
        var cut = RenderComponent<AvatarCard>(parameters => parameters
            .Add(p => p.AvatarUrl, "/images/avatar.jpg")
            .Add(p => p.LazyLoad, true));

        // Assert
        cut.Markup.Should().Contain("loading=\"lazy\"", "should use lazy loading attribute");
    }

    [Fact]
    public void AvatarCard_UsesEagerLoading_WhenLazyLoadDisabled()
    {
        // Arrange & Act
        var cut = RenderComponent<AvatarCard>(parameters => parameters
            .Add(p => p.AvatarUrl, "/images/avatar.jpg")
            .Add(p => p.LazyLoad, false));

        // Assert
        cut.Markup.Should().Contain("loading=\"eager\"", "should use eager loading attribute");
    }

    [Fact]
    public void AvatarCard_RendersBadgeContent()
    {
        // Arrange
        var badgeFragment = (RenderFragment)(builder =>
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", "badge bg-primary");
            builder.AddContent(2, "TUNO");
            builder.CloseElement();
        });

        // Act
        var cut = RenderComponent<AvatarCard>(parameters => parameters
            .Add(p => p.AvatarUrl, "/images/avatar.jpg")
            .Add(p => p.BadgeContent, badgeFragment));

        // Assert
        cut.Markup.Should().Contain("TUNO", "should render badge content");
        cut.Markup.Should().Contain("badge bg-primary", "should have badge classes");
    }

    [Fact]
    public void AvatarCard_HandlesEmptyInstrumentText()
    {
        // Arrange & Act
        var cut = RenderComponent<AvatarCard>(parameters => parameters
            .Add(p => p.AvatarUrl, "/images/avatar.jpg")
            .Add(p => p.InstrumentText, ""));

        // Assert
        cut.Markup.Should().NotContain("bi-music-note", "should not show instrument icon when text is empty");
    }

    [Fact]
    public void AvatarCard_DisplaysInstrumentIcon_WhenInstrumentTextProvided()
    {
        // Arrange & Act
        var cut = RenderComponent<AvatarCard>(parameters => parameters
            .Add(p => p.AvatarUrl, "/images/avatar.jpg")
            .Add(p => p.InstrumentText, "Guitarra"));

        // Assert
        cut.Markup.Should().Contain("bi-music-note", "should show instrument icon");
    }

    [Fact]
    public void AvatarCard_HasProperAriaLabels()
    {
        // Arrange & Act
        var cut = RenderComponent<AvatarCard>(parameters => parameters
            .Add(p => p.AvatarUrl, "/images/avatar.jpg")
            .Add(p => p.ShowEditButton, true)
            .Add(p => p.ShowDeleteButton, true)
            .Add(p => p.ViewTooltip, "Ver Detalhes")
            .Add(p => p.EditTooltip, "Editar")
            .Add(p => p.DeleteTooltip, "Eliminar"));

        // Assert
        cut.Markup.Should().Contain("aria-label=\"Ver Detalhes\"", "view button should have aria-label");
        cut.Markup.Should().Contain("aria-label=\"Editar\"", "edit button should have aria-label");
        cut.Markup.Should().Contain("aria-label=\"Eliminar\"", "delete button should have aria-label");
    }

    [Fact]
    public void AvatarCard_DisplaysOnlyTunaName_WhenFullNameNotProvided()
    {
        // Arrange & Act
        var cut = RenderComponent<AvatarCard>(parameters => parameters
            .Add(p => p.AvatarUrl, "/images/avatar.jpg")
            .Add(p => p.TunaName, "Tuninho")
            .Add(p => p.FullName, ""));

        // Assert
        cut.Markup.Should().Contain("Tuninho", "should display tuna name");
        cut.Markup.Should().NotContain("avatar-card-full-name", "should not have full name section");
    }

    [Fact]
    public void AvatarCard_DisplaysBothNames_WhenBothProvided()
    {
        // Arrange & Act
        var cut = RenderComponent<AvatarCard>(parameters => parameters
            .Add(p => p.AvatarUrl, "/images/avatar.jpg")
            .Add(p => p.TunaName, "Tuninho")
            .Add(p => p.FullName, "João Silva"));

        // Assert
        cut.Markup.Should().Contain("Tuninho", "should display tuna name");
        cut.Markup.Should().Contain("João Silva", "should display full name");
        cut.Markup.Should().Contain("avatar-card-tuna-name", "should have tuna name class");
        cut.Markup.Should().Contain("avatar-card-full-name", "should have full name class");
    }
}
