using Bunit;
using FluentAssertions;
using RTUB.Core.Enums;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the StatusBadge component to ensure request status badges display correctly
/// </summary>
public class StatusBadgeTests : TestContext
{
    [Fact]
    public void StatusBadge_RendersPendingStatus()
    {
        // Arrange & Act
        var cut = RenderComponent<StatusBadge>(parameters => parameters
            .Add(p => p.Status, RequestStatus.Pending));

        // Assert
        cut.Markup.Should().Contain("Pendente", "badge should display Portuguese translation");
        cut.Markup.Should().Contain("bg-warning", "pending status should have warning background");
    }

    [Fact]
    public void StatusBadge_RendersAnalysingStatus()
    {
        // Arrange & Act
        var cut = RenderComponent<StatusBadge>(parameters => parameters
            .Add(p => p.Status, RequestStatus.Analysing));

        // Assert
        cut.Markup.Should().Contain("Em Análise", "badge should display Portuguese translation");
        cut.Markup.Should().Contain("bg-info", "analysing status should have info background");
    }

    [Fact]
    public void StatusBadge_RendersConfirmedStatus()
    {
        // Arrange & Act
        var cut = RenderComponent<StatusBadge>(parameters => parameters
            .Add(p => p.Status, RequestStatus.Confirmed));

        // Assert
        cut.Markup.Should().Contain("Confirmado", "badge should display Portuguese translation");
        cut.Markup.Should().Contain("bg-success", "confirmed status should have success background");
    }

    [Fact]
    public void StatusBadge_RendersRejectedStatus()
    {
        // Arrange & Act
        var cut = RenderComponent<StatusBadge>(parameters => parameters
            .Add(p => p.Status, RequestStatus.Rejected));

        // Assert
        cut.Markup.Should().Contain("Rejeitado", "badge should display Portuguese translation");
        cut.Markup.Should().Contain("bg-danger", "rejected status should have danger background");
    }

    [Fact]
    public void StatusBadge_HasBadgeClass()
    {
        // Arrange & Act
        var cut = RenderComponent<StatusBadge>(parameters => parameters
            .Add(p => p.Status, RequestStatus.Pending));

        // Assert
        cut.Markup.Should().Contain("badge", "should have badge class");
    }

    [Fact]
    public void StatusBadge_RendersAsSpan()
    {
        // Arrange & Act
        var cut = RenderComponent<StatusBadge>(parameters => parameters
            .Add(p => p.Status, RequestStatus.Confirmed));

        // Assert
        cut.Markup.Should().StartWith("<span", "badge should render as a span element");
    }

    [Theory]
    [InlineData(RequestStatus.Pending, "Pendente", "bg-warning")]
    [InlineData(RequestStatus.Analysing, "Em Análise", "bg-info")]
    [InlineData(RequestStatus.Confirmed, "Confirmado", "bg-success")]
    [InlineData(RequestStatus.Rejected, "Rejeitado", "bg-danger")]
    public void StatusBadge_AppliesCorrectStyleAndText_ForEachStatus(
        RequestStatus status, string expectedText, string expectedClass)
    {
        // Arrange & Act
        var cut = RenderComponent<StatusBadge>(parameters => parameters
            .Add(p => p.Status, status));

        // Assert
        cut.Markup.Should().Contain(expectedText, $"status {status} should display {expectedText}");
        cut.Markup.Should().Contain(expectedClass, $"status {status} should have {expectedClass}");
    }
}
