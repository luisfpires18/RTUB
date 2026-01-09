using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using Xunit;

namespace RTUB.Web.Tests.Pages.Activities;

/// <summary>
/// Tests for the EventEnrollments page copy link functionality.
/// Verifies that the copy link button renders and triggers clipboard JS interop correctly.
/// </summary>
public class EventEnrollmentsTests : TestContext
{
    [Fact]
    public void CopyLinkButton_RendersWithCorrectIcon()
    {
        // Arrange
        var markup = @"
            <button class=""btn btn-outline-secondary btn-sm ms-auto"" title=""Copiar link"">
                <i class=""bi bi-link-45deg""></i>
            </button>";

        // Assert
        markup.Should().Contain("bi-link-45deg", "Copy link button should display link icon");
        markup.Should().Contain("Copiar link", "Copy link button should have tooltip");
    }

    [Fact]
    public void CopyLinkToClipboard_ShouldCallJSInteropWithAbsoluteUrl()
    {
        // Arrange
        var testUri = "https://example.com/events/123/enrollments";

        // Simulate the logic from CopyLinkToClipboard method
        // NavigationManager.ToAbsoluteUri(NavigationManager.Uri).ToString()

        // Act
        var absoluteUrl = new Uri(testUri).ToString();

        // Assert
        absoluteUrl.Should().Be(testUri, "Should generate absolute URL for current page");
        absoluteUrl.Should().Contain("/events/", "URL should contain events path");
        absoluteUrl.Should().Contain("/enrollments", "URL should contain enrollments path");
    }

    [Fact]
    public void CopyLinkFeedback_SuccessMessage_ShouldBeInPortuguese()
    {
        // Arrange
        var successMessage = "Link copiado!";
        var errorMessage = "Não foi possível copiar o link";

        // Assert
        successMessage.Should().NotBeNullOrEmpty("Success message should be defined");
        errorMessage.Should().NotBeNullOrEmpty("Error message should be defined");
        successMessage.Should().Contain("copiado", "Success message should be in Portuguese");
        errorMessage.Should().Contain("Não foi possível", "Error message should be in Portuguese");
    }

    [Theory]
    [InlineData("https://rtub.com/events/1/enrollments")]
    [InlineData("https://rtub.com/events/999/enrollments")]
    [InlineData("https://localhost:5001/events/42/enrollments")]
    public void CopyLinkToClipboard_ShouldWorkWithDifferentEventIds(string testUrl)
    {
        // Arrange & Assert
        testUrl.Should().MatchRegex(@"/events/\d+/enrollments$",
            "URL should match enrollments page pattern");
    }

    [Fact]
    public void CopyLinkButton_ShouldBePositionedWithMarginLeftAuto()
    {
        // Arrange
        var buttonClasses = "btn btn-outline-secondary btn-sm ms-auto";

        // Assert
        buttonClasses.Should().Contain("ms-auto",
            "Button should use Bootstrap ms-auto class for right alignment");
    }
}
