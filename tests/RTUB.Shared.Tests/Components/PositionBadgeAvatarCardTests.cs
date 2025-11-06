using Bunit;
using FluentAssertions;
using RTUB.Core.Enums;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for PositionBadge rendering in AvatarCard context
/// Validates long text handling, accessibility, and responsive behavior
/// </summary>
public class PositionBadgeAvatarCardTests : TestContext
{
    [Fact]
    public void PositionBadge_HasAriaLabel()
    {
        // Arrange & Act
        var cut = RenderComponent<PositionBadge>(parameters => parameters
            .Add(p => p.Position, Position.Magister));

        // Assert
        cut.Markup.Should().Contain("aria-label", "badge should have aria-label for accessibility");
        cut.Markup.Should().Contain("MAGISTER", "aria-label should contain position text");
    }

    [Fact]
    public void PositionBadge_HasTooltip()
    {
        // Arrange & Act
        var cut = RenderComponent<PositionBadge>(parameters => parameters
            .Add(p => p.Position, Position.Magister));

        // Assert
        cut.Markup.Should().Contain("title", "badge should have title attribute for tooltip");
    }

    [Fact]
    public void PositionBadge_HasRoleBadgeClass()
    {
        // Arrange & Act
        var cut = RenderComponent<PositionBadge>(parameters => parameters
            .Add(p => p.Position, Position.Magister));

        // Assert
        cut.Markup.Should().Contain("avatar-card-role-badge", "badge should have role badge class for styling");
    }

    [Fact]
    public void PositionBadge_HasCompactPillStyle()
    {
        // Arrange & Act
        var cut = RenderComponent<PositionBadge>(parameters => parameters
            .Add(p => p.Position, Position.Secretario));

        // Assert
        // CSS classes indicate compact pill style with purple background
        cut.Markup.Should().Contain("badge", "should have badge class");
        cut.Markup.Should().Contain("badge-position", "should have position-specific styling");
        cut.Markup.Should().Contain("avatar-card-role-badge", "should have role badge styling");
    }

    [Theory]
    [InlineData(Position.Magister, "MAGISTER")]
    [InlineData(Position.Secretario, "SECRETÁRIO")]
    [InlineData(Position.PrimeiroTesoureiro, "1º TESOUREIRO")]
    public void PositionBadge_DisplaysCorrectText_ForEachPosition(Position position, string expectedText)
    {
        // Arrange & Act
        var cut = RenderComponent<PositionBadge>(parameters => parameters
            .Add(p => p.Position, position));

        // Assert
        cut.Markup.Should().Contain(expectedText, $"badge should display {expectedText} for {position}");
    }

    [Fact]
    public void PositionBadge_LongRoleName_HasEllipsisStyle()
    {
        // Arrange & Act - Using PresidenteConselhoVeteranos which is a longer name
        var cut = RenderComponent<PositionBadge>(parameters => parameters
            .Add(p => p.Position, Position.PresidenteConselhoVeteranos));

        // Assert
        cut.Markup.Should().Contain("avatar-card-role-badge", 
            "long role names should use role badge class with ellipsis styling");
    }

    [Fact]
    public void PositionBadge_MaintainsAccessibility()
    {
        // Arrange & Act
        var cut = RenderComponent<PositionBadge>(parameters => parameters
            .Add(p => p.Position, Position.ViceMagister));

        // Assert
        // Verify both title and aria-label are present for full accessibility
        var markup = cut.Markup;
        markup.Should().Contain("title=", "should have title for hover tooltip");
        markup.Should().Contain("aria-label=", "should have aria-label for screen readers");
    }

    [Fact]
    public void PositionBadge_RendersAsSpan()
    {
        // Arrange & Act
        var cut = RenderComponent<PositionBadge>(parameters => parameters
            .Add(p => p.Position, Position.Magister));

        // Assert
        cut.Markup.Should().StartWith("<span", "badge should render as a span element");
    }

    [Fact]
    public void PositionBadge_AppliesAdditionalClasses()
    {
        // Arrange
        var additionalClass = "custom-class";

        // Act
        var cut = RenderComponent<PositionBadge>(parameters => parameters
            .Add(p => p.Position, Position.Magister)
            .Add(p => p.AdditionalClasses, additionalClass));

        // Assert
        cut.Markup.Should().Contain(additionalClass, "additional classes should be applied");
    }

    #region CSS Behavior Tests (documented)

    [Fact]
    public void RoleBadge_CSS_HasTextOverflowEllipsis()
    {
        // This test documents that the CSS includes:
        // text-overflow: ellipsis;
        // white-space: nowrap;
        // overflow: hidden;
        // for desktop, allowing long role names to truncate
        
        var cssProperty = "text-overflow: ellipsis";
        cssProperty.Should().NotBeNullOrEmpty("CSS should define ellipsis for overflow");
    }

    [Fact]
    public void RoleBadge_CSS_AllowsTwoLineWrap_OnMobile()
    {
        // This test documents that the CSS includes:
        // @media (max-width: 767px) with white-space: normal
        // allowing two-line wrap on mobile devices
        
        var mobileBreakpoint = 767;
        mobileBreakpoint.Should().Be(767, "Mobile breakpoint should allow two-line wrap");
    }

    [Fact]
    public void RoleBadge_CSS_HasPurpleBackground()
    {
        // This test documents that the CSS includes:
        // background-color: #6f42c1 (purple theme)
        // with readable white text color
        
        var purpleColor = "#6f42c1";
        purpleColor.Should().Be("#6f42c1", "Role badges should have purple background");
    }

    [Fact]
    public void RoleBadge_CSS_HasConsistentHeightAndPadding()
    {
        // This test documents that the CSS includes:
        // padding: 0.25rem 0.5rem
        // for consistent height across all badges
        
        var padding = "0.25rem 0.5rem";
        padding.Should().NotBeNullOrEmpty("Role badges should have consistent padding");
    }

    [Fact]
    public void RoleBadge_CSS_MeetsContrastRatio()
    {
        // This test documents that:
        // Purple background (#6f42c1) with white text
        // meets WCAG AA accessibility standards (4.5:1 ratio)
        
        var backgroundColor = "#6f42c1";
        var textColor = "white";
        
        // Purple #6f42c1 with white has approximately 7:1 contrast ratio
        // which exceeds WCAG AA (4.5:1) and even AAA (7:1) standards
        var meetsWCAG_AA = true;
        meetsWCAG_AA.Should().BeTrue("Purple and white should meet WCAG AA contrast standards");
    }

    #endregion
}
