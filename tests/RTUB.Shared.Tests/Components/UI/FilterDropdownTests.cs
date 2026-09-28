using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components.UI;

/// <summary>
/// FilterDropdown contract (UI refactor 036, docs/design/RTUB_UI_REFACTOR.md section 23.7):
/// named trigger, listbox with active descendant and marked selection, keyboard, click-away.
/// </summary>
public class FilterDropdownTests : BunitContext
{
    private static readonly FilterDropdownOption[] Years =
    [
        new("2025-2026", "2025-2026"),
        new("2024-2025", "2024-2025")
    ];

    public FilterDropdownTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<FilterDropdown> RenderYears(string selected = "", Action<string>? onChanged = null) =>
        Render<FilterDropdown>(p => p
            .Add(x => x.Label, "Ano letivo")
            .Add(x => x.AllItemsLabel, "Todos os anos")
            .Add(x => x.Options, Years)
            .Add(x => x.SelectedValue, selected)
            .Add(x => x.SelectedValueChanged, EventCallback.Factory.Create<string>(this, v => onChanged?.Invoke(v))));

    [Fact]
    public void Trigger_IsNamedByLabelAndCurrentValue()
    {
        var cut = RenderYears("2025-2026");

        var trigger = cut.Find("button.filter-dropdown__control");
        trigger.TextContent.Should().Contain("Ano letivo:").And.Contain("2025-2026");
        trigger.GetAttribute("aria-expanded").Should().Be("false");
        trigger.GetAttribute("aria-haspopup").Should().Be("listbox");
    }

    [Fact]
    public void Open_ShowsListbox_WithSelectedOptionMarked_AndActiveDescendant()
    {
        var cut = RenderYears("2025-2026");

        cut.Find("button.filter-dropdown__control").Click();

        cut.Find("button.filter-dropdown__control").GetAttribute("aria-expanded").Should().Be("true");
        var listbox = cut.Find("[role='listbox']");
        listbox.GetAttribute("aria-label").Should().Be("Ano letivo");
        var selected = cut.Find("[role='option'][aria-selected='true']");
        selected.TextContent.Should().Contain("2025-2026");
        selected.QuerySelector(".filter-dropdown__check").Should().NotBeNull("selection is not color alone");
        listbox.GetAttribute("aria-activedescendant").Should().Be(selected.Id);
    }

    [Fact]
    public void ArrowAndEnter_SelectTheNextOption()
    {
        string? chosen = null;
        var cut = RenderYears("2025-2026", v => chosen = v);
        cut.Find("button.filter-dropdown__control").Click();

        cut.Find("[role='listbox']").KeyDown("ArrowDown");
        cut.Find("[role='listbox']").KeyDown("Enter");

        chosen.Should().Be("2024-2025");
        cut.FindAll("[role='listbox']").Should().BeEmpty();
    }

    [Fact]
    public void Escape_ClosesWithoutChanging()
    {
        string? chosen = null;
        var cut = RenderYears("2025-2026", v => chosen = v);
        cut.Find("button.filter-dropdown__control").Click();

        cut.Find("[role='listbox']").KeyDown("Escape");

        cut.FindAll("[role='listbox']").Should().BeEmpty();
        chosen.Should().BeNull();
    }

    [Fact]
    public void ClickOutside_Closes()
    {
        var cut = RenderYears();
        cut.Find("button.filter-dropdown__control").Click();

        cut.Find(".filter-dropdown__backdrop").Click();

        cut.FindAll("[role='listbox']").Should().BeEmpty();
    }

    [Fact]
    public void ClickOption_SelectsIt()
    {
        string? chosen = null;
        var cut = RenderYears("", v => chosen = v);
        cut.Find("button.filter-dropdown__control").Click();

        cut.FindAll("[role='option']").Single(o => o.TextContent.Contains("2024-2025")).Click();

        chosen.Should().Be("2024-2025");
    }

    [Fact]
    public void Disabled_CannotOpen()
    {
        var cut = Render<FilterDropdown>(p => p
            .Add(x => x.Options, Years)
            .Add(x => x.Disabled, true));

        cut.Find("button.filter-dropdown__control").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void WithoutLabel_FallsBackToAGenericName()
    {
        var cut = Render<FilterDropdown>(p => p.Add(x => x.Options, Years));

        cut.Find("button .visually-hidden").TextContent.Should().Be("Filtro: ");
    }

    [Fact]
    public void TwoDropdowns_OpeningOne_DoesNotCloseAnotherInstance()
    {
        // The old static "DropdownOpened" event closed every open dropdown on the server,
        // other users' included. Each instance now owns its state.
        var first = RenderYears();
        var second = RenderYears();
        first.Find("button.filter-dropdown__control").Click();

        second.Find("button.filter-dropdown__control").Click();

        first.FindAll("[role='listbox']").Should().ContainSingle();
        second.FindAll("[role='listbox']").Should().ContainSingle();
    }
}
