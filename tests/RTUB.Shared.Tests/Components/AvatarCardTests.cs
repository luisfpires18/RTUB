using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// AvatarCard (Membros directory entry): identity content, the name as the card's link, online
/// state, and the management menu (docs/design/RTUB_UI_REFACTOR.md section 25). Queries by
/// accessible names rather than CSS classes.
/// </summary>
public class AvatarCardTests : BunitContext
{
    private IRenderedComponent<AvatarCard> RenderCard(Action<ComponentParameterCollectionBuilder<AvatarCard>>? more = null,
        string avatarUrl = "/img/joao.webp", string tunaName = "Arbusto") =>
        Render<AvatarCard>(parameters =>
        {
            parameters
                .Add(p => p.AvatarUrl, avatarUrl)
                .Add(p => p.TunaName, tunaName)
                .Add(p => p.FullName, "Diogo do Couto")
                .Add(p => p.AltText, "Arbusto");
            more?.Invoke(parameters);
        });

    private static List<string> MenuItems(IRenderedComponent<AvatarCard> cut) =>
        cut.FindAll(".dropdown-menu button").Select(b => b.TextContent.Trim()).ToList();

    private static IElement MenuItem(IRenderedComponent<AvatarCard> cut, string name) =>
        cut.FindAll(".dropdown-menu button").Single(b => b.TextContent.Trim() == name);

    #region Identity

    [Fact]
    public void AvatarCard_ShowsTunaNameAsHeading_AndFullName()
    {
        var cut = RenderCard();

        var heading = cut.Find("h3");
        heading.TextContent.Trim().Should().Be("Arbusto");
        cut.Find("article").GetAttribute("aria-labelledby").Should().Be(heading.Id);
        cut.Markup.Should().Contain("Diogo do Couto");
    }

    [Fact]
    public void AvatarCard_UsesFullNameAsHeading_WhenNoTunaName()
    {
        var cut = RenderCard(tunaName: "");

        cut.Find("h3").TextContent.Trim().Should().Be("Diogo do Couto");
        cut.FindAll(".member-item__fullname").Should().BeEmpty("the name is not repeated");
    }

    [Fact]
    public void AvatarCard_Photo_HasAltText_AndSharedFallback()
    {
        var img = RenderCard().Find("img");

        img.GetAttribute("src").Should().Be("/img/joao.webp");
        img.GetAttribute("alt").Should().Be("Arbusto");
        img.HasAttribute("data-avatar-fallback").Should().BeTrue("avatarFallback.js swaps a broken photo for the default avatar");
    }

    [Fact]
    public void AvatarCard_UsesDefaultAvatar_WhenNoUrl()
    {
        RenderCard(avatarUrl: "").Find("img").GetAttribute("src").Should().Be("/images/default-avatar.webp");
    }

    [Theory]
    [InlineData(true, "lazy")]
    [InlineData(false, "eager")]
    public void AvatarCard_LoadingMode_FollowsLazyLoad(bool lazy, string expected)
    {
        RenderCard(p => p.Add(x => x.LazyLoad, lazy)).Find("img").GetAttribute("loading").Should().Be(expected);
    }

    [Fact]
    public void AvatarCard_RendersBadgeContent()
    {
        var cut = RenderCard(p => p.Add(x => x.BadgeContent, (RenderFragment)(b => b.AddMarkupContent(0, "<span class=\"badge\">TUNO</span>"))));

        cut.Find(".member-item__tags .badge").TextContent.Should().Be("TUNO");
    }

    [Fact]
    public void AvatarCard_ShowsInstrument_WhenProvided()
    {
        var cut = RenderCard(p => p.Add(x => x.InstrumentText, "Guitarra"));

        cut.Markup.Should().Contain("Guitarra").And.Contain("bi-music-note");
    }

    [Fact]
    public void AvatarCard_NoInstrumentLine_WhenEmpty()
    {
        RenderCard(p => p.Add(x => x.InstrumentText, "")).Markup.Should().NotContain("bi-music-note");
    }

    [Fact]
    public void AvatarCard_ShowsInactivityWarning_WhenRequested()
    {
        RenderCard(p => p.Add(x => x.ShowInactivityWarning, true)).Markup.Should().Contain("Não tem participado");
    }

    [Fact]
    public void AvatarCard_ShowsExpelledStatus()
    {
        RenderCard(p => p.Add(x => x.IsExpelled, true)).Find(".item-status").TextContent.Should().Contain("Expulso");
    }

    #endregion

    #region Online state

    [Fact]
    public void AvatarCard_ShowsOnline_WhenActiveWithinTheLastHour()
    {
        var cut = RenderCard(p => p.Add(x => x.LastLoginDate, DateTime.UtcNow.AddMinutes(-30)));

        cut.Find(".member-item__online").TextContent.Should().Be("Online");
        cut.Find(".member-item__presence").GetAttribute("title").Should().Be("Online");
    }

    [Theory]
    [InlineData(-2.0)]
    [InlineData(-1.01)]
    public void AvatarCard_NoOnlineMark_WhenLastActivityOlderThanOneHour(double hours)
    {
        var cut = RenderCard(p => p.Add(x => x.LastLoginDate, DateTime.UtcNow.AddHours(hours)));

        cut.FindAll(".member-item__online").Should().BeEmpty();
        cut.FindAll(".member-item__presence").Should().BeEmpty();
    }

    [Fact]
    public void AvatarCard_NoOnlineMark_WhenNeverLoggedIn()
    {
        RenderCard(p => p.Add(x => x.LastLoginDate, (DateTime?)null)).Markup.Should().NotContain("Online");
    }

    #endregion

    #region Opening the details

    [Fact]
    public void AvatarCard_NameIsAButton_ThatOpensTheDetails()
    {
        var viewed = false;
        var cut = RenderCard(p => p
            .Add(x => x.ViewTooltip, "Ver Detalhes")
            .Add(x => x.OnView, EventCallback.Factory.Create(this, () => viewed = true)));

        var open = cut.Find("h3 button");
        open.GetAttribute("type").Should().Be("button");
        open.GetAttribute("title").Should().Be("Ver Detalhes");
        open.Click();

        viewed.Should().BeTrue();
    }

    [Fact]
    public void AvatarCard_HasNoSeparateViewButton()
    {
        RenderCard().FindAll("button").Should().ContainSingle("the name is the only way in when there are no admin actions");
    }

    #endregion

    #region Management menu

    [Fact]
    public void AvatarCard_NoMenu_WithoutManagementPermissions()
    {
        RenderCard().FindAll(".dropdown-menu").Should().BeEmpty();
    }

    [Fact]
    public void AvatarCard_MemberMenu_EditThenDeleteLast()
    {
        var cut = RenderCard(p => p
            .Add(x => x.ShowEditButton, true)
            .Add(x => x.ShowDeleteButton, true)
            .Add(x => x.EditTooltip, "Editar")
            .Add(x => x.DeleteTooltip, "Eliminar"));

        cut.Find("[data-bs-toggle=dropdown]").GetAttribute("aria-label").Should().Be("Gerir: Arbusto");
        MenuItems(cut).Should().Equal("Editar", "Eliminar");
    }

    [Fact]
    public void AvatarCard_EditOnly_WhenDeleteNotAllowed()
    {
        MenuItems(RenderCard(p => p.Add(x => x.ShowEditButton, true))).Should().Equal("Editar");
    }

    [Fact]
    public void AvatarCard_LeitaoMenu_NicknameExpelDelete()
    {
        var cut = RenderCard(p => p
            .Add(x => x.ShowEditButton, true)
            .Add(x => x.ShowDeleteButton, true)
            .Add(x => x.ShowSetNicknameButton, true)
            .Add(x => x.ShowExpelButton, true));

        MenuItems(cut).Should().Equal("Editar", "Definir Alcunha", "Expulsar membro", "Eliminar");
    }

    [Fact]
    public void AvatarCard_ExpelledLeitao_OffersReactivate()
    {
        var cut = RenderCard(p => p
            .Add(x => x.ShowExpelButton, true)
            .Add(x => x.IsExpelled, true));

        MenuItems(cut).Should().Equal("Reativar membro");
    }

    [Fact]
    public void AvatarCard_MenuItems_InvokeCallbacks()
    {
        var calls = new List<string>();
        var cut = RenderCard(p => p
            .Add(x => x.ShowEditButton, true)
            .Add(x => x.ShowDeleteButton, true)
            .Add(x => x.ShowSetNicknameButton, true)
            .Add(x => x.ShowExpelButton, true)
            .Add(x => x.OnEdit, EventCallback.Factory.Create(this, () => calls.Add("edit")))
            .Add(x => x.OnSetNickname, EventCallback.Factory.Create(this, () => calls.Add("nickname")))
            .Add(x => x.OnExpel, EventCallback.Factory.Create(this, () => calls.Add("expel")))
            .Add(x => x.OnDelete, EventCallback.Factory.Create(this, () => calls.Add("delete"))));

        foreach (var item in new[] { "Editar", "Definir Alcunha", "Expulsar membro", "Eliminar" })
        {
            MenuItem(cut, item).Click();
        }

        calls.Should().Equal("edit", "nickname", "expel", "delete");
    }

    [Fact]
    public void AvatarCard_Reactivate_InvokesCallback()
    {
        var reactivated = false;
        var cut = RenderCard(p => p
            .Add(x => x.ShowExpelButton, true)
            .Add(x => x.IsExpelled, true)
            .Add(x => x.OnReactivate, EventCallback.Factory.Create(this, () => reactivated = true)));

        MenuItem(cut, "Reativar membro").Click();

        reactivated.Should().BeTrue();
    }

    #endregion
}
