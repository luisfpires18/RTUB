using FluentAssertions;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Base;

/// <summary>
/// Tests for MultiModalState to ensure proper modal state management functionality
/// </summary>
public class MultiModalStateTests
{
    // Test enum representing different modal types
    private enum TestModal { Edit, Delete, ViewDetails, Settings }

    [Fact]
    public void IsOpen_ReturnsFalse_WhenModalNeverOpened()
    {
        // Arrange
        var modals = new MultiModalState<TestModal>();

        // Act & Assert
        modals.IsOpen(TestModal.Edit).Should().BeFalse("modal should be closed by default");
        modals.IsOpen(TestModal.Delete).Should().BeFalse("modal should be closed by default");
    }

    [Fact]
    public void Open_SetsModalToOpen()
    {
        // Arrange
        var modals = new MultiModalState<TestModal>();

        // Act
        modals.Open(TestModal.Edit);

        // Assert
        modals.IsOpen(TestModal.Edit).Should().BeTrue("modal should be open after calling Open");
    }

    [Fact]
    public void Open_ClosesOtherModals_WhenOpeningNew()
    {
        // Arrange
        var modals = new MultiModalState<TestModal>();
        modals.Open(TestModal.Edit);

        // Act
        modals.Open(TestModal.Delete);

        // Assert
        modals.IsOpen(TestModal.Edit).Should().BeFalse("Edit modal should be closed when Delete modal is opened");
        modals.IsOpen(TestModal.Delete).Should().BeTrue("Delete modal should be open");
    }

    [Fact]
    public void Close_SetsModalToClosed()
    {
        // Arrange
        var modals = new MultiModalState<TestModal>();
        modals.Open(TestModal.Edit);

        // Act
        modals.Close(TestModal.Edit);

        // Assert
        modals.IsOpen(TestModal.Edit).Should().BeFalse("modal should be closed after calling Close");
    }

    [Fact]
    public void Close_DoesNotAffectOtherModals()
    {
        // Arrange
        var modals = new MultiModalState<TestModal>();
        modals.Open(TestModal.Edit);
        modals.Open(TestModal.Delete); // This closes Edit and opens Delete

        // Act
        modals.Close(TestModal.Edit); // Close already-closed modal

        // Assert
        modals.IsOpen(TestModal.Delete).Should().BeTrue("Delete modal should remain unaffected");
    }

    [Fact]
    public void CloseAll_ClosesAllOpenModals()
    {
        // Arrange
        var modals = new MultiModalState<TestModal>();
        modals.Open(TestModal.Edit);
        modals.Open(TestModal.Delete);
        modals.Open(TestModal.ViewDetails);

        // Act
        modals.CloseAll();

        // Assert
        modals.IsOpen(TestModal.Edit).Should().BeFalse("Edit modal should be closed");
        modals.IsOpen(TestModal.Delete).Should().BeFalse("Delete modal should be closed");
        modals.IsOpen(TestModal.ViewDetails).Should().BeFalse("ViewDetails modal should be closed");
    }

    [Fact]
    public void Toggle_OpensClosedModal()
    {
        // Arrange
        var modals = new MultiModalState<TestModal>();

        // Act
        modals.Toggle(TestModal.Edit);

        // Assert
        modals.IsOpen(TestModal.Edit).Should().BeTrue("modal should be open after toggling from closed");
    }

    [Fact]
    public void Toggle_ClosesOpenModal()
    {
        // Arrange
        var modals = new MultiModalState<TestModal>();
        modals.Open(TestModal.Edit);

        // Act
        modals.Toggle(TestModal.Edit);

        // Assert
        modals.IsOpen(TestModal.Edit).Should().BeFalse("modal should be closed after toggling from open");
    }

    [Fact]
    public void Toggle_ClosesOtherModals_WhenTogglingClosed()
    {
        // Arrange
        var modals = new MultiModalState<TestModal>();
        modals.Open(TestModal.Edit);

        // Act
        modals.Toggle(TestModal.Delete); // Toggle Delete (from closed to open)

        // Assert
        modals.IsOpen(TestModal.Edit).Should().BeFalse("Edit modal should be closed when Delete is toggled open");
        modals.IsOpen(TestModal.Delete).Should().BeTrue("Delete modal should be open");
    }

    [Fact]
    public void OnStateChanged_IsInvoked_WhenOpeningModal()
    {
        // Arrange
        var modals = new MultiModalState<TestModal>();
        var stateChangedCount = 0;
        modals.OnStateChanged += () => stateChangedCount++;

        // Act
        modals.Open(TestModal.Edit);

        // Assert
        stateChangedCount.Should().Be(1, "OnStateChanged should be invoked once when opening a modal");
    }

    [Fact]
    public void OnStateChanged_IsInvoked_WhenClosingModal()
    {
        // Arrange
        var modals = new MultiModalState<TestModal>();
        modals.Open(TestModal.Edit);
        var stateChangedCount = 0;
        modals.OnStateChanged += () => stateChangedCount++;

        // Act
        modals.Close(TestModal.Edit);

        // Assert
        stateChangedCount.Should().Be(1, "OnStateChanged should be invoked once when closing a modal");
    }

    [Fact]
    public void OnStateChanged_IsInvoked_WhenCallingCloseAll()
    {
        // Arrange
        var modals = new MultiModalState<TestModal>();
        modals.Open(TestModal.Edit);
        var stateChangedCount = 0;
        modals.OnStateChanged += () => stateChangedCount++;

        // Act
        modals.CloseAll();

        // Assert
        stateChangedCount.Should().Be(1, "OnStateChanged should be invoked once when calling CloseAll");
    }

    [Fact]
    public void OnStateChanged_IsInvoked_WhenToggling()
    {
        // Arrange
        var modals = new MultiModalState<TestModal>();
        var stateChangedCount = 0;
        modals.OnStateChanged += () => stateChangedCount++;

        // Act
        modals.Toggle(TestModal.Edit);
        modals.Toggle(TestModal.Edit);

        // Assert
        stateChangedCount.Should().Be(2, "OnStateChanged should be invoked twice for two toggle operations");
    }

    [Fact]
    public void OnStateChanged_NotInvoked_WhenNoSubscribers()
    {
        // Arrange
        var modals = new MultiModalState<TestModal>();

        // Act & Assert - Should not throw
        Action act = () =>
        {
            modals.Open(TestModal.Edit);
            modals.Close(TestModal.Edit);
            modals.Toggle(TestModal.Edit);
            modals.CloseAll();
        };
        act.Should().NotThrow("operations should work without event subscribers");
    }

    [Fact]
    public void WorksWithStringKeys()
    {
        // Arrange
        var modals = new MultiModalState<string>();

        // Act
        modals.Open("edit");
        modals.Open("delete");

        // Assert
        modals.IsOpen("edit").Should().BeFalse("edit should be closed when delete is opened");
        modals.IsOpen("delete").Should().BeTrue("delete should be open");
    }

    [Fact]
    public void WorksWithIntKeys()
    {
        // Arrange
        var modals = new MultiModalState<int>();

        // Act
        modals.Open(1);
        modals.Open(2);

        // Assert
        modals.IsOpen(1).Should().BeFalse("1 should be closed when 2 is opened");
        modals.IsOpen(2).Should().BeTrue("2 should be open");
    }

    [Fact]
    public void MultipleSubscribers_AllReceiveNotifications()
    {
        // Arrange
        var modals = new MultiModalState<TestModal>();
        var subscriber1Count = 0;
        var subscriber2Count = 0;
        modals.OnStateChanged += () => subscriber1Count++;
        modals.OnStateChanged += () => subscriber2Count++;

        // Act
        modals.Open(TestModal.Edit);

        // Assert
        subscriber1Count.Should().Be(1, "first subscriber should receive notification");
        subscriber2Count.Should().Be(1, "second subscriber should receive notification");
    }

    [Fact]
    public void Open_InvokesSingleNotification_EvenWhenClosingOtherModals()
    {
        // Arrange
        var modals = new MultiModalState<TestModal>();
        modals.Open(TestModal.Edit);
        modals.Open(TestModal.Delete);
        var stateChangedCount = 0;
        modals.OnStateChanged += () => stateChangedCount++;

        // Act
        modals.Open(TestModal.ViewDetails);

        // Assert
        stateChangedCount.Should().Be(1, "Open should invoke OnStateChanged only once, not for each closed modal");
    }
}
