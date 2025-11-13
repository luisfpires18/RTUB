using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Core.Enums;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the ParticipationModal component to ensure proper rendering and behavior
/// </summary>
public class ParticipationModalTests : TestContext
{
    [Fact]
    public void ParticipationModal_WhenShowIsFalse_DoesNotRender()
    {
        // Arrange & Act
        var cut = RenderComponent<ParticipationModal>(parameters => parameters
            .Add(p => p.Show, false)
            .Add(p => p.Title, "Test Participation Modal"));

        // Assert
        cut.Markup.Should().BeEmpty("modal should not render when Show is false");
    }

    [Fact]
    public void ParticipationModal_WhenShowIsTrue_Renders()
    {
        // Arrange & Act
        var cut = RenderComponent<ParticipationModal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Title, "Test Participation Modal"));

        // Assert
        cut.Markup.Should().Contain("modal-backdrop", "backdrop should be rendered");
        cut.Markup.Should().Contain("modal show d-block", "modal should have show and d-block classes");
    }

    [Fact]
    public void ParticipationModal_RendersTitle_WhenProvided()
    {
        // Arrange
        var expectedTitle = "Inscrever em Evento";

        // Act
        var cut = RenderComponent<ParticipationModal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Title, expectedTitle));

        // Assert
        cut.Markup.Should().Contain(expectedTitle, "modal should display the provided title");
    }

    [Fact]
    public void ParticipationModal_ShowsWillAttendSection_WhenShowWillAttendIsTrue()
    {
        // Arrange & Act
        var cut = RenderComponent<ParticipationModal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.ShowWillAttend, true)
            .Add(p => p.Title, "Test Modal"));

        // Assert
        cut.Markup.Should().Contain("Vais participar?", "modal should show 'Will Attend' question");
        cut.Markup.Should().Contain("Vou", "modal should show 'Yes' button");
        cut.Markup.Should().Contain("Não vou", "modal should show 'No' button");
    }

    [Fact]
    public void ParticipationModal_HidesWillAttendSection_WhenShowWillAttendIsFalse()
    {
        // Arrange & Act
        var cut = RenderComponent<ParticipationModal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.ShowWillAttend, false)
            .Add(p => p.Title, "Test Modal"));

        // Assert
        cut.Markup.Should().NotContain("Vais participar?", "modal should not show 'Will Attend' question for rehearsals");
    }

    [Fact]
    public void ParticipationModal_ShowsInstrumentToggle_ForNonLeitaoMembers()
    {
        // Arrange & Act
        var cut = RenderComponent<ParticipationModal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.IsLeitao, false)
            .Add(p => p.Title, "Test Modal"));

        // Assert
        cut.Markup.Should().Contain("Quero tocar", "modal should show 'Want to Play' toggle for non-Leitão members");
    }

    [Fact]
    public void ParticipationModal_ShowsInstrumentDropdown_WhenWantToPlayIsTrue()
    {
        // Arrange & Act
        var cut = RenderComponent<ParticipationModal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.IsLeitao, false)
            .Add(p => p.WantToPlay, true)
            .Add(p => p.Title, "Test Modal"));

        // Assert
        cut.Markup.Should().Contain("Instrumento", "modal should show instrument label");
        cut.Markup.Should().Contain("Selecionar...", "modal should show instrument dropdown");
    }

    [Fact]
    public void ParticipationModal_ShowsNotesField()
    {
        // Arrange & Act
        var cut = RenderComponent<ParticipationModal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.NotesPlaceholder, "Informações adicionais...")
            .Add(p => p.Title, "Test Modal"));

        // Assert
        cut.Markup.Should().Contain("Notas", "modal should show notes label");
        cut.Markup.Should().Contain("Informações adicionais...", "modal should show notes placeholder");
    }

    [Fact]
    public void ParticipationModal_ShowsSubmitButton_WithCustomText()
    {
        // Arrange
        var submitButtonText = "Confirmar Inscrição";

        // Act
        var cut = RenderComponent<ParticipationModal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.SubmitButtonText, submitButtonText)
            .Add(p => p.Title, "Test Modal"));

        // Assert
        cut.Markup.Should().Contain(submitButtonText, "modal should show custom submit button text");
    }

    [Fact]
    public void ParticipationModal_ShowsCancelButton()
    {
        // Arrange & Act
        var cut = RenderComponent<ParticipationModal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Title, "Test Modal"));

        // Assert
        cut.Markup.Should().Contain("Cancelar", "modal should show cancel button");
    }

    [Fact]
    public void ParticipationModal_ConfiguresNotesRows()
    {
        // Arrange
        var notesRows = 5;

        // Act
        var cut = RenderComponent<ParticipationModal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.NotesRows, notesRows)
            .Add(p => p.Title, "Test Modal"));

        // Assert
        cut.Markup.Should().Contain($"rows=\"{notesRows}\"", "modal should configure notes textarea rows");
    }

    [Fact]
    public void ParticipationModal_ShowsInstrumentAsOptional_ForLeitaoMembers()
    {
        // Arrange & Act
        var cut = RenderComponent<ParticipationModal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.IsLeitao, true)
            .Add(p => p.Title, "Test Modal"));

        // Assert
        cut.Markup.Should().Contain("Instrumento (Opcional)", "modal should show instrument as optional for Leitão members");
    }

    [Fact]
    public void ParticipationModal_ForEventMode_ShowsAllSections()
    {
        // Arrange & Act - Simulating event enrollment mode
        var cut = RenderComponent<ParticipationModal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.ShowWillAttend, true)
            .Add(p => p.WillAttend, true)
            .Add(p => p.IsLeitao, false)
            .Add(p => p.Title, "Inscrever em Evento")
            .Add(p => p.SubmitButtonText, "Inscrever"));

        // Assert
        cut.Markup.Should().Contain("Vais participar?", "event mode should show attendance question");
        cut.Markup.Should().Contain("Quero tocar", "event mode should show instrument toggle");
        cut.Markup.Should().Contain("Notas", "event mode should show notes field");
        cut.Markup.Should().Contain("Inscrever", "event mode should show submit button");
    }

    [Fact]
    public void ParticipationModal_ForRehearsalMode_HidesWillAttendSection()
    {
        // Arrange & Act - Simulating rehearsal attendance mode
        var cut = RenderComponent<ParticipationModal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.ShowWillAttend, false)
            .Add(p => p.IsLeitao, false)
            .Add(p => p.Title, "Marcar Presença")
            .Add(p => p.SubmitButtonText, "Confirmar"));

        // Assert
        cut.Markup.Should().NotContain("Vais participar?", "rehearsal mode should not show attendance question");
        cut.Markup.Should().Contain("Quero tocar", "rehearsal mode should show instrument toggle");
        cut.Markup.Should().Contain("Confirmar", "rehearsal mode should show confirm button");
    }
}
