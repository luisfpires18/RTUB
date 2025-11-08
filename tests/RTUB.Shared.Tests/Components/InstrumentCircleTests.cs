using AutoFixture;
using Bunit;
using FluentAssertions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the InstrumentCircle component to ensure instruments display correctly
/// </summary>
public class InstrumentCircleTests : TestContext
{
    private readonly Fixture _fixture;

    public InstrumentCircleTests()
    {
        _fixture = new Fixture();
    }

    [Fact]
    public void InstrumentCircle_RendersInstrumentName()
    {
        // Arrange
        var instrument = Instrument.Create("Guitarra", "Test Guitar", InstrumentCondition.Good);

        // Act
        var cut = RenderComponent<InstrumentCircle>(parameters => parameters
            .Add(p => p.Instrument, instrument)
            .Add(p => p.ImageUrl, ""));

        // Assert
        cut.Markup.Should().Contain("Test Guitar", "card should display instrument name");
    }

    [Fact]
    public void InstrumentCircle_RendersInstrumentCategory()
    {
        // Arrange
        var instrument = Instrument.Create("Bandolim", "Test Mandolin", InstrumentCondition.Excellent);

        // Act
        var cut = RenderComponent<InstrumentCircle>(parameters => parameters
            .Add(p => p.Instrument, instrument)
            .Add(p => p.ImageUrl, ""));

        // Assert
        cut.Markup.Should().Contain("Bandolim", "card should display instrument category");
    }

    [Fact]
    public void InstrumentCircle_RendersConditionBadge()
    {
        // Arrange
        var instrument = Instrument.Create("Flauta", "Test Flute", InstrumentCondition.Good);

        // Act
        var cut = RenderComponent<InstrumentCircle>(parameters => parameters
            .Add(p => p.Instrument, instrument)
            .Add(p => p.ImageUrl, ""));

        // Assert
        cut.Markup.Should().Contain("Bom", "card should display condition in Portuguese");
        cut.Markup.Should().Contain("bg-info", "good condition should have bg-info class");
    }

    [Theory]
    [InlineData(InstrumentCondition.Excellent, "Óptimo", "bg-success")]
    [InlineData(InstrumentCondition.Good, "Bom", "bg-info")]
    [InlineData(InstrumentCondition.Worn, "Velho", "bg-warning")]
    [InlineData(InstrumentCondition.NeedsMaintenance, "Precisa Manutenção", "bg-danger")]
    public void InstrumentCircle_DisplaysCorrectCondition(InstrumentCondition condition, string expectedText, string expectedClass)
    {
        // Arrange
        var instrument = Instrument.Create("Baixo", "Test Bass", condition);

        // Act
        var cut = RenderComponent<InstrumentCircle>(parameters => parameters
            .Add(p => p.Instrument, instrument)
            .Add(p => p.ImageUrl, ""));

        // Assert
        cut.Markup.Should().Contain(expectedText, $"card should display condition as {expectedText}");
        cut.Markup.Should().Contain(expectedClass, $"condition {condition} should have class {expectedClass}");
    }

    [Fact]
    public void InstrumentCircle_DisplaysBrand_WhenSet()
    {
        // Arrange
        var instrument = Instrument.Create("Guitarra", "Test Guitar", InstrumentCondition.Good);
        instrument.Brand = "Fender";

        // Act
        var cut = RenderComponent<InstrumentCircle>(parameters => parameters
            .Add(p => p.Instrument, instrument)
            .Add(p => p.ImageUrl, ""));

        // Assert
        cut.Markup.Should().Contain("Fender", "card should display brand when set");
        cut.Markup.Should().Contain("Marca:", "card should have brand label");
    }

    [Fact]
    public void InstrumentCircle_DisplaysLocation_WhenSet()
    {
        // Arrange
        var instrument = Instrument.Create("Acordeao", "Test Accordion", InstrumentCondition.Good);
        instrument.Location = "Storage Room";

        // Act
        var cut = RenderComponent<InstrumentCircle>(parameters => parameters
            .Add(p => p.Instrument, instrument)
            .Add(p => p.ImageUrl, ""));

        // Assert
        cut.Markup.Should().Contain("Storage Room", "card should display location when set");
        cut.Markup.Should().Contain("Local:", "card should have location label");
    }

    [Theory]
    [InlineData("Estandarte", "bi-flag")]
    [InlineData("Guitarra", "bi-music-note")]
    [InlineData("Acordeao", "bi-grid-3x3-gap")]
    [InlineData("Percussao", "bi-circle")]
    public void InstrumentCircle_DisplaysCorrectIcon_ForInstrumentType(string category, string expectedIcon)
    {
        // Arrange
        var instrument = Instrument.Create(category, "Test Instrument", InstrumentCondition.Good);

        // Act
        var cut = RenderComponent<InstrumentCircle>(parameters => parameters
            .Add(p => p.Instrument, instrument)
            .Add(p => p.ImageUrl, ""));

        // Assert
        cut.Markup.Should().Contain(expectedIcon, $"instrument type {category} should display icon {expectedIcon}");
    }

    [Fact]
    public void InstrumentCircle_ShowsViewButton_WhenShowViewButtonIsTrue()
    {
        // Arrange
        var instrument = Instrument.Create("Flauta", "Test Flute", InstrumentCondition.Good);
        var viewClicked = false;

        // Act
        var cut = RenderComponent<InstrumentCircle>(parameters => parameters
            .Add(p => p.Instrument, instrument)
            .Add(p => p.ImageUrl, "")
            .Add(p => p.ShowViewButton, true)
            .Add(p => p.OnView, () => viewClicked = true));

        // Assert
        cut.Markup.Should().Contain("Ver Detalhes", "view button should be displayed");
        cut.Markup.Should().Contain("bi-eye", "view button should have eye icon");
    }

    [Fact]
    public void InstrumentCircle_HidesViewButton_WhenShowViewButtonIsFalse()
    {
        // Arrange
        var instrument = Instrument.Create("Bandolim", "Test Mandolin", InstrumentCondition.Good);

        // Act
        var cut = RenderComponent<InstrumentCircle>(parameters => parameters
            .Add(p => p.Instrument, instrument)
            .Add(p => p.ImageUrl, "")
            .Add(p => p.ShowViewButton, false));

        // Assert
        cut.Markup.Should().NotContain("Ver Detalhes", "view button should be hidden");
    }

    [Fact]
    public void InstrumentCircle_ShowsEditButton_WhenShowEditButtonIsTrue()
    {
        // Arrange
        var instrument = Instrument.Create("Baixo", "Test Bass", InstrumentCondition.Good);

        // Act
        var cut = RenderComponent<InstrumentCircle>(parameters => parameters
            .Add(p => p.Instrument, instrument)
            .Add(p => p.ImageUrl, "")
            .Add(p => p.ShowEditButton, true));

        // Assert
        cut.Markup.Should().Contain("bi-pencil", "edit button should have pencil icon");
    }

    [Fact]
    public void InstrumentCircle_ShowsDeleteButton_WhenShowDeleteButtonIsTrue()
    {
        // Arrange
        var instrument = Instrument.Create("Cavaquinho", "Test Cavaquinho", InstrumentCondition.Good);

        // Act
        var cut = RenderComponent<InstrumentCircle>(parameters => parameters
            .Add(p => p.Instrument, instrument)
            .Add(p => p.ImageUrl, "")
            .Add(p => p.ShowDeleteButton, true));

        // Assert
        cut.Markup.Should().Contain("bi-trash", "delete button should have trash icon");
    }

    [Fact]
    public void InstrumentCircle_DoesNotDisplayBrand_WhenNotSet()
    {
        // Arrange
        var instrument = Instrument.Create("Fagote", "Test Bassoon", InstrumentCondition.Good);
        // Brand not set

        // Act
        var cut = RenderComponent<InstrumentCircle>(parameters => parameters
            .Add(p => p.Instrument, instrument)
            .Add(p => p.ImageUrl, ""));

        // Assert
        cut.Markup.Should().NotContain("Marca:", "brand label should not be shown when brand is not set");
    }

    [Fact]
    public void InstrumentCircle_DoesNotDisplayLocation_WhenNotSet()
    {
        // Arrange
        var instrument = Instrument.Create("Pandeireta", "Test Tambourine", InstrumentCondition.Good);
        // Location not set

        // Act
        var cut = RenderComponent<InstrumentCircle>(parameters => parameters
            .Add(p => p.Instrument, instrument)
            .Add(p => p.ImageUrl, ""));

        // Assert
        cut.Markup.Should().NotContain("Local:", "location label should not be shown when location is not set");
    }
}
