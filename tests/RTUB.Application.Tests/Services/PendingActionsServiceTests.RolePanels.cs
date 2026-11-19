// PendingActionsService: role panels.
using System.Security.Claims;
using FluentAssertions;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Application.Services;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public partial class PendingActionsServiceTests
{
    [Fact]
    public async Task Secretary_SeesLastYearsMissingTopLevelFolders_AndCreatesOnlyThose()
    {
        var secretary = AddUser([MemberCategory.Tuno], retired: false, positions: [Position.Secretario]);
        var admin = AddUser([MemberCategory.Tuno], retired: false);
        AddRole(admin, "Admin");
        var docs = Documentation(["Atas", "Contas", "Logistics", "Logistics/FITAB", "Regulamentos"], ["contas"]);
        var service = Service(new SecretaryPendingActionSource(docs.Object));

        var panel = (await service.GetPanelAsync(TestPrincipal.SignedIn(secretary), "secretary", null, 1)).Value!;
        var row = panel.Rows.Single();
        row.Context.Should().Be("Em falta, como em 2025-2026: Atas, Regulamentos", "the board files folder and an existing name (any case) are left out");
        row.Dismissible.Should().BeTrue();
        row.Actions[0].Confirm.Should().Contain("Criar 2 pastas em 2026-2027: Atas, Regulamentos");
        (await service.GetPageAsync(TestPrincipal.SignedIn(admin))).Value!.Panels.Select(p => p.Key).Should().NotContain("secretary", "a role without the position");
        (await service.RespondAsync(TestPrincipal.SignedIn(admin), new("secretary.folders", "", "create", null, null))).Status.Should().Be(EventResultStatus.NotFound);

        (await service.RespondAsync(TestPrincipal.SignedIn(secretary), new("secretary.folders", "", "create", null, null))).Status.Should().Be(EventResultStatus.Ok);

        docs.Verify(d => d.CreateFolderAsync(It.Is<DocumentFolderInput>(i => i.Name == "Atas"), It.Is<ClaimsPrincipal>(p => p.FindFirst(ClaimTypes.NameIdentifier)!.Value == secretary)));
        docs.Verify(d => d.CreateFolderAsync(It.Is<DocumentFolderInput>(i => i.Name == "Regulamentos"), It.IsAny<ClaimsPrincipal>()));
        docs.Verify(d => d.CreateFolderAsync(It.IsAny<DocumentFolderInput>(), It.IsAny<ClaimsPrincipal>()), Times.Exactly(2), "only the missing ones");
    }

    [Fact]
    public async Task Secretary_AgoraNao_IsTheirOwnForThisFiscalYear_AndNothingMissingMeansNoRow()
    {
        var secretary = AddUser([MemberCategory.Tuno], retired: false, positions: [Position.Secretario]);
        var complete = Service(SecretarySource(Documentation(["Atas"], ["Atas", "Extra"]).Object));
        (await complete.GetPanelAsync(TestPrincipal.SignedIn(secretary), "secretary", null, 1)).Value!.Total.Should().Be(0);

        var service = Service(SecretarySource(Documentation(["Atas"], []).Object));
        (await service.DismissAsync(TestPrincipal.SignedIn(secretary), new("secretary.folders", ""))).Status.Should().Be(EventResultStatus.Ok);

        (await service.GetPanelAsync(TestPrincipal.SignedIn(secretary), "secretary", null, 1)).Value!.Total.Should().Be(0);
        Dismissals().Should().ContainSingle(d => d.UserId == secretary && d.ActionKind == "secretary.folders" && d.Period == "2026-2027");
        (await service.RespondAsync(TestPrincipal.SignedIn(secretary), new("secretary.folders", "", "create", null, null))).Status.Should().Be(EventResultStatus.NotFound,
            "dismissed: nothing pending");
    }

    // ---------- live permissions ----------

    [Fact]
    public async Task APanelGatedByRole_FollowsTheDatabase_NotTheCookie()
    {
        var me = AddUser([MemberCategory.Tuno], retired: false);
        var gated = new FakeSource("mods", required: true) { Role = "Mod" };
        var cookie = TestPrincipal.SignedIn(me); // no role claims at all

        (await Service(gated).GetPageAsync(cookie)).Value!.Panels.Select(p => p.Key).Should().NotContain("mods");
        AddRole(me, "Mod");
        (await Service(gated).GetPageAsync(cookie)).Value!.Panels.Select(p => p.Key).Should().Contain("mods");
        SetRetired(me, true);
        (await Service(gated).GetPageAsync(cookie)).Status.Should().Be(EventResultStatus.NotFound, "087: a Reformado has no page, whatever the role");
        SetRetired(me, false);
        (await Service(gated).GetPageAsync(cookie)).Value!.Panels.Select(p => p.Key).Should().Contain("mods");
    }

    // ---------- Galeria and Novidades (077) ----------

    [Fact]
    public async Task Gallery_ListsOverEventsOfTheYearWithoutAPhoto_AVideoDoesNotCount_AnyonesPhotoResolvesForAll()
    {
        var me = AddUser([MemberCategory.Tuno], retired: false);
        var other = AddUser([MemberCategory.Tuno], retired: false);
        var withVideo = AddEvent("Com vídeo", Today.AddDays(-3));
        var withPhoto = AddEvent("Com foto", Today.AddDays(-4));
        var bare = AddEvent("Sem nada", Today.AddDays(-5));
        AddEvent("Cancelada", Today.AddDays(-6), cancelled: true);
        AddEvent("Ainda não foi", Today.AddDays(2));
        AddEvent("Ano passado", new DateTime(2026, 8, 30));
        AddMedia(other, withPhoto, MediaType.Image);
        AddMedia(other, withVideo, MediaType.Video);
        AddMedia(other, null, MediaType.Image); // a legacy item is never matched to an event

        var service = Service(new GalleryPendingActionSource());
        var panel = (await service.GetPanelAsync(TestPrincipal.SignedIn(me), "gallery", null, 1)).Value!;

        panel.Rows.Select(r => r.SourceKey).Should().Equal(Key(withVideo), Key(bare));
        panel.Rows[0].Actions.Single().Href.Should().Be($"/gallery?upload=1&event={withVideo}&date=2026-11-03&title=Com%20v%C3%ADdeo");
        panel.Rows[0].DismissLabel.Should().Be("Não tenho");

        AddMedia(other, bare, MediaType.Image);
        (await service.GetPanelAsync(TestPrincipal.SignedIn(me), "gallery", null, 1)).Value!.Total.Should().Be(1);
    }

    [Fact]
    public async Task Gallery_NaoTenho_IsPersonal_AndOnlyForActiveMembers()
    {
        var me = AddUser([MemberCategory.Tuno], retired: false);
        var other = AddUser([MemberCategory.Tuno], retired: false);
        var retired = AddUser([MemberCategory.Tuno], retired: true);
        var ev = AddEvent("Serenata", Today.AddDays(-2));
        var service = Service(new GalleryPendingActionSource());

        (await service.DismissAsync(TestPrincipal.SignedIn(me), new PendingActionDismissInput(GalleryPendingActionSource.KindCode, Key(ev)))).Status
            .Should().Be(EventResultStatus.Ok);

        (await service.GetPanelAsync(TestPrincipal.SignedIn(me), "gallery", null, 1)).Value!.Total.Should().Be(0);
        (await service.GetPanelAsync(TestPrincipal.SignedIn(other), "gallery", null, 1)).Value!.Total.Should().Be(1);
        (await service.GetPanelAsync(TestPrincipal.SignedIn(retired), "gallery", null, 1)).Status.Should().Be(EventResultStatus.NotFound);
        Dismissals().Single().Period.Should().Be("2026-2027");
    }

    [Fact]
    public async Task News_OnlyForWriters_OnePromptPerWeek_ResolvedByOwnPostThisWeek_DismissedForTheWeek()
    {
        // 087: the Direção in office who may write news; a writer outside the Direção, or a Direção member who cannot write, has no panel.
        var mod = AddUser([MemberCategory.Tuno], retired: false, positions: [Position.ViceMagister]);
        AddRole(mod, "Mod");
        var admin = AddUser([MemberCategory.Tuno], retired: false, positions: [Position.Magister]);
        AddRole(admin, "Admin");
        var member = AddUser([MemberCategory.Tuno], retired: false);
        var writerOutside = AddUser([MemberCategory.Tuno], retired: false);
        AddRole(writerOutside, "Mod");
        var direcaoNotWriter = AddUser([MemberCategory.Tuno], retired: false, positions: [Position.Secretario]);
        var service = Service(new NewsPendingActionSource());

        (await service.GetPanelAsync(TestPrincipal.SignedIn(member), "news", null, 1)).Status.Should().Be(EventResultStatus.NotFound);
        (await service.GetPanelAsync(TestPrincipal.SignedIn(writerOutside), "news", null, 1)).Status.Should().Be(EventResultStatus.NotFound);
        (await service.GetPanelAsync(TestPrincipal.SignedIn(direcaoNotWriter), "news", null, 1)).Status.Should().Be(EventResultStatus.NotFound);
        var row = (await service.GetPanelAsync(TestPrincipal.SignedIn(mod), "news", null, 1)).Value!.Rows.Single();
        row.Actions.Single().Href.Should().Be("/news?new=1");
        row.Required.Should().BeFalse();

        AddNews(mod, new DateTime(2026, 11, 1, 22, 0, 0)); // last Sunday: another week
        (await service.GetPanelAsync(TestPrincipal.SignedIn(mod), "news", null, 1)).Value!.Total.Should().Be(1);
        AddNews(mod, new DateTime(2026, 11, 2, 9, 0, 0)); // a draft this Monday
        (await service.GetPanelAsync(TestPrincipal.SignedIn(mod), "news", null, 1)).Value!.Total.Should().Be(0, "a saved draft resolves the week");

        (await service.DismissAsync(TestPrincipal.SignedIn(admin), new PendingActionDismissInput(NewsPendingActionSource.KindCode, ""))).Status
            .Should().Be(EventResultStatus.Ok);
        (await service.GetPanelAsync(TestPrincipal.SignedIn(admin), "news", null, 1)).Value!.Total.Should().Be(0);
        Dismissals().Single().Period.Should().Be("2026-W45");
        NewsPendingActionSource.Week(new DateTime(2027, 1, 1)).Should().Be("2026-W53");
    }
}
