using FluentAssertions;

namespace RTUB.Web.Tests.ReactPortal;

/// <summary>
/// Task 164: "Copiar link" (Trintuna gathering, meeting poll) builds the link from the site's own address
/// (<c>shareLink</c>: <c>shareOrigin</c> from /api/account/me = App:BaseUrl), not from the host the page happens to be on.
/// </summary>
public class ShareLinkSourceTests
{
    private static string Read(params string[] path) => File.ReadAllText(Path.Combine([RepoPaths.Root(), "src", "RTUB.Web", "portal", "src", .. path])).Replace("\r\n", "\n");

    [Theory]
    [InlineData("TrintunaDialogs.tsx", "await shareLink(`${portal.trintuna}?almoco=${g.id}`)")]
    [InlineData("MeetingPollCard.tsx", "await shareLink(`${portal.meetingPolls}?poll=${poll.id}`)")]
    public void CopyLink_UsesTheSiteAddress_NotThePageHost(string file, string call)
    {
        var source = Read("components", file);

        source.Should().Contain(call);
        source.Should().NotContain("${location.origin}${portal.");
    }

    [Fact]
    public void ShareLink_PrefersShareOrigin_AndFallsBackToThePage()
    {
        Read("data", "apiSession.ts").Should().Contain("(user?.authenticated && user.shareOrigin) || location.origin");
    }
}
