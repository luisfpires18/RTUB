using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components.UI;

/// <summary>
/// PageHeader, PageActions/PageAction and MobileBottomNav contracts (UI refactor 035,
/// docs/design/RTUB_UI_REFACTOR.md section 22): one heading, one action list rendered as header
/// buttons and as the phone bar, and navigation bars that are navigation.
/// </summary>
public class PageHeaderTests : BunitContext
{
    [Fact]
    public void PageHeader_RendersOneHeading_WithIconHiddenAndSubtitle()
    {
        var cut = Render<PageHeader>(parameters => parameters
            .Add(p => p.Title, "Ensaios")
            .Add(p => p.Icon, "bi-music-note-list")
            .Add(p => p.Subtitle, "Terças e Quintas"));

        var heading = cut.FindAll("h1").Should().ContainSingle().Subject;
        heading.TextContent.Trim().Should().Be("Ensaios");
        heading.QuerySelector("i")!.GetAttribute("aria-hidden").Should().Be("true");
        cut.Find(".page-header__subtitle").TextContent.Should().Be("Terças e Quintas");
    }

    [Fact]
    public void PageHeader_WithoutActions_RendersNoActionGroupOrBar()
    {
        var cut = Render<PageHeader>(parameters => parameters.Add(p => p.Title, "Hierarquia"));

        cut.FindAll(".page-actions, .page-action-bar").Should().BeEmpty();
    }

    [Fact]
    public void PageHeader_BackHref_IsANamedLink()
    {
        var cut = Render<PageHeader>(parameters => parameters
            .Add(p => p.Title, "Mapa")
            .Add(p => p.BackHref, "/members"));

        var back = cut.Find("a.page-header__back");
        back.GetAttribute("href").Should().Be("/members");
        back.GetAttribute("aria-label").Should().Be("Voltar");
    }

    [Fact]
    public void PageHeader_OnBack_IsAButtonThatCallsBack()
    {
        var wentBack = false;
        var cut = Render<PageHeader>(parameters => parameters
            .Add(p => p.Title, "Relatório")
            .Add(p => p.OnBack, EventCallback.Factory.Create(this, () => wentBack = true)));

        cut.Find("button.page-header__back").Click();

        wentBack.Should().BeTrue();
    }

    [Fact]
    public void PageActions_RenderEachActionInTheHeaderAndInTheBar_AsButtonGroupsNotNavigation()
    {
        var cut = Render<PageHeader>(parameters => parameters
            .Add(p => p.Title, "Ensaios")
            .Add(p => p.Actions, Actions(b =>
            {
                b.OpenComponent<PageAction>(0);
                b.AddComponentParameter(1, nameof(PageAction.Label), "Minhas Presenças");
                b.AddComponentParameter(2, nameof(PageAction.ShortLabel), "Presenças");
                b.AddComponentParameter(3, nameof(PageAction.Icon), "bi-calendar-check");
                b.CloseComponent();
            })));

        cut.FindAll("nav").Should().BeEmpty("page actions are not navigation");

        var groups = cut.FindAll("[role='group']");
        groups.Should().HaveCount(2);
        groups.Should().OnlyContain(g => g.GetAttribute("aria-label") == "Ações da página");

        cut.Find(".page-actions button").TextContent.Trim().Should().Be("Minhas Presenças");
        var barItem = cut.Find(".page-action-bar button");
        barItem.TextContent.Trim().Should().Be("Presenças");
        barItem.GetAttribute("aria-label").Should().Be("Minhas Presenças", "the full label stays the accessible name");
    }

    [Fact]
    public void PageAction_Intent_SetsTheWeight()
    {
        var cut = Render<PageActions>(parameters => parameters.Add(p => p.ChildContent, Actions(b =>
        {
            b.OpenComponent<PageAction>(0);
            b.AddComponentParameter(1, nameof(PageAction.Label), "Adicionar");
            b.AddComponentParameter(2, nameof(PageAction.Intent), PageActionIntent.Primary);
            b.CloseComponent();
            b.OpenComponent<PageAction>(3);
            b.AddComponentParameter(4, nameof(PageAction.Label), "Estatísticas");
            b.CloseComponent();
            b.OpenComponent<PageAction>(5);
            b.AddComponentParameter(6, nameof(PageAction.Label), "Eliminar");
            b.AddComponentParameter(7, nameof(PageAction.Intent), PageActionIntent.Danger);
            b.CloseComponent();
        })));

        var header = cut.FindAll(".page-actions button");
        header[0].ClassList.Should().Contain("btn-primary");
        header[1].ClassList.Should().Contain("btn-outline-secondary");
        header[2].ClassList.Should().Contain("btn-outline-danger");
        cut.FindAll(".page-action-bar button")[2].ClassList.Should().Contain("page-action-bar__item--danger");
    }

    [Fact]
    public void PageAction_Click_FromEitherPresentation_RunsTheSameAction()
    {
        var clicks = 0;
        var cut = Render<PageActions>(parameters => parameters.Add(p => p.ChildContent, Actions(b =>
        {
            b.OpenComponent<PageAction>(0);
            b.AddComponentParameter(1, nameof(PageAction.Label), "Adicionar");
            b.AddComponentParameter(2, nameof(PageAction.OnClick), EventCallback.Factory.Create(this, () => clicks++));
            b.CloseComponent();
        })));

        cut.Find(".page-actions button").Click();
        cut.Find(".page-action-bar button").Click();

        clicks.Should().Be(2);
    }

    [Fact]
    public void PageAction_Busy_IsDisabledAndAnnounced()
    {
        var cut = Render<PageActions>(parameters => parameters.Add(p => p.ChildContent, Actions(b =>
        {
            b.OpenComponent<PageAction>(0);
            b.AddComponentParameter(1, nameof(PageAction.Label), "Export");
            b.AddComponentParameter(2, nameof(PageAction.Busy), true);
            b.CloseComponent();
        })));

        foreach (var button in cut.FindAll("button"))
        {
            button.HasAttribute("disabled").Should().BeTrue();
            button.GetAttribute("aria-busy").Should().Be("true");
            button.QuerySelector(".spinner-border").Should().NotBeNull();
        }
    }

    [Fact]
    public void PageAction_Href_IsALinkInBothPresentations()
    {
        var cut = Render<PageActions>(parameters => parameters.Add(p => p.ChildContent, Actions(b =>
        {
            b.OpenComponent<PageAction>(0);
            b.AddComponentParameter(1, nameof(PageAction.Label), "Configurar");
            b.AddComponentParameter(2, nameof(PageAction.Href), "/naipes/config");
            b.CloseComponent();
        })));

        cut.FindAll("button").Should().BeEmpty();
        cut.FindAll("a").Should().HaveCount(2).And.OnlyContain(a => a.GetAttribute("href") == "/naipes/config");
    }

    [Fact]
    public void MobileBottomNav_IsNamedNavigation_MarkingTheCurrentItem()
    {
        var cut = Render<MobileBottomNav>(parameters => parameters
            .Add(p => p.Items, new[]
            {
                new MobileBottomNav.MobileNavItem("section-a", "Sobre Nós", "bi-info-circle"),
                new MobileBottomNav.MobileNavItem("section-b", "História", "bi-clock-history")
            })
            .Add(p => p.ActiveKey, "section-b")
            .Add(p => p.AriaLabel, "Secções da página"));

        cut.Find("nav").GetAttribute("aria-label").Should().Be("Secções da página");
        var buttons = cut.FindAll("nav button");
        buttons[0].HasAttribute("aria-current").Should().BeFalse();
        buttons[1].GetAttribute("aria-current").Should().Be("true");
        cut.FindAll("[aria-selected]").Should().BeEmpty("aria-selected belongs to tabs and options, not buttons");
    }

    private static RenderFragment Actions(Action<Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder> build) => b => build(b);
}
