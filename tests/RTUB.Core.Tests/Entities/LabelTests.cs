using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

public class LabelTests
{
    #region Create Tests

    [Fact]
    public void Create_WithValidData_ShouldCreateInstance()
    {
        // Arrange
        var reference = "home-welcome";
        var title = "Welcome Title";
        var content = "Welcome content text";

        // Act
        var label = Label.Create(reference, title, content);

        // Assert
        label.Should().NotBeNull();
        label.Reference.Should().Be(reference);
        label.Title.Should().Be(title);
        label.Content.Should().Be(content);
        label.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_WithIsActiveFalse_ShouldCreateInactiveLabel()
    {
        // Arrange
        var reference = "test-ref";
        var title = "Test Title";
        var content = "Test content";

        // Act
        var label = Label.Create(reference, title, content, isActive: false);

        // Assert
        label.IsActive.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WithEmptyReference_ShouldThrowException(string? reference)
    {
        // Act
        var act = () => Label.Create(reference!, "Title", "Content");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*referência*");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WithEmptyTitle_ShouldThrowException(string? title)
    {
        // Act
        var act = () => Label.Create("valid-ref", title!, "Content");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*título*");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WithEmptyContent_ShouldThrowException(string? content)
    {
        // Act
        var act = () => Label.Create("valid-ref", "Title", content!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*conteúdo*");
    }

    #endregion

    #region UpdateContent Tests

    [Fact]
    public void UpdateContent_WithValidData_ShouldUpdateProperties()
    {
        // Arrange
        var label = Label.Create("ref", "Old Title", "Old Content");
        var newTitle = "New Title";
        var newContent = "New Content";

        // Act
        label.UpdateContent(newTitle, newContent, true);

        // Assert
        label.Title.Should().Be(newTitle);
        label.Content.Should().Be(newContent);
        label.IsActive.Should().BeTrue();
        label.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UpdateContent_WithIsActiveFalse_ShouldDeactivate()
    {
        // Arrange
        var label = Label.Create("ref", "Title", "Content");

        // Act
        label.UpdateContent("New Title", "New Content", false);

        // Assert
        label.IsActive.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void UpdateContent_WithEmptyTitle_ShouldThrowException(string? title)
    {
        // Arrange
        var label = Label.Create("ref", "Title", "Content");

        // Act
        var act = () => label.UpdateContent(title!, "Content", true);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*título*");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void UpdateContent_WithEmptyContent_ShouldThrowException(string? content)
    {
        // Arrange
        var label = Label.Create("ref", "Title", "Content");

        // Act
        var act = () => label.UpdateContent("New Title", content!, true);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*conteúdo*");
    }

    #endregion

    #region Activate/Deactivate Tests

    [Fact]
    public void Activate_WhenInactive_ShouldSetIsActiveToTrue()
    {
        // Arrange
        var label = Label.Create("ref", "Title", "Content", isActive: false);

        // Act
        label.Activate();

        // Assert
        label.IsActive.Should().BeTrue();
        label.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Activate_WhenAlreadyActive_ShouldRemainActive()
    {
        // Arrange
        var label = Label.Create("ref", "Title", "Content", isActive: true);

        // Act
        label.Activate();

        // Assert
        label.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Deactivate_WhenActive_ShouldSetIsActiveToFalse()
    {
        // Arrange
        var label = Label.Create("ref", "Title", "Content", isActive: true);

        // Act
        label.Deactivate();

        // Assert
        label.IsActive.Should().BeFalse();
        label.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_ShouldRemainInactive()
    {
        // Arrange
        var label = Label.Create("ref", "Title", "Content", isActive: false);

        // Act
        label.Deactivate();

        // Assert
        label.IsActive.Should().BeFalse();
    }

    #endregion
}
