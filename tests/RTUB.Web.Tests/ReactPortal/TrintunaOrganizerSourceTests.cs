using FluentAssertions;

namespace RTUB.Web.Tests.ReactPortal;

/// <summary>
/// Task 159: the Trintuna Organizador is a member search like "Quem tem acesso" (the shared <c>MemberPicker</c>), limited
/// to the allowed people (Owners + access list) through its <c>options</c> list, searched in the browser. The chosen
/// organizer is a removable chip. The options are memoized so the picker's search effect does not loop.
/// </summary>
public class TrintunaOrganizerSourceTests
{
    private static string Read(string file) => File.ReadAllText(Path.Combine(
        RepoPaths.Root(), "src", "RTUB.Web", "portal", "src", "components", file)).Replace("\r\n", "\n");

    [Fact]
    public void Organizer_IsAMemberSearch_OverTheAllowedList_NotASelect()
    {
        var form = Read("TrintunaForm.tsx");
        var field = form[form.IndexOf("label=\"Organizador\" error=", StringComparison.Ordinal)..form.IndexOf("</FormField>", form.IndexOf("label=\"Organizador\" error=", StringComparison.Ordinal), StringComparison.Ordinal)];

        field.Should().Contain("<MemberPicker").And.Contain("options={pickable}").And.Contain("onPick={(m) => setOrganizer(m.id)}");
        field.Should().Contain("<PersonChip").And.Contain("onRemove={() => setOrganizer('')}");
        field.Should().NotContain("<SelectField");
        form.Should().Contain("const organizers = useMemo(").And.Contain("const pickable = useMemo(");
    }

    [Fact]
    public void MemberPicker_WithOptions_SearchesThatListWithoutCallingTheServer()
    {
        var picker = Read("LogisticsFields.tsx");

        picker.Should().Contain("options?: MemberOption[];");
        var effect = picker[picker.IndexOf("useEffect(", StringComparison.Ordinal)..picker.IndexOf("}, [query, boardId, options]);", StringComparison.Ordinal)];
        effect.IndexOf("if (options)", StringComparison.Ordinal).Should().BeLessThan(effect.IndexOf("logisticsApi.members", StringComparison.Ordinal), "a fixed list returns before the server search");
    }
}
