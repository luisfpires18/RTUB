using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components.Common;

/// <summary>
/// EmptyState and ErrorDisplay contracts (UI refactor 036, docs/design/RTUB_UI_REFACTOR.md
/// sections 23.3 and 23.5).
/// </summary>
public class StateComponentsTests : BunitContext
{
    [Fact]
    public void EmptyState_DefaultTitle_IsPortuguese()
    {
        var cut = Render<EmptyState>();

        cut.Find(".empty-state-title").TextContent.Should().Be("Sem resultados");
    }

    [Fact]
    public void EmptyState_TitleAndMessage_IconDecorative_NoInteractiveCard()
    {
        var cut = Render<EmptyState>(p => p
            .Add(x => x.Title, "Sem ensaios marcados")
            .Add(x => x.Message, "Os próximos ensaios aparecem aqui.")
            .Add(x => x.Icon, "bi-calendar-x"));

        cut.Find(".empty-state-title").TextContent.Should().Be("Sem ensaios marcados");
        cut.Find(".empty-state-message").TextContent.Should().Be("Os próximos ensaios aparecem aqui.");
        cut.Find(".empty-state-icon").GetAttribute("aria-hidden").Should().Be("true");
        cut.FindAll("[role], [tabindex], button, a").Should().BeEmpty("without an action the state is plain content");
    }

    [Fact]
    public void EmptyState_Action_IsAButtonThatRunsTheCallback()
    {
        var clicked = false;
        var cut = Render<EmptyState>(p => p
            .Add(x => x.Title, "Sem quadros")
            .Add(x => x.ActionText, "Criar Quadro")
            .Add(x => x.OnAction, EventCallback.Factory.Create(this, () => clicked = true)));

        var button = cut.Find("button");
        button.TextContent.Should().Be("Criar Quadro");
        button.Click();
        clicked.Should().BeTrue();
    }

    [Fact]
    public void EmptyState_ActionUrl_IsALink()
    {
        var cut = Render<EmptyState>(p => p
            .Add(x => x.ActionText, "Ver atuações")
            .Add(x => x.ActionUrl, "/events"));

        cut.Find("a").GetAttribute("href").Should().Be("/events");
    }

    [Fact]
    public void ErrorDisplay_WithErrors_IsAnnouncedAlert_ListingEachError()
    {
        var cut = Render<ErrorDisplay>(p => p
            .Add(x => x.Title, "Erro ao carregar documentação")
            .Add(x => x.Errors, new[] { "Falha na ligação", "Tente mais tarde" }));

        cut.Find(".error-display-container").GetAttribute("role").Should().Be("alert");
        cut.Find(".error-display-title").TextContent.Should().Be("Erro ao carregar documentação");
        cut.FindAll(".error-display-item").Select(li => li.TextContent)
            .Should().Equal("Falha na ligação", "Tente mais tarde");
    }

    [Fact]
    public void ErrorDisplay_BlankErrors_RendersNothing()
    {
        var cut = Render<ErrorDisplay>(p => p.Add(x => x.Errors, new[] { "", " " }));

        cut.Markup.Trim().Should().BeEmpty();
    }
}
