using FluentAssertions;
using RTUB.Shared.Base;

namespace RTUB.Shared.Tests.Base;

/// <summary>
/// Unit tests for MultiModalState
/// </summary>
public class MultiModalStateTests
{
    [Fact]
    public void IsOpen_WhenModalNotOpened_ReturnsFalse()
    {
        // Arrange
        var modals = new MultiModalState<string>();

        // Act
        var result = modals.IsOpen("Edit");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Open_OpensModal()
    {
        // Arrange
        var modals = new MultiModalState<string>();

        // Act
        modals.Open("Edit");

        // Assert
        modals.IsOpen("Edit").Should().BeTrue();
    }

    [Fact]
    public void Open_ClosesOtherModals()
    {
        // Arrange
        var modals = new MultiModalState<string>();
        modals.Open("Edit");

        // Act
        modals.Open("Delete");

        // Assert
        modals.IsOpen("Edit").Should().BeFalse();
        modals.IsOpen("Delete").Should().BeTrue();
    }

    [Fact]
    public void Close_ClosesModal()
    {
        // Arrange
        var modals = new MultiModalState<string>();
        modals.Open("Edit");

        // Act
        modals.Close("Edit");

        // Assert
        modals.IsOpen("Edit").Should().BeFalse();
    }

    [Fact]
    public void CloseAll_ClosesAllModals()
    {
        // Arrange
        var modals = new MultiModalState<string>();
        modals.Open("Edit");
        modals.Open("Delete"); // This closes Edit
        modals.Open("View"); // This closes Delete

        // Act
        modals.CloseAll();

        // Assert
        modals.IsOpen("Edit").Should().BeFalse();
        modals.IsOpen("Delete").Should().BeFalse();
        modals.IsOpen("View").Should().BeFalse();
    }

    [Fact]
    public void Toggle_WhenClosed_OpensModal()
    {
        // Arrange
        var modals = new MultiModalState<string>();

        // Act
        modals.Toggle("Edit");

        // Assert
        modals.IsOpen("Edit").Should().BeTrue();
    }

    [Fact]
    public void Toggle_WhenOpen_ClosesModal()
    {
        // Arrange
        var modals = new MultiModalState<string>();
        modals.Open("Edit");

        // Act
        modals.Toggle("Edit");

        // Assert
        modals.IsOpen("Edit").Should().BeFalse();
    }

    [Fact]
    public void AnyOpen_WhenNoModalsOpen_ReturnsFalse()
    {
        // Arrange
        var modals = new MultiModalState<string>();

        // Act & Assert
        modals.AnyOpen.Should().BeFalse();
    }

    [Fact]
    public void AnyOpen_WhenModalOpen_ReturnsTrue()
    {
        // Arrange
        var modals = new MultiModalState<string>();
        modals.Open("Edit");

        // Act & Assert
        modals.AnyOpen.Should().BeTrue();
    }

    [Fact]
    public void Count_ReturnsNumberOfRegisteredModals()
    {
        // Arrange
        var modals = new MultiModalState<string>();

        // Act
        modals.Open("Edit");
        modals.Open("Delete"); // Closes Edit
        modals.Open("View"); // Closes Delete

        // Assert
        modals.Count.Should().Be(3); // All three modals are registered
    }

    [Fact]
    public void WorksWithEnumKeys()
    {
        // Arrange
        var modals = new MultiModalState<TestModalType>();

        // Act
        modals.Open(TestModalType.Edit);

        // Assert
        modals.IsOpen(TestModalType.Edit).Should().BeTrue();
        modals.IsOpen(TestModalType.Delete).Should().BeFalse();
    }
}

/// <summary>
/// Test enum for MultiModalState tests
/// </summary>
internal enum TestModalType
{
    Edit,
    Delete
}
