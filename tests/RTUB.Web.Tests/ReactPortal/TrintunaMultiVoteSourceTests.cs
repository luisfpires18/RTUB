using FluentAssertions;

namespace RTUB.Web.Tests.ReactPortal;

/// <summary>
/// Task 166: the Trintuna date poll takes one or more dates (checkboxes), through the shared <c>PollCard</c>. Meetings polls
/// stay single-choice (radio, one option). The portal has no component test runner, so the contract is pinned in the source.
/// </summary>
public class TrintunaMultiVoteSourceTests
{
    private static string Read(params string[] path) =>
        File.ReadAllText(Path.Combine([RepoPaths.Root(), "src", "RTUB.Web", .. path])).Replace("\r\n", "\n");

    private static string Portal(params string[] path) => Read(["portal", "src", .. path]);

    [Fact]
    public void PollCard_UsesCheckboxes_OnlyForAMultiplePoll()
    {
        var card = Portal("components", "MeetingPollCard.tsx");

        card.Should().Contain("const multiple = poll.multiple === true;");
        card.Should().Contain("type={multiple ? 'checkbox' : 'radio'}");
        card.Should().Contain("checked={selected.includes(option.id)}");
        card.Should().Contain("!multiple ? [id]", "a radio choice replaces the selection");
        card.Should().Contain("onVote?: (optionIds: number[]) => Promise<Outcome<unknown>>");
    }

    [Fact]
    public void MeetingsPolls_StaySingleChoice()
    {
        Portal("components", "MeetingPollCard.tsx").Should().Contain("meetingsApi.votePoll(poll.id, selected[0])");
        Portal("pages", "MeetingPolls.tsx").Should().Contain("<PollCard poll={poll} onChanged={onChanged} />").And.NotContain("onVote");
        Portal("data", "meetingsTypes.ts").Should().Contain("multiple?: boolean;").And.Contain("myOptionIds?: number[];");
    }

    [Fact]
    public void Trintuna_SendsTheChosenDates()
    {
        Portal("data", "trintunaApi.ts").Should().Contain("vote: (id: number, optionIds: number[]) => call<Gathering>('PUT', `${one(id)}/vote`, { optionIds })");
        Portal("components", "TrintunaDialogs.tsx").Should().Contain("onVote={(optionIds) => trintunaApi.vote(g.id, optionIds)}");
    }

    [Fact]
    public void TheMigration_OnlyMovesTheUniqueIndex()
    {
        var migration = Read("Migrations", "20261127154848_AllowTrintunaMultiVote.cs");
        var up = migration[migration.IndexOf("void Up", StringComparison.Ordinal)..migration.IndexOf("void Down", StringComparison.Ordinal)];

        up.Should().NotContain("DropTable").And.NotContain("DropColumn").And.NotContain("Sql(");
        up.Should().Contain("columns: new[] { \"GatheringPollOptionId\", \"UserId\" },\n                unique: true);");
        Read("Migrations", "ApplicationDbContextModelSnapshot.cs").Should()
            .Contain("b.HasIndex(\"GatheringId\", \"UserId\");\n\n                    b.HasIndex(\"GatheringPollOptionId\", \"UserId\")\n                        .IsUnique();");
    }
}
