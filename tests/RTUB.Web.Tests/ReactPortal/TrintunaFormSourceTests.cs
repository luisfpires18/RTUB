using FluentAssertions;

namespace RTUB.Web.Tests.ReactPortal;

/// <summary>
/// #364 (Codex round 3): "Marcar data" in the Trintuna gathering form never skips settling without a word. The portal has no
/// component test runner, so these tests pin the contract in the source: the choice is derived from the dates still in
/// the list (removing or changing the chosen date clears it), and a chosen date missing after the save shows an error
/// and keeps the form open instead of closing it.
/// </summary>
public class TrintunaFormSourceTests
{
    private static readonly string Form = File.ReadAllText(Path.Combine(
        RepoPaths.Root(), "src", "RTUB.Web", "portal", "src", "components", "TrintunaForm.tsx")).Replace("\r\n", "\n");

    [Fact]
    public void SettleChoice_OnlyOffersSavedOptionsStillInTheDateList()
    {
        Form.Should().Contain("const settleOptions = g ? g.poll.options.filter((o) => dates.includes(o.date)) : [];");
        Form.Should().Contain("const settleDate = settleOptions.some((o) => o.date === settle) ? settle : '';");
        Form.Should().Contain("value={settleDate}");
        Form.Should().Contain("{settleOptions.map((o) => (");
        Form.Should().NotContain("value={settle}", "the select shows the derived choice, so a removed date reads 'Ainda não'");
    }

    [Fact]
    public void ChosenDateMissingAfterSave_ShowsAnErrorAndKeepsTheFormOpen()
    {
        var submit = Form[Form.IndexOf("const submit", StringComparison.Ordinal)..Form.IndexOf("const organizers", StringComparison.Ordinal)];

        submit.Should().Contain("if (settleDate && !option) return setErrors({ settle:");
        submit.IndexOf("if (settleDate && !option)", StringComparison.Ordinal)
            .Should().BeLessThan(submit.IndexOf("onClose();", StringComparison.Ordinal), "the error returns before the form closes");
    }
}
