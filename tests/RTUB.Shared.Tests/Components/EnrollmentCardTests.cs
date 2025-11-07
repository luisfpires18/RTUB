using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the EnrollmentCard component to ensure enrollment information displays correctly
/// with proper actions, badges, and text wrapping
/// </summary>
public class EnrollmentCardTests : TestContext
{
    [Fact]
    public void EnrollmentCard_RendersWithBasicProperties()
    {
        // Arrange & Act
        var cut = RenderComponent<EnrollmentCard>(parameters => parameters
            .Add(p => p.AvatarUrl, "/images/avatar.jpg")
            .Add(p => p.TunaName, "Tuninho")
            .Add(p => p.FullName, "João Silva")
            .Add(p => p.InstrumentText, "Guitarra")
            .Add(p => p.Notes, "Test notes")
            .Add(p => p.AltText, "Tuninho"));

        // Assert
        cut.Markup.Should().Contain("enrollment-card", "should have enrollment-card class");
        cut.Markup.Should().Contain("Tuninho", "should display tuna name");
        cut.Markup.Should().Contain("João Silva", "should display full name");
        cut.Markup.Should().Contain("Guitarra", "should display instrument");
        cut.Markup.Should().Contain("Test notes", "should display notes");
        cut.Markup.Should().Contain("/images/avatar.jpg", "should have avatar URL");
    }

    [Fact]
    public void EnrollmentCard_DisplaysDashForEmptyNotes()
    {
        // Arrange & Act
        var cut = RenderComponent<EnrollmentCard>(parameters => parameters
            .Add(p => p.AvatarUrl, "/images/avatar.jpg")
            .Add(p => p.Notes, ""));

        // Assert
        cut.Markup.Should().Contain("—", "should display dash for empty notes");
    }

    [Fact]
    public void EnrollmentCard_ShowsDeleteButton_WhenShowDeleteButtonIsTrue()
    {
        // Arrange & Act
        var cut = RenderComponent<EnrollmentCard>(parameters => parameters
            .Add(p => p.AvatarUrl, "/images/avatar.jpg")
            .Add(p => p.ShowDeleteButton, true)
            .Add(p => p.DeleteTooltip, "Eliminar Inscrição"));

        // Assert
        cut.Markup.Should().Contain("enrollment-card-delete-btn", "should have delete button");
        cut.Markup.Should().Contain("bi-trash", "should have trash icon");
        cut.Markup.Should().Contain("Eliminar Inscrição", "should have delete tooltip");
    }

    [Fact]
    public void EnrollmentCard_HidesDeleteButton_WhenShowDeleteButtonIsFalse()
    {
        // Arrange & Act
        var cut = RenderComponent<EnrollmentCard>(parameters => parameters
            .Add(p => p.AvatarUrl, "/images/avatar.jpg")
            .Add(p => p.ShowDeleteButton, false));

        // Assert
        cut.Markup.Should().NotContain("enrollment-card-delete-btn", "should not have delete button");
    }

    [Fact]
    public void EnrollmentCard_ShowsApproveButton_WhenShowApproveButtonIsTrue()
    {
        // Arrange & Act
        var cut = RenderComponent<EnrollmentCard>(parameters => parameters
            .Add(p => p.AvatarUrl, "/images/avatar.jpg")
            .Add(p => p.ShowApproveButton, true)
            .Add(p => p.ApproveTooltip, "Aprovar"));

        // Assert
        cut.Markup.Should().Contain("enrollment-card-approve-btn", "should have approve button");
        cut.Markup.Should().Contain("bi-check", "should have check icon");
        cut.Markup.Should().Contain("Aprovar", "should have approve tooltip");
    }

    [Fact]
    public void EnrollmentCard_HidesApproveButton_WhenShowApproveButtonIsFalse()
    {
        // Arrange & Act
        var cut = RenderComponent<EnrollmentCard>(parameters => parameters
            .Add(p => p.AvatarUrl, "/images/avatar.jpg")
            .Add(p => p.ShowApproveButton, false));

        // Assert
        cut.Markup.Should().NotContain("enrollment-card-approve-btn", "should not have approve button");
    }

    [Fact]
    public void EnrollmentCard_DisplaysInstrumentRow_WhenInstrumentTextProvided()
    {
        // Arrange & Act
        var cut = RenderComponent<EnrollmentCard>(parameters => parameters
            .Add(p => p.AvatarUrl, "/images/avatar.jpg")
            .Add(p => p.InstrumentText, "Guitarra"));

        // Assert
        cut.Markup.Should().Contain("Instrumento:", "should display instrument label");
        cut.Markup.Should().Contain("Guitarra", "should display instrument value");
    }

    [Fact]
    public void EnrollmentCard_HidesInstrumentRow_WhenInstrumentTextEmpty()
    {
        // Arrange & Act
        var cut = RenderComponent<EnrollmentCard>(parameters => parameters
            .Add(p => p.AvatarUrl, "/images/avatar.jpg")
            .Add(p => p.InstrumentText, ""));

        // Assert
        cut.Markup.Should().NotContain("Instrumento:", "should not display instrument label when empty");
    }

    [Fact]
    public void EnrollmentCard_WrapsNotesText()
    {
        // Arrange
        var longNotes = "This is a very long note that should wrap properly without clipping the text content. It contains multiple sentences and should display correctly.";

        // Act
        var cut = RenderComponent<EnrollmentCard>(parameters => parameters
            .Add(p => p.AvatarUrl, "/images/avatar.jpg")
            .Add(p => p.Notes, longNotes));

        // Assert
        cut.Markup.Should().Contain(longNotes, "should display long notes");
        cut.Markup.Should().Contain("enrollment-card-notes", "should have notes class for text wrapping");
    }

    [Fact]
    public void EnrollmentCard_DisplaysStatusText_WhenProvided()
    {
        // Arrange & Act
        var cut = RenderComponent<EnrollmentCard>(parameters => parameters
            .Add(p => p.AvatarUrl, "/images/avatar.jpg")
            .Add(p => p.StatusText, "Aprovado")
            .Add(p => p.StatusLabel, "Estado:"));

        // Assert
        cut.Markup.Should().Contain("Estado:", "should display status label");
        cut.Markup.Should().Contain("Aprovado", "should display status text");
    }

    [Fact]
    public void EnrollmentCard_HidesStatusRow_WhenStatusTextEmpty()
    {
        // Arrange & Act
        var cut = RenderComponent<EnrollmentCard>(parameters => parameters
            .Add(p => p.AvatarUrl, "/images/avatar.jpg")
            .Add(p => p.StatusText, ""));

        // Assert
        cut.Markup.Should().NotContain("Estado:", "should not display status label when empty");
    }

    [Fact]
    public void EnrollmentCard_RendersBadgeContent()
    {
        // Arrange
        RenderFragment badgeContent = builder =>
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", "badge bg-success");
            builder.AddContent(2, "Tuno");
            builder.CloseElement();
        };

        // Act
        var cut = RenderComponent<EnrollmentCard>(parameters => parameters
            .Add(p => p.AvatarUrl, "/images/avatar.jpg")
            .Add(p => p.BadgeContent, badgeContent));

        // Assert
        cut.Markup.Should().Contain("badge bg-success", "should render badge content");
        cut.Markup.Should().Contain("Tuno", "should display badge text");
    }
}
