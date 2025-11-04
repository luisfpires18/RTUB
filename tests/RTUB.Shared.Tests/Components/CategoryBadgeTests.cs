using Bunit;
using FluentAssertions;
using RTUB.Core.Enums;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the CategoryBadge component to ensure member category badges display correctly
/// </summary>
public class CategoryBadgeTests : TestContext
{
    [Fact]
    public void CategoryBadge_RendersTunoCategory()
    {
        // Arrange & Act
        var cut = RenderComponent<CategoryBadge>(parameters => parameters
            .Add(p => p.Category, MemberCategory.Tuno));

        // Assert
        cut.Markup.Should().Contain("TUNO", "badge should display category name");
        cut.Markup.Should().Contain("badge-tuno", "Tuno category should have correct class");
    }

    [Fact]
    public void CategoryBadge_RendersVeteranoCategory()
    {
        // Arrange & Act
        var cut = RenderComponent<CategoryBadge>(parameters => parameters
            .Add(p => p.Category, MemberCategory.Veterano));

        // Assert
        cut.Markup.Should().Contain("VETERANO", "badge should display category name");
        cut.Markup.Should().Contain("badge-veterano", "Veterano category should have correct class");
    }

    [Fact]
    public void CategoryBadge_RendersTunossauroCategory()
    {
        // Arrange & Act
        var cut = RenderComponent<CategoryBadge>(parameters => parameters
            .Add(p => p.Category, MemberCategory.Tunossauro));

        // Assert
        cut.Markup.Should().Contain("TUNOSSAURO", "badge should display category name");
        cut.Markup.Should().Contain("badge-tunossauro", "Tunossauro category should have correct class");
    }

    [Fact]
    public void CategoryBadge_RendersTunoHonorarioCategory()
    {
        // Arrange & Act
        var cut = RenderComponent<CategoryBadge>(parameters => parameters
            .Add(p => p.Category, MemberCategory.TunoHonorario));

        // Assert
        cut.Markup.Should().Contain("TUNO HONORÁRIO", "badge should display category name with accent");
        cut.Markup.Should().Contain("badge-honorario", "TunoHonorario category should have correct class");
    }

    [Fact]
    public void CategoryBadge_RendersCaloiroCategory()
    {
        // Arrange & Act
        var cut = RenderComponent<CategoryBadge>(parameters => parameters
            .Add(p => p.Category, MemberCategory.Caloiro));

        // Assert
        cut.Markup.Should().Contain("CALOIRO", "badge should display category name");
        cut.Markup.Should().Contain("badge-caloiro", "Caloiro category should have correct class");
    }

    [Fact]
    public void CategoryBadge_RendersLeitaoCategory()
    {
        // Arrange & Act
        var cut = RenderComponent<CategoryBadge>(parameters => parameters
            .Add(p => p.Category, MemberCategory.Leitao));

        // Assert
        cut.Markup.Should().Contain("LEITÃO", "badge should display category name with tilde");
        cut.Markup.Should().Contain("badge-leitao", "Leitao category should have correct class");
    }

    [Fact]
    public void CategoryBadge_HasBadgeClass()
    {
        // Arrange & Act
        var cut = RenderComponent<CategoryBadge>(parameters => parameters
            .Add(p => p.Category, MemberCategory.Tuno));

        // Assert
        cut.Markup.Should().Contain("badge", "should have badge class");
    }

    [Fact]
    public void CategoryBadge_AppliesAdditionalClasses()
    {
        // Arrange
        var additionalClass = "custom-class";

        // Act
        var cut = RenderComponent<CategoryBadge>(parameters => parameters
            .Add(p => p.Category, MemberCategory.Tuno)
            .Add(p => p.AdditionalClasses, additionalClass));

        // Assert
        cut.Markup.Should().Contain(additionalClass, "additional classes should be applied");
    }

    [Fact]
    public void CategoryBadge_RendersAsSpan()
    {
        // Arrange & Act
        var cut = RenderComponent<CategoryBadge>(parameters => parameters
            .Add(p => p.Category, MemberCategory.Veterano));

        // Assert
        cut.Markup.Should().StartWith("<span", "badge should render as a span element");
    }

    [Theory]
    [InlineData(MemberCategory.Tuno, "TUNO", "badge-tuno")]
    [InlineData(MemberCategory.Veterano, "VETERANO", "badge-veterano")]
    [InlineData(MemberCategory.Tunossauro, "TUNOSSAURO", "badge-tunossauro")]
    [InlineData(MemberCategory.TunoHonorario, "TUNO HONORÁRIO", "badge-honorario")]
    [InlineData(MemberCategory.Caloiro, "CALOIRO", "badge-caloiro")]
    [InlineData(MemberCategory.Leitao, "LEITÃO", "badge-leitao")]
    public void CategoryBadge_AppliesCorrectStyleAndText_ForEachCategory(
        MemberCategory category, string expectedText, string expectedClass)
    {
        // Arrange & Act
        var cut = RenderComponent<CategoryBadge>(parameters => parameters
            .Add(p => p.Category, category));

        // Assert
        cut.Markup.Should().Contain(expectedText, $"category {category} should display {expectedText}");
        cut.Markup.Should().Contain(expectedClass, $"category {category} should have {expectedClass}");
    }
}
