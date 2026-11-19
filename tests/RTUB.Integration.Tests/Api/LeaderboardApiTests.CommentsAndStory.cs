// LeaderboardApiTests: CommentsAndStory tests.
using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;

public partial class LeaderboardApiTests
{
    [Fact]
    public async Task Comments_AnyMemberWrites_NewestFirst_PushToTheMember_AndValidates()
    {
        _factory.Push.Invocations.Clear();
        var target = await AddAsync(MemberCategory.Tuno);
        var (member, me) = await SignInAsync("Member");
        await AreaHttp.WithTokenAsync(member);
        var (mod, _) = await SignInAsync("Mod");
        await AreaHttp.WithTokenAsync(mod);

        (await AreaHttp.Errors(await member.PostAsJsonAsync($"/api/leaderboard/members/{target.Id}/comments", new { text = "   " }))).Keys.Should().Contain("text");
        (await AreaHttp.Errors(await member.PostAsJsonAsync($"/api/leaderboard/members/{target.Id}/comments", new { text = new string('a', 1001) }))).Keys.Should().Contain("text");
        (await member.PostAsJsonAsync("/api/leaderboard/members/no-such-member/comments", new { text = "olá" })).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await member.PostAsJsonAsync($"/api/leaderboard/members/{target.Id}/comments", new { text = "  primeiro  " })).StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await Json(await mod.PostAsJsonAsync($"/api/leaderboard/members/{target.Id}/comments", new { text = "segundo" }));
        list.EnumerateArray().Select(c => c.GetProperty("text").GetString()).Should().Equal("segundo", "primeiro");
        list[0].EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("id", "authorName", "authorAvatarUrl", "text", "createdAt", "likes",
            "likedByMe", "canDelete", "likedBy");
        list[1].GetProperty("canDelete").GetBoolean().Should().BeFalse("the Mod did not write it");
        _factory.Push.Verify(p => p.SendToUserAsync(target.Id, It.IsAny<SendPushNotificationDto>()), Times.Exactly(2));

        await member.PostAsJsonAsync($"/api/leaderboard/members/{me.Id}/comments", new { text = "eu" });
        _factory.Push.Verify(p => p.SendToUserAsync(me.Id, It.IsAny<SendPushNotificationDto>()), Times.Never, "no push for a comment on one's own row");
        (await AreaHttp.Json(member, $"/api/leaderboard/members/{target.Id}/comments"))[1].GetProperty("canDelete").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Likes_Toggle_AndDelete_IsTheAuthorsOrAdminsAndOwners()
    {
        _factory.Push.Invocations.Clear();
        var target = await AddAsync(MemberCategory.Tuno);
        var (author, authorUser) = await SignInAsync("Member");
        await AreaHttp.WithTokenAsync(author);
        var (other, _) = await SignInAsync("Mod");
        await AreaHttp.WithTokenAsync(other);
        var (admin, _) = await SignInAsync("Admin");
        await AreaHttp.WithTokenAsync(admin);
        var (owner, _) = await SignInAsync("Owner");
        await AreaHttp.WithTokenAsync(owner);

        async Task<int> CommentAsync(string text) =>
            (await Json(await author.PostAsJsonAsync($"/api/leaderboard/members/{target.Id}/comments", new { text })))[0].GetProperty("id").GetInt32();

        var first = await CommentAsync("um");
        (await Json(await other.PostAsync($"/api/leaderboard/comments/{first}/like", null))).GetProperty("liked").GetBoolean().Should().BeTrue();
        _factory.Push.Verify(p => p.SendToUserAsync(authorUser.Id, It.IsAny<SendPushNotificationDto>()), Times.Once, "the author hears of the like");
        var liked = (await AreaHttp.Json(other, $"/api/leaderboard/members/{target.Id}/comments")).EnumerateArray().Single(c => c.GetProperty("id").GetInt32() == first);
        (liked.GetProperty("likes").GetInt32(), liked.GetProperty("likedByMe").GetBoolean()).Should().Be((1, true));
        (await Json(await other.PostAsync($"/api/leaderboard/comments/{first}/like", null))).GetProperty("liked").GetBoolean().Should().BeFalse();

        (await other.DeleteAsync($"/api/leaderboard/comments/{first}")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "a Mod deletes only their own");
        (await author.DeleteAsync($"/api/leaderboard/comments/{first}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await author.DeleteAsync($"/api/leaderboard/comments/{first}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.PostAsync($"/api/leaderboard/comments/{first}/like", null)).StatusCode.Should().Be(HttpStatusCode.NotFound, "a deleted comment cannot be liked");
        (await AreaHttp.Json(author, $"/api/leaderboard/members/{target.Id}/comments")).EnumerateArray().Should().NotContain(c => c.GetProperty("id").GetInt32() == first);

        (await admin.DeleteAsync($"/api/leaderboard/comments/{await CommentAsync("dois")}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await owner.DeleteAsync($"/api/leaderboard/comments/{await CommentAsync("três")}")).StatusCode.Should().Be(HttpStatusCode.NoContent, "the Owner inherits Admin");
        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LeaderboardComments.IgnoreQueryFilters()
            .CountAsync(c => c.TargetUserId == target.Id && c.DeletedAt != null)).Should().Be(3, "deletes stay soft, as before");
    }

    [Fact]
    public async Task Writes_NeedTheAntiforgeryHeader()
    {
        var target = await AddAsync(MemberCategory.Tuno);
        var (member, _) = await SignInAsync("Admin");

        (await member.PostAsJsonAsync($"/api/leaderboard/members/{target.Id}/comments", new { text = "sem token" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await member.PostAsync("/api/leaderboard/comments/1/like", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await member.DeleteAsync("/api/leaderboard/comments/1")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await AreaHttp.Json(member, $"/api/leaderboard/members/{target.Id}/comments")).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Story_IsTheFixedText_ForEveryRole_AndNobodyEditsIt()
    {
        // 029A: the story no longer comes from the "ranking_story" label (the Labels table itself went in 043).
        var fixedStory = RTUB.Application.Services.LeaderboardService.Story;
        foreach (var role in new[] { "Member", "Mod", "Admin", "Owner" })
        {
            var (client, _) = await SignInAsync(role);
            await AreaHttp.WithTokenAsync(client);
            var body = await AreaHttp.Json(client, "/api/leaderboard?q=zzz-none");
            var story = body.GetProperty("story");
            (story.GetProperty("title").GetString(), story.GetProperty("content").GetString(), story.GetProperty("isActive").GetBoolean())
                .Should().Be((fixedStory.Title, fixedStory.Content, true), role);
            body.TryGetProperty("canEditStory", out _).Should().BeFalse("story editing went with the Labels admin ({0})", role);
            (await client.PutAsJsonAsync("/api/leaderboard/story", new { title = "t", content = "c", isActive = true })).StatusCode
                .Should().Be(HttpStatusCode.NotFound, "the story edit endpoint was removed ({0})", role);
        }

        (await Anonymous().PutAsJsonAsync("/api/leaderboard/story", new { title = "t", content = "c", isActive = true })).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public void LeaderboardService_NoLongerDependsOnTheLabelService()
    {
        // ILabelService has no caller and is gone; RetiredRoutesTests guards that.
        typeof(RTUB.Application.Services.LeaderboardService).GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType.Name)
            .Should().NotContain(name => name.Contains("Label"));
        typeof(RTUB.Application.Interfaces.ILeaderboardService).GetMethod("UpdateStoryAsync").Should().BeNull();
    }

    [Fact]
    public void ReactFiles_SayNothingAboutMigration_AndTheMemberMenuLinksTheClassification()
    {
        var src = Path.Combine(RepoPaths.Root(), "src", "RTUB.Web", "portal", "src");
        File.ReadAllText(Path.Combine(src, "pages/Leaderboard.tsx")).Should().NotContainAny("migra", "Migra");
        File.ReadAllText(Path.Combine(src, "data/content.ts")).Should().Contain("leaderboard: '/leaderboard'");
        File.ReadAllText(Path.Combine(src, "components/MemberShell.tsx")).Should().Contain("href: portal.leaderboard,", "the member menu links it (030)");
    }
}
