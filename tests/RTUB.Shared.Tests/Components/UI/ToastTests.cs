using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components.UI;

/// <summary>
/// ToastService + ToastHost and FilterToolbar contracts (UI refactor 036,
/// docs/design/RTUB_UI_REFACTOR.md sections 23.5 and 23.8).
/// </summary>
public class ToastTests : BunitContext
{
    private readonly ToastService _toasts = new();

    public ToastTests()
    {
        Services.AddSingleton(_toasts);
    }

    [Fact]
    public void LiveRegions_ExistBeforeAnyToast()
    {
        var cut = Render<ToastHost>();

        cut.Find("[role='status']").GetAttribute("aria-live").Should().Be("polite");
        cut.Find("[role='alert']").GetAttribute("aria-live").Should().Be("assertive");
        cut.FindAll(".rtub-toast").Should().BeEmpty();
    }

    [Fact]
    public void Success_GoesToThePoliteRegion_ErrorToTheAssertiveOne()
    {
        var cut = Render<ToastHost>();

        _toasts.ShowSuccess("Link copiado!");
        _toasts.ShowError("Não foi possível copiar o link");

        cut.WaitForAssertion(() =>
        {
            cut.Find("[role='status'] .rtub-toast--success").TextContent.Should().Contain("Link copiado!");
            var error = cut.Find("[role='alert'] .rtub-toast--error");
            error.TextContent.Should().Contain("Erro:").And.Contain("Não foi possível copiar o link");
        });
    }

    [Fact]
    public void DismissButton_IsNamed_AndRemovesTheToast()
    {
        var cut = Render<ToastHost>();
        _toasts.ShowWarning("Atenção");
        cut.WaitForElement(".rtub-toast");

        var close = cut.Find(".rtub-toast__close");
        close.GetAttribute("aria-label").Should().Be("Fechar notificação");
        close.Click();

        cut.WaitForAssertion(() => cut.FindAll(".rtub-toast").Should().BeEmpty());
    }

    [Fact]
    public void Success_LeavesByItself_ErrorStays()
    {
        var cut = Render<ToastHost>(p => p.Add(x => x.Timeout, 50));

        _toasts.ShowSuccess("Guardado");
        _toasts.ShowError("Falhou");

        cut.WaitForAssertion(() =>
        {
            cut.FindAll(".rtub-toast--success").Should().BeEmpty();
            cut.FindAll(".rtub-toast--error").Should().ContainSingle();
        }, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Service_SuppressesDuplicates_AndCapsTheStack()
    {
        _toasts.ShowSuccess("Link copiado!");
        _toasts.ShowSuccess("Link copiado!");
        _toasts.Toasts.Should().ContainSingle();

        for (var i = 0; i < 5; i++)
        {
            _toasts.ShowInfo($"Mensagem {i}");
        }

        _toasts.Toasts.Should().HaveCount(ToastService.MaxVisible);
        _toasts.Toasts.Last().Message.Should().Be("Mensagem 4", "the oldest leave first");
    }

    [Fact]
    public void Service_IgnoresBlankMessages()
    {
        _toasts.ShowSuccess("  ");

        _toasts.Toasts.Should().BeEmpty();
    }

    [Fact]
    public void FilterToolbar_IsANamedSearchLandmark()
    {
        var cut = Render<FilterToolbar>();

        var toolbar = cut.Find(".filter-toolbar");
        toolbar.GetAttribute("role").Should().Be("search");
        toolbar.GetAttribute("aria-label").Should().Be("Pesquisa e filtros");
    }
}
