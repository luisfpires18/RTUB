using FluentAssertions;

namespace RTUB.Web.Tests.ReactPortal;

/// <summary>
/// Task 160: on a past gathering the Owner or its organizer marks who went. The people offered are the Hall of Fame list
/// (keys, no user ids) through the shared <c>MemberPicker</c> <c>options</c> (memoized); each Fui is a removable chip.
/// Other members see the dialog as before.
/// </summary>
public class TrintunaMarkWhoWentSourceTests
{
    private static string Read(params string[] path) => File.ReadAllText(Path.Combine(
        new[] { RepoPaths.Root(), "src", "RTUB.Web", "portal", "src" }.Concat(path).ToArray())).Replace("\r\n", "\n");

    [Fact]
    public void Api_MarksAndUnmarks_ByEncodedKey()
    {
        var api = Read("data", "trintunaApi.ts");

        api.Should().Contain("mark: (id: number, key: string) => call<Gathering>('PUT', `${one(id)}/attendees/${encodeURIComponent(key)}`)");
        api.Should().Contain("unmark: (id: number, key: string) => call<void>('DELETE', `${one(id)}/attendees/${encodeURIComponent(key)}`)");
    }

    [Fact]
    public void MarkWhoWent_UsesTheSharedPicker_ByNicknameOrName_WithKeysOnly()
    {
        var mark = Read("components", "TrintunaMarkWhoWent.tsx");

        mark.Should().Contain("const options = useMemo(").And.Contain("id: p.key").And.Contain("fullName: p.fullName");
        mark.Should().Contain("<MemberPicker").And.Contain("options={options}").And.Contain("exclude={g.going.map((p) => p.key)}");
        mark.Should().NotContain("<PersonChip", "task 161: no second list; the X is on the Quem foi tile");
        mark.Should().NotContain("userId").And.NotContain("organizers(");
    }

    /// <summary>Task 161: removing a mark is an X on the "Quem foi" tile, confirmed with the shared AsyncConfirmDialog.</summary>
    [Fact]
    public void RemovingAMark_IsAnXOnTheTile_WithAConfirmDialog()
    {
        var dialogs = Read("components", "TrintunaDialogs.tsx");
        var people = Read("components", "EventDetailPeople.tsx");

        dialogs.Should().Contain("onRemove={g.past && g.canEdit ? (p) => setUnmarking(");
        dialogs.Should().Contain("<AsyncConfirmDialog").And.Contain("trintunaApi.unmark(g.id, unmarking.key)");
        people.Should().Contain("onRemove?: (p: EventParticipant) => void;");
        people.Should().Contain("<IconAction icon=\"close\"").And.Contain("className=\"mtg-who__remove\"");
        people.Should().Contain("<People people={going} onRemove={onRemove} />");
    }

    /// <summary>Task 161: the browser-side search (organizer, Marcar quem foi) matches every word over nickname and full name, accents folded, as the server search does.</summary>
    [Fact]
    public void LocalSearch_MatchesNicknameOrFullName_AccentsFolded()
    {
        var picker = Read("components", "LogisticsFields.tsx");

        picker.Should().Contain(@"const words = fold(query).split(/\s+/).filter(Boolean);");
        picker.Should().Contain("words.every((w) => fold(`${m.displayName} ${m.fullName ?? ''}`).includes(w))");
        Read("components", "TrintunaForm.tsx").Should().Contain("fullName: m.fullName, avatarUrl: m.avatarUrl");
    }

    [Fact]
    public void Dialog_ShowsItOnlyOnPastGatheringsTheCallerCanEdit_FedByTheHallOfFame()
    {
        Read("components", "TrintunaDialogs.tsx").Should().Contain("{g.past && g.canEdit && <MarkWhoWent gathering={g} people={people} onChanged={onChanged} />}");
        Read("pages", "Trintuna.tsx").Should().Contain("people={data?.hallOfFame ?? []}");
    }
}
