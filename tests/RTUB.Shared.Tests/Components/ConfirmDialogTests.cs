using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the ConfirmDialog component to ensure confirmation dialogs work correctly
/// </summary>
public class ConfirmDialogTests : BunitContext
{
    public ConfirmDialogTests()
    {
        // Modal (used by ConfirmDialog) injects IJSRuntime for scroll-lock
        // Modal registers with the dialog stack in modalHelper.js (focus, Escape, scroll lock).
        JSInterop.SetupVoid("modalHelper.openDialog", _ => true);
        JSInterop.SetupVoid("modalHelper.closeDialog", _ => true);
    }

    [Fact]
    public void ConfirmDialog_WhenShowIsFalse_DoesNotRender()
    {
        // Arrange & Act
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.Show, false));

        // Assert
        cut.Markup.Should().BeEmpty("dialog should not render when Show is false");
    }

    [Fact]
    public void ConfirmDialog_WhenShowIsTrue_Renders()
    {
        // Arrange & Act
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.Show, true));

        // Assert
        cut.Markup.Should().NotBeEmpty("dialog should render when Show is true");
        cut.Markup.Should().Contain("modal", "dialog should render as a modal");
    }

    [Fact]
    public void ConfirmDialog_RendersDefaultTitle()
    {
        // Arrange & Act
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.Show, true));

        // Assert
        cut.Markup.Should().Contain("Confirmar", "default title should be 'Confirmar'");
    }

    [Fact]
    public void ConfirmDialog_RendersCustomTitle()
    {
        // Arrange
        var customTitle = "Delete Confirmation";

        // Act
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Title, customTitle));

        // Assert
        cut.Markup.Should().Contain(customTitle, "custom title should be displayed");
    }

    [Fact]
    public void ConfirmDialog_RendersMessage_WhenProvided()
    {
        // Arrange
        var message = "Are you sure you want to delete this item?";

        // Act
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Message, message));

        // Assert
        cut.Markup.Should().Contain(message, "message should be displayed");
    }

    [Fact]
    public void ConfirmDialog_RendersWarningMessage_WhenProvided()
    {
        // Arrange
        var warningMessage = "This action cannot be undone.";

        // Act
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Message, "Delete?")
            .Add(p => p.WarningMessage, warningMessage));

        // Assert
        cut.Markup.Should().Contain(warningMessage, "warning message should be displayed");
        cut.Find(".confirm-dialog__warning").TextContent.Should().Be(warningMessage);
    }

    [Fact]
    public void ConfirmDialog_RendersCustomBodyContent()
    {
        // Arrange
        var bodyContent = "Custom body content";

        // Act
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.BodyContent, builder => builder.AddContent(0, bodyContent)));

        // Assert
        cut.Markup.Should().Contain(bodyContent, "custom body content should be displayed");
    }

    [Fact]
    public void ConfirmDialog_RendersDefaultButtonTexts()
    {
        // Arrange & Act
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.Show, true));

        // Assert
        cut.Markup.Should().Contain("Confirmar", "default confirm button text should be displayed");
        cut.Markup.Should().Contain("Cancelar", "default cancel button text should be displayed");
    }

    [Fact]
    public void ConfirmDialog_RendersCustomButtonTexts()
    {
        // Arrange
        var confirmText = "Delete";
        var cancelText = "Keep";

        // Act
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.ConfirmText, confirmText)
            .Add(p => p.CancelText, cancelText));

        // Assert
        cut.Markup.Should().Contain(confirmText, "custom confirm button text should be displayed");
        cut.Markup.Should().Contain(cancelText, "custom cancel button text should be displayed");
    }

    [Fact]
    public void ConfirmDialog_UsesDefaultButtonClass()
    {
        // Arrange & Act
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.Show, true));

        // Assert
        cut.Markup.Should().Contain("btn-primary", "default confirm button should have primary class");
    }

    [Fact]
    public void ConfirmDialog_UsesCustomButtonClass()
    {
        // Arrange
        var customClass = "btn-danger";

        // Act
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.ConfirmButtonClass, customClass));

        // Assert
        cut.Markup.Should().Contain(customClass, "custom button class should be applied");
    }

    [Fact]
    public void ConfirmDialog_ConfirmButton_InvokesOnConfirmCallback()
    {
        // Arrange
        bool onConfirmCalled = false;

        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.ConfirmText, "Confirm")
            .Add(p => p.OnConfirm, EventCallback.Factory.Create(this, () =>
            {
                onConfirmCalled = true;
            })));

        // Act
        var confirmButton = cut.FindAll("button")
            .FirstOrDefault(b => b.TextContent.Contains("Confirm"));
        confirmButton?.Click();

        // Assert
        onConfirmCalled.Should().BeTrue("OnConfirm callback should be invoked when confirm button is clicked");
    }

    [Fact]
    public void ConfirmDialog_ConfirmButton_InvokesShowChangedCallback()
    {
        // Arrange
        bool showChangedCalled = false;
        bool newShowValue = true;

        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.ConfirmText, "Confirm")
            .Add(p => p.ShowChanged, EventCallback.Factory.Create<bool>(this, (value) =>
            {
                showChangedCalled = true;
                newShowValue = value;
            })));

        // Act
        var confirmButton = cut.FindAll("button")
            .FirstOrDefault(b => b.TextContent.Contains("Confirm"));
        confirmButton?.Click();

        // Assert
        showChangedCalled.Should().BeTrue("ShowChanged callback should be invoked");
        newShowValue.Should().BeFalse("ShowChanged should receive false after confirm");
    }

    [Fact]
    public void ConfirmDialog_CancelButton_InvokesOnCancelCallback()
    {
        // Arrange
        bool onCancelCalled = false;

        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.CancelText, "Cancel")
            .Add(p => p.OnCancel, EventCallback.Factory.Create(this, () =>
            {
                onCancelCalled = true;
            })));

        // Act
        var cancelButton = cut.FindAll("button")
            .FirstOrDefault(b => b.TextContent.Contains("Cancel"));
        cancelButton?.Click();

        // Assert
        onCancelCalled.Should().BeTrue("OnCancel callback should be invoked when cancel button is clicked");
    }

    [Fact]
    public void ConfirmDialog_CancelButton_InvokesShowChangedCallback()
    {
        // Arrange
        bool showChangedCalled = false;
        bool newShowValue = true;

        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.CancelText, "Cancel")
            .Add(p => p.ShowChanged, EventCallback.Factory.Create<bool>(this, (value) =>
            {
                showChangedCalled = true;
                newShowValue = value;
            })));

        // Act
        var cancelButton = cut.FindAll("button")
            .FirstOrDefault(b => b.TextContent.Contains("Cancel"));
        cancelButton?.Click();

        // Assert
        showChangedCalled.Should().BeTrue("ShowChanged callback should be invoked");
        newShowValue.Should().BeFalse("ShowChanged should receive false after cancel");
    }

    [Fact]
    public void ConfirmDialog_IsCentered_ByDefault()
    {
        // Arrange & Act
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.Show, true));

        // Assert
        cut.Markup.Should().Contain("modal-dialog-centered", "dialog should be centered by default");
    }

    [Fact]
    public void ConfirmDialog_DoesNotShowCloseButton_ByDefault()
    {
        // Arrange & Act
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.Show, true));

        // Assert — ConfirmDialog defaults ShowCloseButton to false
        cut.Markup.Should().NotContain("modal-close-arrow", "close button should not be shown by default");
    }

    [Fact]
    public void ConfirmDialog_HidesCloseButton_WhenShowCloseButtonIsFalse()
    {
        // Arrange & Act
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.ShowCloseButton, false));

        // Assert
        cut.Markup.Should().NotContain("modal-close-arrow", "close button should be hidden when ShowCloseButton is false");
    }

    [Fact]
    public void ConfirmDialog_AppliesModalSize()
    {
        // Arrange & Act
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Size, Modal.ModalSize.Large));

        // Assert
        cut.Markup.Should().Contain("modal-lg", "dialog should apply the specified modal size");
    }

    [Fact]
    public void ConfirmDialog_CancelButton_IsTheQuietChoice_AndTakesInitialFocus()
    {
        // Arrange & Act
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.Show, true));

        // Assert
        var cancel = cut.FindAll("button").Single(b => b.TextContent.Contains("Cancelar"));
        cancel.ClassList.Should().Contain("btn-outline-secondary", "cancel is quieter than the confirm action");
        cancel.HasAttribute("data-autofocus").Should().BeTrue("the safe choice has focus when the dialog opens");
    }

    [Fact]
    public void ConfirmDialog_BodyContentTakesPrecedence_OverMessage()
    {
        // Arrange
        var bodyContent = "Body content from RenderFragment";
        var message = "Simple message";

        // Act
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Message, message)
            .Add(p => p.BodyContent, builder => builder.AddContent(0, bodyContent)));

        // Assert
        cut.Markup.Should().Contain(bodyContent, "body content should be displayed");
        // The message would still be in the component but not in a <p> tag since BodyContent is provided
    }

    // ---------- intent, busy state and dismissal (UI refactor 035) ----------

    [Fact]
    public void ConfirmDialog_DangerConfirm_IsMarkedDestructive_WithoutAnInfoIcon()
    {
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Message, "Eliminar o ensaio?")
            .Add(p => p.ConfirmButtonClass, "btn-danger"));

        cut.Find(".confirm-dialog").ClassList.Should().Contain("confirm-dialog--danger");
        cut.FindAll(".confirm-dialog__icon .bi-exclamation-triangle-fill").Should().ContainSingle();
        cut.Markup.Should().NotContain("bi-info-circle");

        var buttons = cut.FindAll(".modal-footer button");
        buttons[^1].ClassList.Should().Contain("btn-danger", "the destructive action is last");
    }

    [Fact]
    public void ConfirmDialog_NonDestructiveConfirm_IsAPlainConfirmation()
    {
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Message, "Publicar?")
            .Add(p => p.ConfirmButtonClass, "btn-success"));

        cut.Find(".confirm-dialog").ClassList.Should().Contain("confirm-dialog--default");
    }

    [Fact]
    public void ConfirmDialog_IsDescribedByItsMessage()
    {
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Message, "Eliminar?"));

        var describedBy = cut.Find("[role='dialog']").GetAttribute("aria-describedby");
        cut.Find("#" + describedBy).TextContent.Should().Contain("Eliminar?");
    }

    [Fact]
    public async Task ConfirmDialog_WhileConfirming_BlocksASecondConfirmAndDismissal()
    {
        var pending = new TaskCompletionSource();
        var confirmations = 0;
        var cancellations = 0;
        bool? closedWith = null;
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.ConfirmButtonClass, "btn-danger")
            .Add(p => p.OnConfirm, EventCallback.Factory.Create(this, async () => { confirmations++; await pending.Task; }))
            .Add(p => p.OnCancel, EventCallback.Factory.Create(this, () => cancellations++))
            .Add(p => p.ShowChanged, EventCallback.Factory.Create<bool>(this, value => closedWith = value)));

        var confirm = cut.Find(".modal-footer .btn-danger");
        _ = confirm.ClickAsync(new());

        cut.WaitForAssertion(() => cut.Find(".modal-footer .btn-danger").GetAttribute("aria-busy").Should().Be("true"));
        cut.FindAll(".modal-footer button").Should().OnlyContain(b => b.HasAttribute("disabled"));

        await cut.InvokeAsync(() => cut.FindComponent<Modal>().Instance.HandleEscape());
        confirmations.Should().Be(1);
        cancellations.Should().Be(0, "Escape cannot dismiss while the action runs");
        closedWith.Should().BeNull();

        await cut.InvokeAsync(() => pending.SetResult());
        cut.WaitForAssertion(() => closedWith.Should().BeFalse("the dialog closes once the action finished"));
    }

    [Fact]
    public async Task ConfirmDialog_Escape_Cancels()
    {
        var cancelled = false;
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.OnCancel, EventCallback.Factory.Create(this, () => cancelled = true)));

        JSInterop.VerifyInvoke("modalHelper.openDialog").Arguments[2].Should().Be(true);
        await cut.InvokeAsync(() => cut.FindComponent<Modal>().Instance.HandleEscape());

        cancelled.Should().BeTrue();
    }
}
