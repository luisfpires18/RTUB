using FluentAssertions;
using Xunit;

namespace RTUB.Web.Tests.ReactPortal;

/// <summary>
/// Task 079 (#260): the optional "Adicionar ao Google Calendar" offer. Google opens only from the member's click (a link,
/// never window.open after a save), the destination is fixed, the payload is title / place / times / the RTUB page only,
/// and the offer follows only a new "vou" to an upcoming activity. The link builder itself is checked by
/// `npm run check:calendar` (portal/checks/calendar.check.mjs).
/// </summary>
public class CalendarOfferPortalTests
{
    private static readonly string Src = Path.Combine(GetProjectRoot(), "src", "RTUB.Web", "portal", "src");

    private static string Read(string file) => File.ReadAllText(Path.Combine(Src, file));

    [Fact]
    public void TheOffer_OpensGoogleOnlyFromAClick_WithAFixedDestination_AndNoPrivateFields()
    {
        var offer = Read("CalendarOffer.tsx");
        var builder = Read("calendar.ts");

        offer.Should().NotContain("location.origin", "the link back uses the configured Calendar:PublicOrigin, not the host the page came from");
        offer.Should().NotContain("window.open").And.NotContain("location.assign").And.NotContain("location.href =");
        offer.Should().Contain("target=\"_blank\" rel=\"noopener noreferrer\"").And.Contain("(abre noutra janela)");
        offer.Should().Contain("Alterações ou cancelamentos na RTUB não atualizam esta cópia automaticamente.");
        offer.Should().NotContain("Adicionado ao calendário", "RTUB cannot see whether the member saved it in Google");
        builder.Should().Contain("export const GOOGLE_TEMPLATE = 'https://calendar.google.com/calendar/render';");
        builder.Should().Contain("ctz: ZONE").And.NotContain("getTimezoneOffset").And.NotContain("getHours");
        offer.Should().NotContain("notes").And.NotContain("description").And.NotContain("participants");
    }

    [Fact]
    public void TheOffer_FollowsOnlyANewYes_ToAnUpcomingActivity_OnEveryAnswerPath()
    {
        Read("EventDialogs.tsx").Should().Contain(
            "if (choice.willAttend && enrollment.status !== 'going' && saved.state === 'open' && !saved.event.past && !saved.event.cancelled) setOffer(saved);");
        Read("RehearsalDialogs.tsx").Should().Contain(
            "if (willAttend && !going && data.state === 'open' && !data.rehearsal.past && !data.rehearsal.cancelled) setOffer(true);")
            .And.Contain("Presença registada, pendente de aprovação.");
        Read("PendingActions.tsx").Should().Contain("if (o.kind === 'ok' && a.answer === 'yes' && upcoming) offerCalendar(row);");
        Read("EventDetail.tsx").Should().Contain("{m.myStatus === 'going' && (").And.Contain("Adicionar ao calendário");
        Read("RehearsalDetail.tsx").Should().Contain("{open && r.mine && r.mine.status !== 'notGoing' && (");
    }

    private static string GetProjectRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find project root directory");
    }
}
