using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components.Common;

/// <summary>
/// AppErrorBoundary contract (UI refactor 036, docs/design/RTUB_UI_REFACTOR.md section 23.9):
/// a rendering or child-component failure inside a page's content shows the RTUB fallback
/// instead of ending the circuit, without exception details, and can be retried.
/// </summary>
public class AppErrorBoundaryTests : BunitContext
{
    public AppErrorBoundaryTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void ChildComponentHandler_Throwing_ShowsFallbackInsteadOfEndingTheCircuit()
    {
        var cut = Render<PageWithBoundary>();

        cut.Find("button.child").Click();

        cut.Find(".app-error").GetAttribute("role").Should().Be("alert");
        cut.Find("h1").TextContent.Should().Be("Não foi possível mostrar esta página");
        cut.Markup.Should().NotContain("secret detail", "exception details are never shown");
        cut.FindAll("button.go, button.child").Should().BeEmpty();
    }

    [Fact]
    public void PageOwnHandler_IsNotCaught_ItReachesTheCircuitErrorUi()
    {
        // Blazor attributes an event handler's exception to the component that owns the handler
        // (the page), which sits outside its own boundary. Documented limit (section 23.9):
        // pages keep try/catch around saves; an escape ends the circuit and shows #blazor-error-ui.
        var cut = Render<PageWithBoundary>();

        var act = () => cut.Find("button.go").Click();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RenderingFailure_ShowsFallback_AndRetryRendersTheContentAgain()
    {
        var failure = new Failure { On = true };
        var cut = Render<PageWithBoundary>(p => p.Add(x => x.Fail, failure));

        cut.FindAll(".app-error").Should().ContainSingle();

        failure.On = false;
        cut.FindAll(".app-error button").First(b => b.TextContent == "Tentar novamente").Click();

        cut.FindAll(".app-error").Should().BeEmpty();
        cut.Find("button.go").Should().NotBeNull();
    }

    [Fact]
    public void Fallback_OffersRetryAndReload()
    {
        var cut = Render<PageWithBoundary>(p => p.Add(x => x.Fail, new Failure { On = true }));

        cut.FindAll(".app-error button").Select(b => b.TextContent)
            .Should().Equal("Tentar novamente", "Recarregar página");
    }

    [Fact]
    public void WithoutFailure_RendersOnlyTheContent()
    {
        var cut = Render<PageWithBoundary>();

        cut.FindAll(".app-error").Should().BeEmpty();
        cut.Find("button.go").TextContent.Should().Be("Go");
        cut.Find("button.child").Should().NotBeNull();
    }

    private sealed class ThrowingChild : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "button");
            builder.AddAttribute(1, "class", "child");
            builder.AddAttribute(2, "onclick", EventCallback.Factory.Create(this, () => throw new InvalidOperationException("secret detail")));
            builder.AddContent(3, "Child");
            builder.CloseElement();
        }
    }

    private sealed class Failure
    {
        public bool On { get; set; }
    }

    private sealed class PageWithBoundary : ComponentBase
    {
        [Parameter] public Failure Fail { get; set; } = new();

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<AppErrorBoundary>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(content =>
            {
                if (Fail.On)
                {
                    throw new InvalidOperationException("secret detail");
                }

                content.OpenElement(0, "button");
                content.AddAttribute(1, "class", "go");
                content.AddAttribute(2, "onclick", EventCallback.Factory.Create(this, () => throw new InvalidOperationException("secret detail")));
                content.AddContent(3, "Go");
                content.CloseElement();
                content.OpenComponent<ThrowingChild>(4);
                content.CloseComponent();
            }));
            builder.CloseComponent();
        }
    }
}
