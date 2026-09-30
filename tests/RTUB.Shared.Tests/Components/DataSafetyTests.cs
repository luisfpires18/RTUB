using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Forms, dialogs and data safety (UI refactor 037, docs/design/RTUB_UI_REFACTOR.md section 24):
/// what counts as unsaved, the discard question on every dismissal path, Back, busy state and the
/// shared failure nets.
/// </summary>
public class DataSafetyTests : BunitContext
{
    public DataSafetyTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // ---------- UnsavedChanges

    [Fact]
    public void UnsavedChanges_IsDirtyOnlyWhileValuesDifferFromTheBaseline()
    {
        var model = new Sample { Name = "Sede" };
        var changes = new UnsavedChanges();
        changes.Track(() => new { model.Name, model.Notes });

        changes.IsDirty.Should().BeFalse("a form that merely opened is clean");
        model.Name = "Auditório";
        changes.IsDirty.Should().BeTrue();
        model.Name = "Sede";
        changes.IsDirty.Should().BeFalse("typing and undoing is not a change");
    }

    [Fact]
    public void UnsavedChanges_MarkCleanAndClear()
    {
        var model = new Sample { Name = "A" };
        var changes = new UnsavedChanges();
        changes.Track(() => model);

        model.Name = "B";
        changes.MarkClean();
        changes.IsDirty.Should().BeFalse("saved values are the new baseline");

        model.Name = "C";
        changes.Clear();
        changes.IsDirty.Should().BeFalse("a closed form has nothing to lose");
        changes.IsTracking.Should().BeFalse();
    }

    // ---------- BusyState

    [Fact]
    public async Task BusyState_IgnoresASecondActivation_AndResetsAfterSuccess()
    {
        var busy = new BusyState();
        var gate = new TaskCompletionSource();
        var runs = 0;

        var first = busy.RunAsync(async () => { runs++; await gate.Task; });
        var second = await busy.RunAsync(() => { runs++; return Task.CompletedTask; });

        busy.IsBusy.Should().BeTrue();
        second.Should().BeFalse("a double click while saving does not save twice");
        gate.SetResult();
        (await first).Should().BeTrue();
        runs.Should().Be(1);
        busy.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task BusyState_ResetsAfterFailure()
    {
        var busy = new BusyState();

        var act = () => busy.RunAsync(() => throw new InvalidOperationException("falhou"));

        await act.Should().ThrowAsync<InvalidOperationException>();
        busy.IsBusy.Should().BeFalse("a failed save leaves Guardar usable again");
    }

    // ---------- Modal + UnsavedChangesGuard

    [Fact]
    public async Task CleanDialog_ClosesAtOnce()
    {
        var host = RenderDialog(dirty: false);

        await host.InvokeAsync(() => host.Instance.Dialog!.RequestCloseAsync());

        host.Instance.Closed.Should().Be(1);
        host.FindAll("[role=dialog]").Should().BeEmpty();
    }

    [Fact]
    public async Task DirtyDialog_Close_AsksFirst_ContinuarKeepsItOpen()
    {
        var host = RenderDialog(dirty: true);

        var closing = host.InvokeAsync(() => host.Instance.Dialog!.RequestCloseAsync());

        host.WaitForAssertion(() => host.Find("[role=dialog] .modal-title").TextContent.Should().Be("Editar"));
        var question = host.FindAll(".modal-title").Single(t => t.TextContent == "Descartar alterações?");
        question.Should().NotBeNull();
        host.Find("button[data-autofocus]").TextContent.Should().Contain("Continuar a editar", "the safe choice has focus");

        host.Find("button[data-autofocus]").Click();

        (await closing).Should().BeFalse();
        host.Instance.Closed.Should().Be(0);
        host.FindAll(".modal-title").Select(t => t.TextContent).Should().Equal("Editar");
    }

    [Fact]
    public async Task DirtyDialog_Descartar_Closes()
    {
        var host = RenderDialog(dirty: true);

        var closing = host.InvokeAsync(() => host.Instance.Dialog!.RequestCloseAsync());
        host.WaitForElement("button.btn-danger");
        host.Find("button.btn-danger").Click();

        (await closing).Should().BeTrue();
        host.Instance.Closed.Should().Be(1);
    }

    [Fact]
    public void DirtyDialog_CloseButton_AsksFirst()
    {
        var host = RenderDialog(dirty: true);

        host.Find("button.modal-close").Click();

        host.FindAll(".modal-title").Select(t => t.TextContent).Should().Contain("Descartar alterações?");
        host.Instance.Closed.Should().Be(0);
    }

    [Fact]
    public void Back_OnADirtyDialog_AsksLikeEscape()
    {
        var host = RenderDialog(dirty: true);

        // Pending until the user answers, as the JS call is.
        var back = host.InvokeAsync(() => host.Instance.Dialog!.HandleBack());

        back.IsCompleted.Should().BeFalse();
        host.WaitForAssertion(() =>
            host.FindAll(".modal-title").Select(t => t.TextContent).Should().Contain("Descartar alterações?"));
        host.Instance.Closed.Should().Be(0);
    }

    [Fact]
    public async Task Back_OnACleanDialog_ClosesIt()
    {
        var host = RenderDialog(dirty: false);

        await host.Instance.Dialog!.HandleBack();

        host.Instance.Closed.Should().Be(1);
    }

    [Fact]
    public async Task Back_OnANonDismissibleDialog_KeepsIt()
    {
        var host = RenderDialog(dirty: false, dismissible: false);

        await host.Instance.Dialog!.HandleBack();

        host.Instance.Closed.Should().Be(0);
        host.FindAll("[role=dialog]").Should().ContainSingle();
    }

    [Fact]
    public async Task CanCloseFalse_WinsBeforeTheDiscardQuestion()
    {
        var host = RenderDialog(dirty: true, canClose: false);

        (await host.InvokeAsync(() => host.Instance.Dialog!.RequestCloseAsync())).Should().BeFalse();

        host.FindAll(".modal-title").Select(t => t.TextContent).Should().NotContain("Descartar alterações?",
            "a dialog busy saving is not asked about discarding");
    }

    [Fact]
    public async Task Guard_ConfirmLeave_ContinueStays_DiscardRunsOnDiscard()
    {
        var discarded = 0;
        var dirty = true;
        var cut = Render<UnsavedChangesGuard>(p => p
            .Add(x => x.IsDirty, () => dirty)
            .Add(x => x.OnDiscard, EventCallback.Factory.Create(this, () => discarded++)));

        var stay = cut.InvokeAsync(() => cut.Instance.ConfirmLeave());
        cut.WaitForElement("button[data-autofocus]").Click();
        (await stay).Should().BeFalse();

        var leave = cut.InvokeAsync(() => cut.Instance.ConfirmLeave());
        cut.WaitForElement("button.btn-danger").Click();
        (await leave).Should().BeTrue();
        discarded.Should().Be(1);

        dirty = false;
        (await cut.InvokeAsync(() => cut.Instance.ConfirmLeave())).Should().BeTrue("a clean form never asks");
    }

    // ---------- Shared failure nets

    [Fact]
    public void ConfirmDialog_FailingConfirm_StaysOpenWithAMessage_AndUnlocks()
    {
        var shown = true;
        var calls = 0;
        var cut = Render<ConfirmDialog>(p => p
            .Add(x => x.Show, shown)
            .Add(x => x.Title, "Eliminar ensaio")
            .Add(x => x.Message, "Eliminar?")
            .Add(x => x.ConfirmButtonClass, "btn-danger")
            .Add(x => x.ShowChanged, EventCallback.Factory.Create<bool>(this, v => shown = v))
            .Add(x => x.OnConfirm, EventCallback.Factory.Create(this, () =>
            {
                calls++;
                throw new InvalidOperationException("SQLite Error 19: FOREIGN KEY constraint failed");
            })));

        cut.Find("button.btn-danger").Click();

        shown.Should().BeTrue("a failed delete does not look done");
        var alert = cut.Find("[role=alert]");
        alert.TextContent.Should().Contain("Não foi possível concluir a operação");
        cut.Markup.Should().NotContain("SQLite", "internal details are never shown");
        cut.Find("button.btn-danger").HasAttribute("disabled").Should().BeFalse("the user can retry or cancel");
        calls.Should().Be(1);
    }

    [Fact]
    public async Task ConfirmDialog_DoubleConfirm_RunsOnce()
    {
        var gate = new TaskCompletionSource();
        var calls = 0;
        var cut = Render<ConfirmDialog>(p => p
            .Add(x => x.Show, true)
            .Add(x => x.ConfirmButtonClass, "btn-danger")
            .Add(x => x.OnConfirm, EventCallback.Factory.Create(this, async () => { calls++; await gate.Task; })));

        var confirm = cut.Find("button.btn-danger");
        _ = confirm.ClickAsync(new());
        cut.Find("button.btn-danger").HasAttribute("disabled").Should().BeTrue();
        await cut.InvokeAsync(() => cut.Find("button.btn-danger").Click());
        gate.SetResult();

        calls.Should().Be(1);
    }

    [Fact]
    public void CrudModalManager_FailingSave_KeepsTheDialogAndItsData()
    {
        var entity = new Sample { Name = "Novo" };
        var cut = Render<CrudModalManager<Sample>>(p => p
            .Add(x => x.ShowEditModal, true)
            .Add(x => x.EditingEntity, entity)
            .Add(x => x.OnSave, EventCallback.Factory.Create(this, () => throw new TimeoutException("db timeout"))));

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Guardar").Click();

        cut.Find("[role=alert]").TextContent.Should().Contain("Não foi possível guardar");
        cut.Markup.Should().NotContain("db timeout");
        cut.FindAll("[role=dialog]").Should().ContainSingle();
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Guardar").HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public void CrudModalManager_EditedEntity_CancelAsks()
    {
        var entity = new Sample { Name = "Original" };
        var cut = Render<CrudModalManager<Sample>>(p => p
            .Add(x => x.ShowEditModal, true)
            .Add(x => x.EditingEntity, entity));

        entity.Name = "Alterado";
        cut.Render();
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Cancelar").Click();

        cut.FindAll(".modal-title").Select(t => t.TextContent).Should().Contain("Descartar alterações?");
    }

    [Fact]
    public void SearchBar_FailingSearch_GoesToTheErrorBoundary_NotTheTimerThread()
    {
        // A failing OnSearch used to throw on the debounce timer's thread, which ends the server
        // process. It must reach the page's AppErrorBoundary instead.
        var cut = Render<SearchInBoundary>();

        cut.Find("input").Input("reunião");

        cut.WaitForAssertion(() => cut.FindAll(".app-error").Should().ContainSingle(), TimeSpan.FromSeconds(10));
    }

    private sealed class SearchInBoundary : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<AppErrorBoundary>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(content =>
            {
                content.OpenComponent<SearchBar>(0);
                content.AddAttribute(1, nameof(SearchBar.DebounceDelay), 10);
                content.AddAttribute(2, nameof(SearchBar.OnSearch), EventCallback.Factory.Create<string>(this,
                    (Func<string, Task>)(_ => throw new InvalidOperationException("could not be translated"))));
                content.CloseComponent();
            }));
            builder.CloseComponent();
        }
    }

    // ---------- helpers

    public sealed class Sample
    {
        public string? Name { get; set; }
        public string? Notes { get; set; }
    }

    private IRenderedComponent<DialogHost> RenderDialog(bool dirty, bool dismissible = true, bool canClose = true) =>
        Render<DialogHost>(p => p
            .Add(x => x.Dirty, dirty)
            .Add(x => x.Dismissible, dismissible)
            .Add(x => x.Closable, canClose));

    /// <summary>A page with one edit dialog, as pages write it.</summary>
    public sealed class DialogHost : ComponentBase
    {
        [Parameter] public bool Dirty { get; set; }
        [Parameter] public bool Dismissible { get; set; } = true;
        [Parameter] public bool Closable { get; set; } = true;

        public Modal? Dialog { get; private set; }
        public int Closed { get; private set; }
        private bool _show = true;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<Modal>(0);
            builder.AddAttribute(1, nameof(Modal.Show), _show);
            builder.AddAttribute(2, nameof(Modal.Title), "Editar");
            builder.AddAttribute(3, nameof(Modal.ShowCloseButton), Dismissible);
            builder.AddAttribute(4, nameof(Modal.IsDirty), (Func<bool>)(() => Dirty));
            builder.AddAttribute(5, nameof(Modal.CanClose), (Func<Task<bool>>)(() => Task.FromResult(Closable)));
            builder.AddAttribute(6, nameof(Modal.ShowChanged), EventCallback.Factory.Create<bool>(this, v =>
            {
                _show = v;
                if (!v) Closed++;
            }));
            builder.AddComponentReferenceCapture(7, r => Dialog = (Modal)r);
            builder.CloseComponent();
        }
    }
}
