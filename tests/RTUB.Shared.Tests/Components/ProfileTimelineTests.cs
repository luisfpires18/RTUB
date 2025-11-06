using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the ProfileTimeline component to ensure timeline displays correctly
/// </summary>
public class ProfileTimelineTests : TestContext
{
    [Fact]
    public void ProfileTimeline_ShowsLeitaoStage()
    {
        // Arrange
        var user = new ApplicationUser
        {
            YearLeitao = 2020,
            Categories = new List<MemberCategory> { MemberCategory.Leitao }
        };

        // Act
        var cut = RenderComponent<ProfileTimeline>(parameters => parameters
            .Add(p => p.User, user));

        // Assert
        cut.Markup.Should().Contain("LEITÃO", "should display Leitão stage");
        cut.Markup.Should().Contain("2020", "should display year");
    }

    [Fact]
    public void ProfileTimeline_ShowsCaloiroStage()
    {
        // Arrange
        var user = new ApplicationUser
        {
            YearLeitao = 2020,
            YearCaloiro = 2021,
            Categories = new List<MemberCategory> { MemberCategory.Caloiro }
        };

        // Act
        var cut = RenderComponent<ProfileTimeline>(parameters => parameters
            .Add(p => p.User, user));

        // Assert
        cut.Markup.Should().Contain("LEITÃO", "should display Leitão stage");
        cut.Markup.Should().Contain("CALOIRO", "should display Caloiro stage");
        cut.Markup.Should().Contain("2021", "should display Caloiro year");
    }

    [Fact]
    public void ProfileTimeline_ShowsTunoStage()
    {
        // Arrange
        var user = new ApplicationUser
        {
            YearLeitao = 2020,
            YearCaloiro = 2021,
            YearTuno = 2022,
            Categories = new List<MemberCategory> { MemberCategory.Tuno }
        };

        // Act
        var cut = RenderComponent<ProfileTimeline>(parameters => parameters
            .Add(p => p.User, user));

        // Assert
        cut.Markup.Should().Contain("LEITÃO", "should display Leitão stage");
        cut.Markup.Should().Contain("CALOIRO", "should display Caloiro stage");
        cut.Markup.Should().Contain("TUNO", "should display Tuno stage");
        cut.Markup.Should().Contain("2022", "should display Tuno year");
    }

    [Fact]
    public void ProfileTimeline_ShowsActiveStageForCurrentCategory()
    {
        // Arrange
        var user = new ApplicationUser
        {
            YearLeitao = 2020,
            YearCaloiro = 2021,
            YearTuno = DateTime.Now.Year - 1,
            Categories = new List<MemberCategory> { MemberCategory.Tuno }
        };

        // Act
        var cut = RenderComponent<ProfileTimeline>(parameters => parameters
            .Add(p => p.User, user));

        // Assert
        cut.Markup.Should().Contain("timeline-item active", "current stage should be marked as active");
    }

    [Fact]
    public void ProfileTimeline_ShowsYearsAsTunoForActiveTuno()
    {
        // Arrange
        var currentYear = DateTime.Now.Year;
        var user = new ApplicationUser
        {
            YearLeitao = currentYear - 5,
            YearCaloiro = currentYear - 4,
            YearTuno = currentYear - 3,
            Categories = new List<MemberCategory> { MemberCategory.Tuno }
        };

        // Act
        var cut = RenderComponent<ProfileTimeline>(parameters => parameters
            .Add(p => p.User, user));

        // Assert
        cut.Markup.Should().Contain("anos)", "should display years as Tuno");
    }

    [Fact]
    public void ProfileTimeline_ShowsNoInformationMessageWhenNoYears()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Categories = new List<MemberCategory> { MemberCategory.Tuno }
        };

        // Act
        var cut = RenderComponent<ProfileTimeline>(parameters => parameters
            .Add(p => p.User, user));

        // Assert
        cut.Markup.Should().Contain("Nenhuma informação de entrada disponível", 
            "should show no information message when no years are set");
    }

    [Fact]
    public void ProfileTimeline_ShowsCompletedStagesForProgressedUser()
    {
        // Arrange
        var user = new ApplicationUser
        {
            YearLeitao = 2020,
            YearCaloiro = 2021,
            YearTuno = 2022,
            Categories = new List<MemberCategory> { MemberCategory.Tuno }
        };

        // Act
        var cut = RenderComponent<ProfileTimeline>(parameters => parameters
            .Add(p => p.User, user));

        // Assert
        cut.Markup.Should().Contain("timeline-item completed", "previous stages should be marked as completed");
    }

    [Fact]
    public void ProfileTimeline_ShowsConnectorBetweenStages()
    {
        // Arrange
        var user = new ApplicationUser
        {
            YearLeitao = 2020,
            YearCaloiro = 2021,
            Categories = new List<MemberCategory> { MemberCategory.Caloiro }
        };

        // Act
        var cut = RenderComponent<ProfileTimeline>(parameters => parameters
            .Add(p => p.User, user));

        // Assert
        cut.Markup.Should().Contain("timeline-connector", "should show connector between stages");
    }
}
