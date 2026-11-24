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
    public void MarkWhoWent_UsesTheSharedPickerAndChips_WithKeysOnly()
    {
        var mark = Read("components", "TrintunaMarkWhoWent.tsx");

        mark.Should().Contain("const options = useMemo(").And.Contain("id: p.key");
        mark.Should().Contain("<MemberPicker").And.Contain("options={options}").And.Contain("exclude={g.going.map((p) => p.key)}");
        mark.Should().Contain("<PersonChip").And.Contain("onRemove={() => run(() => trintunaApi.unmark(g.id, p.key)");
        mark.Should().Contain("removeLabel={`Retirar Fui a ${p.displayName}`}");
        mark.Should().NotContain("userId").And.NotContain("organizers(");
    }

    [Fact]
    public void Dialog_ShowsItOnlyOnPastGatheringsTheCallerCanEdit_FedByTheHallOfFame()
    {
        Read("components", "TrintunaDialogs.tsx").Should().Contain("{g.past && g.canEdit && <MarkWhoWent gathering={g} people={people} onChanged={onChanged} />}");
        Read("pages", "Trintuna.tsx").Should().Contain("people={data?.hallOfFame ?? []}");
    }
}
