using FluentAssertions;

namespace RTUB.Web.Tests.ReactPortal;

/// <summary>
/// Task 138: a load whose input can change while the page stays open (a year, a tab, a search, a page number) must
/// drop an answer for an older input, or that answer replaces the newer one. Each place found by the audit keeps its
/// guard: `useLoad`, `useLatest`, or an effect cleanup (`live = false`). The pickers are checked by `npm run check:load`.
/// </summary>
public class PortalLoadGuardSourceTests
{
    private static readonly string Src = Path.Combine(RepoPaths.Root(), "src", "RTUB.Web", "portal", "src");

    private static string Read(string file) => File.ReadAllText(Path.Combine(Src, file));

    [Theory]
    [InlineData("components/MemberListDialogs.tsx", "useLoad(() => membersApi.birthdays(search.q), [search.q], orNull<Birthday[]>)")]
    [InlineData("pages/Documentation.tsx", "latest(api.list(fiscalYear),")]
    [InlineData("pages/Governance.tsx", "getGovernance(fiscalYear).then((d) => () => setData(d), () => () => setFailed(true))")]
    [InlineData("pages/TreasuryCalotes.tsx", "latest(treasuryApi.calotes(year),")]
    [InlineData("pages/News.tsx", "latest(api.feed(1, t),")]
    [InlineData("components/OwnerLoginsDialog.tsx", "latest(ownerApi.logins(from, to),")]
    public void ALoadWithChangingInput_DropsOlderAnswers(string file, string guarded)
    {
        Read(file).Should().Contain(guarded);
    }

    [Theory]
    [InlineData("components/TreasuryTransactionDialogs.tsx", "treasuryApi.history(reportId, page, PAGE).then((o) => live &&")]
    [InlineData("pages/Governance.tsx", "governanceApi.manage(year).then((o) => live &&")]
    [InlineData("components/MusicLearnDialogs.tsx", "musicApi.album(albumId).then((r) => live &&")]
    [InlineData("components/MusicPlayAlongField.tsx", "if (!live || r.kind !== 'ok') return;")]
    [InlineData("components/EventPrizesDialog.tsx", "eventsApi.agenda().then((o) => live &&")]
    public void AnEffectLoad_IgnoresItsAnswerAfterCleanup(string file, string guarded)
    {
        var source = Read(file);
        source.Should().Contain(guarded).And.Contain("live = false;");
    }

    [Fact]
    public void TheOwnerPages_LoadThroughTheSharedHook()
    {
        Read("components/OwnerUi.tsx").Should().Contain("return useLoad(load, deps, orAccess<T>);");
        Read("components/AppShared.tsx").Should().Contain("export function useLoad<T, S>(").And.Contain("if (live) setState(pick(o));")
            .And.Contain("last.current = 0;", "useLatest works again after StrictMode's test remount in development");
    }
}
