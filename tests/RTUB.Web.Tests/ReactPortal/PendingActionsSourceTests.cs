using FluentAssertions;
using RTUB.Application.Services;
using Xunit;

namespace RTUB.Web.Tests.ReactPortal;

/// <summary>
/// Task 072 (docs/react-pending-actions.md): the answer dialog's note limit is each module's own, so a note the event or
/// rehearsal page accepts is never cut short on /pending-actions (PR #273 review). Source contract: no JS test runner.
/// </summary>
public class PendingActionsSourceTests
{
    [Fact]
    public void TheAnswerDialog_UsesEachModulesNoteLimit()
    {
        var page = File.ReadAllText(Path.Combine(FindRoot(), "src", "RTUB.Web", "portal", "src", "PendingActions.tsx"));

        page.Should().Contain($"'{EventResponsePendingActionSource.KindCode}': {EventAgendaService.MaxNotesLength}")
            .And.Contain($"'{RehearsalResponsePendingActionSource.KindCode}': {RehearsalAgendaService.NotesMax}")
            .And.Contain("maxLength={NOTES_MAX[row.kind]");
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "RTUB.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }
}
