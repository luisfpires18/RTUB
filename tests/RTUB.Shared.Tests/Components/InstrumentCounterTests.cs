using Bunit;
using FluentAssertions;
using RTUB.Core.Enums;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the InstrumentCounter component to ensure instrument counts display correctly
/// </summary>
public class InstrumentCounterTests : TestContext
{
    [Fact]
    public void InstrumentCounter_DoesNotRender_WhenInstrumentCountsIsNull()
    {
        // Arrange & Act
        var cut = RenderComponent<InstrumentCounter>(parameters => parameters
            .Add(p => p.InstrumentCounts, null));

        // Assert
        cut.Markup.Should().BeEmpty("component should not render when InstrumentCounts is null");
    }

    [Fact]
    public void InstrumentCounter_DoesNotRender_WhenAllCountsAreZero()
    {
        // Arrange
        var instrumentCounts = new Dictionary<InstrumentType, int>
        {
            { InstrumentType.Guitarra, 0 },
            { InstrumentType.Bandolim, 0 },
            { InstrumentType.Cavaquinho, 0 }
        };

        // Act
        var cut = RenderComponent<InstrumentCounter>(parameters => parameters
            .Add(p => p.InstrumentCounts, instrumentCounts));

        // Assert
        cut.Markup.Should().BeEmpty("component should not render when all counts are zero");
    }

    [Fact]
    public void InstrumentCounter_RendersCorrectly_WithNonZeroCounts()
    {
        // Arrange
        var instrumentCounts = new Dictionary<InstrumentType, int>
        {
            { InstrumentType.Guitarra, 5 },
            { InstrumentType.Bandolim, 3 },
            { InstrumentType.Cavaquinho, 0 }
        };

        // Act
        var cut = RenderComponent<InstrumentCounter>(parameters => parameters
            .Add(p => p.InstrumentCounts, instrumentCounts));

        // Assert
        cut.Markup.Should().Contain("instrument-counter-section", "component should render the counter section");
        cut.Markup.Should().Contain("instrument-counter-header", "component should render the header");
        cut.Markup.Should().Contain("instrument-counter-grid", "component should render the grid");
    }

    [Fact]
    public void InstrumentCounter_DisplaysDefaultTitle()
    {
        // Arrange
        var instrumentCounts = new Dictionary<InstrumentType, int>
        {
            { InstrumentType.Guitarra, 5 }
        };

        // Act
        var cut = RenderComponent<InstrumentCounter>(parameters => parameters
            .Add(p => p.InstrumentCounts, instrumentCounts));

        // Assert
        cut.Markup.Should().Contain("Instrumentos", "default title should be displayed");
    }

    [Fact]
    public void InstrumentCounter_DisplaysCustomTitle()
    {
        // Arrange
        var instrumentCounts = new Dictionary<InstrumentType, int>
        {
            { InstrumentType.Guitarra, 5 }
        };
        var customTitle = "Instruments Used";

        // Act
        var cut = RenderComponent<InstrumentCounter>(parameters => parameters
            .Add(p => p.InstrumentCounts, instrumentCounts)
            .Add(p => p.Title, customTitle));

        // Assert
        cut.Markup.Should().Contain(customTitle, "custom title should be displayed");
    }

    [Fact]
    public void InstrumentCounter_DisplaysCorrectCounts()
    {
        // Arrange
        var instrumentCounts = new Dictionary<InstrumentType, int>
        {
            { InstrumentType.Guitarra, 5 },
            { InstrumentType.Bandolim, 3 }
        };

        // Act
        var cut = RenderComponent<InstrumentCounter>(parameters => parameters
            .Add(p => p.InstrumentCounts, instrumentCounts));

        // Assert
        cut.Markup.Should().Contain(">5<", "should display count 5");
        cut.Markup.Should().Contain(">3<", "should display count 3");
    }

    [Fact]
    public void InstrumentCounter_OnlyShowsNonZeroCounts()
    {
        // Arrange
        var instrumentCounts = new Dictionary<InstrumentType, int>
        {
            { InstrumentType.Guitarra, 5 },
            { InstrumentType.Bandolim, 0 },
            { InstrumentType.Cavaquinho, 3 }
        };

        // Act
        var cut = RenderComponent<InstrumentCounter>(parameters => parameters
            .Add(p => p.InstrumentCounts, instrumentCounts));

        // Assert
        var boxes = cut.FindAll(".instrument-counter-box");
        boxes.Count.Should().Be(2, "should only display boxes for non-zero counts");
    }

    [Fact]
    public void InstrumentCounter_OrdersByCountDescending()
    {
        // Arrange
        var instrumentCounts = new Dictionary<InstrumentType, int>
        {
            { InstrumentType.Guitarra, 2 },
            { InstrumentType.Bandolim, 5 },
            { InstrumentType.Cavaquinho, 3 }
        };

        // Act
        var cut = RenderComponent<InstrumentCounter>(parameters => parameters
            .Add(p => p.InstrumentCounts, instrumentCounts));

        // Assert
        var boxes = cut.FindAll(".instrument-counter-box");
        boxes.Count.Should().Be(3, "should display all three non-zero instruments");
        
        // First box should have count 5 (highest)
        boxes[0].InnerHtml.Should().Contain(">5<", "highest count should be first");
    }

    [Fact]
    public void InstrumentCounter_HasMusicIcon()
    {
        // Arrange
        var instrumentCounts = new Dictionary<InstrumentType, int>
        {
            { InstrumentType.Guitarra, 5 }
        };

        // Act
        var cut = RenderComponent<InstrumentCounter>(parameters => parameters
            .Add(p => p.InstrumentCounts, instrumentCounts));

        // Assert
        cut.Markup.Should().Contain("bi-music-note-beamed", "should display music note icon");
    }

    [Fact]
    public void InstrumentCounter_AppliesDefaultMargins()
    {
        // Arrange
        var instrumentCounts = new Dictionary<InstrumentType, int>
        {
            { InstrumentType.Guitarra, 5 }
        };

        // Act
        var cut = RenderComponent<InstrumentCounter>(parameters => parameters
            .Add(p => p.InstrumentCounts, instrumentCounts));

        // Assert
        cut.Markup.Should().Contain("mt-3", "should have default margin-top");
        cut.Markup.Should().Contain("mb-4", "should have default margin-bottom");
    }

    [Fact]
    public void InstrumentCounter_AppliesCustomMargins()
    {
        // Arrange
        var instrumentCounts = new Dictionary<InstrumentType, int>
        {
            { InstrumentType.Guitarra, 5 }
        };

        // Act
        var cut = RenderComponent<InstrumentCounter>(parameters => parameters
            .Add(p => p.InstrumentCounts, instrumentCounts)
            .Add(p => p.MarginTop, "mt-5")
            .Add(p => p.MarginBottom, "mb-2"));

        // Assert
        cut.Markup.Should().Contain("mt-5", "should apply custom margin-top");
        cut.Markup.Should().Contain("mb-2", "should apply custom margin-bottom");
    }

    [Fact]
    public void InstrumentCounter_AppliesAdditionalClass()
    {
        // Arrange
        var instrumentCounts = new Dictionary<InstrumentType, int>
        {
            { InstrumentType.Guitarra, 5 }
        };
        var additionalClass = "custom-class";

        // Act
        var cut = RenderComponent<InstrumentCounter>(parameters => parameters
            .Add(p => p.InstrumentCounts, instrumentCounts)
            .Add(p => p.AdditionalClass, additionalClass));

        // Assert
        cut.Markup.Should().Contain(additionalClass, "should apply additional CSS class");
    }

    [Fact]
    public void InstrumentCounter_HandlesEmptyDictionary()
    {
        // Arrange
        var instrumentCounts = new Dictionary<InstrumentType, int>();

        // Act
        var cut = RenderComponent<InstrumentCounter>(parameters => parameters
            .Add(p => p.InstrumentCounts, instrumentCounts));

        // Assert
        cut.Markup.Should().BeEmpty("component should not render with empty dictionary");
    }
}
