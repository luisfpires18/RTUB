using FluentAssertions;

namespace RTUB.Web.Tests.ReactPortal;

/// <summary>
/// Task 156: on /trintuna picking Vou / Não vou (Fui / Não fui) saves at once; there is no Guardar / Fechar pair in the
/// answer. The portal has no component test runner, so the contract is pinned in the source.
/// </summary>
public class TrintunaPresenceSourceTests
{
    private static readonly string Dialogs = File.ReadAllText(Path.Combine(
        RepoPaths.Root(), "src", "RTUB.Web", "portal", "src", "components", "TrintunaDialogs.tsx")).Replace("\r\n", "\n");

    private static string Presence() =>
        Dialogs[Dialogs.IndexOf("function Presence", StringComparison.Ordinal)..Dialogs.IndexOf("const asParticipant", StringComparison.Ordinal)];

    [Fact]
    public void PickingAnAnswer_SavesAtOnce_WithoutAGuardarStep()
    {
        var presence = Presence();

        presence.Should().Contain("onChange={() => pick(true)}").And.Contain("onChange={() => pick(false)}");
        presence.Should().Contain("trintunaApi.answer(gathering.id, value)");
        presence.Should().NotContain("AnswerActions").And.NotContain("label=\"Guardar\"").And.NotContain("<form");
        presence.Should().Contain("setChoice(stored);", "a failed save shows the stored answer again");
    }
}
