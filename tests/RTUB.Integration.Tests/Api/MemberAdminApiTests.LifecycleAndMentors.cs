// MemberAdminApiTests: LifecycleAndMentors tests.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;

public partial class MemberAdminApiTests
{
    [Fact]
    public async Task ExpelAndReactivate_AreForLeitoes()
    {
        var (admin, _) = await SignInAsync("Admin");
        await AreaHttp.WithTokenAsync(admin);
        var leitao = await AddAsync(MemberCategory.Leitao);
        var tuno = await AddAsync(MemberCategory.Tuno);

        (await admin.PostAsync($"/api/members/{tuno.Id}/expel", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.PostAsync($"/api/members/{leitao.Id}/expel", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await UserAsync(leitao.Id))!.IsExpelled.Should().BeTrue();
        (await admin.GetFromJsonAsync<JsonElement>($"/api/members/{leitao.Id}")).GetProperty("expelled").GetBoolean().Should().BeTrue();
        (await admin.PostAsync($"/api/members/{leitao.Id}/reactivate", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await UserAsync(leitao.Id))!.IsExpelled.Should().BeFalse();
        (await UserAsync(tuno.Id))!.IsExpelled.Should().BeFalse();
    }

    [Fact]
    public async Task TheReminder_FollowsTheOldListsConditions_AndMakeActiveIsGone()
    {
        var (admin, _) = await SignInAsync("Admin");
        await AreaHttp.WithTokenAsync(admin);
        var activeTuno = await AddAsync(MemberCategory.Tuno, yearTuno: 2020);
        var almostBack = await AddAsync(MemberCategory.Tuno, yearTuno: 2015, retired: true);
        // Two completed months with a rehearsal, none this month: 2/3 on the way back.
        var thisMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        await AttendAsync(almostBack.Id, thisMonth.AddMonths(-1).AddDays(9));
        await AttendAsync(almostBack.Id, thisMonth.AddMonths(-2).AddDays(10));

        var row = (await admin.GetFromJsonAsync<JsonElement>($"/api/members/active?q={Uri.EscapeDataString(almostBack.Nickname!)}")).EnumerateArray().Single();
        row.GetProperty("encourage").GetBoolean().Should().BeTrue();
        row.TryGetProperty("canMakeActive", out _).Should().BeFalse("\"Tornar ativo\" and its permanent override were removed in 044B");

        (await admin.PostAsync($"/api/members/{activeTuno.Id}/reminder", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _factory.Push.Verify(p => p.SendToUserAsync(activeTuno.Id, It.IsAny<SendPushNotificationDto>()), Times.Never);

        (await admin.PostAsync($"/api/members/{almostBack.Id}/reminder", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        _factory.Push.Verify(p => p.SendToUserAsync(almostBack.Id, It.IsAny<SendPushNotificationDto>()), Times.Once);

        (await admin.PostAsync($"/api/members/{almostBack.Id}/activate", null)).IsSuccessStatusCode.Should().BeFalse();
        (await UserAsync(almostBack.Id))!.IsRetired.Should().BeTrue("only three consecutive months of activity bring a Reformado back");
    }

    [Fact]
    public async Task Delete_IsTheOldHardDelete_OwnerAnyMember_AdminLeitoesOnly()
    {
        var (admin, _) = await SignInAsync("Admin");
        await AreaHttp.WithTokenAsync(admin);
        var (owner, _) = await SignInAsync("Owner");
        await AreaHttp.WithTokenAsync(owner);
        var leitao = await AddAsync(MemberCategory.Leitao);
        var tuno = await AddAsync(MemberCategory.Tuno);

        (await admin.GetFromJsonAsync<JsonElement>("/api/members?q=zzz-none")).GetProperty("canDeleteMembers").GetBoolean().Should().BeFalse();
        (await owner.GetFromJsonAsync<JsonElement>("/api/members?q=zzz-none")).GetProperty("canDeleteMembers").GetBoolean().Should().BeTrue();

        (await admin.DeleteAsync($"/api/members/{tuno.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await UserAsync(tuno.Id)).Should().NotBeNull();
        (await admin.DeleteAsync($"/api/members/{leitao.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await UserAsync(leitao.Id)).Should().BeNull();
        (await owner.DeleteAsync($"/api/members/{tuno.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await UserAsync(tuno.Id)).Should().BeNull();
        (await owner.DeleteAsync($"/api/members/{tuno.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_AMemberNamedOnAnMbwayTransfer_IsRefused_BeforeAnythingOfTheirsIsDeleted()
    {
        var (owner, _) = await SignInAsync("Owner");
        await AreaHttp.WithTokenAsync(owner);
        var tuno = await AddAsync(MemberCategory.Tuno);
        int commentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.MbwayTransfers.Add(new MbwayTransfer { Date = DateTime.Today, Amount = 3m, MemberUserId = tuno.Id, TransferTo = "Tesouraria" });
            var comment = LeaderboardComment.Create(tuno.Id, tuno.Id, "texto");
            db.LeaderboardComments.Add(comment);
            await db.SaveChangesAsync();
            commentId = comment.Id;
        }

        var response = await owner.DeleteAsync($"/api/members/{tuno.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, "the transfer blocks the delete in the database");
        (await response.Content.ReadAsStringAsync()).Should().Contain("members:in-use");
        (await UserAsync(tuno.Id)).Should().NotBeNull();
        using var check = _factory.Services.CreateScope();
        (await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().LeaderboardComments.AnyAsync(c => c.Id == commentId))
            .Should().BeTrue("a refused delete removes nothing");
    }

    [Fact]
    public async Task Delete_AMemberWhoLikedAnotherMembersComment_RemovesTheLike_AndTheMember()
    {
        var (owner, _) = await SignInAsync("Owner");
        await AreaHttp.WithTokenAsync(owner);
        var tuno = await AddAsync(MemberCategory.Tuno);
        var other = await AddAsync(MemberCategory.Tuno);
        int commentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var comment = LeaderboardComment.Create(other.Id, other.Id, "texto");
            db.LeaderboardComments.Add(comment);
            await db.SaveChangesAsync();
            db.LeaderboardCommentLikes.Add(LeaderboardCommentLike.Create(comment.Id, tuno.Id));
            await db.SaveChangesAsync();
            commentId = comment.Id;
        }

        (await owner.DeleteAsync($"/api/members/{tuno.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await UserAsync(tuno.Id)).Should().BeNull();
        using var check = _factory.Services.CreateScope();
        var after = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await after.LeaderboardComments.AnyAsync(c => c.Id == commentId)).Should().BeTrue("the comment is another member's");
        (await after.LeaderboardCommentLikes.AnyAsync(l => l.CommentId == commentId)).Should().BeFalse();
    }

    [Fact]
    public async Task MentorSearch_OffersTunosAndAbove_WithoutPrivateFields_AndLeavesOutTheMemberEdited()
    {
        var (admin, _) = await SignInAsync("Admin");
        var tag = Unique();
        var tuno = await AddAsync(MemberCategory.Tuno, yearTuno: 2010, nickname: $"Padrinho {tag}");
        await AddAsync(MemberCategory.Caloiro, nickname: $"Caloiro {tag}");

        var found = await admin.GetFromJsonAsync<JsonElement>($"/api/members/mentors?q={tag}");
        found.EnumerateArray().Select(m => m.GetProperty("id").GetString()).Should().Equal(tuno.Id);
        found[0].EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("id", "displayName", "fullName", "avatarUrl");
        (await admin.GetFromJsonAsync<JsonElement>($"/api/members/mentors?q={tag}&exclude={tuno.Id}")).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task TheBlazorBridgeIsGone_AndTheReactFilesSayNothingAboutMigration()
    {
        var (admin, _) = await SignInAsync("Admin");
        foreach (var client in new[] { admin, Anonymous() })
        {
            (await client.GetAsync("/members/manage")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        var web = typeof(RTUB.Program).Assembly;
        web.GetType("RTUB.Pages.Members.Members").Should().BeNull("the Blazor /members/manage page was retired");
        web.GetTypes()
            .SelectMany(t => t.GetCustomAttributes(typeof(RouteAttribute), false).Cast<RouteAttribute>())
            .Select(r => r.Template)
            .Should().NotContain(new[] { "/members/manage", "/member/events", "/member/gallery", "/member/roles", "/hierarchy" });

        var src = Path.Combine(RepoPaths.Root(), "src", "RTUB.Web", "portal", "src");
        // MemberManage.tsx was split (113) into the Member*Dialog / MemberFormFields files and data/memberFormState.ts.
        var memberFiles = new[] { "pages/Members.tsx", "components/MemberDialogs.tsx", "components/MemberFormDialog.tsx", "components/MemberFormFields.tsx",
            "components/MemberInfoDialog.tsx", "components/MemberActionDialogs.tsx", "data/memberFormState.ts", "data/membersApi.ts" };
        foreach (var file in memberFiles)
        {
            var text = File.ReadAllText(Path.Combine(src, file));
            text.Should().NotContainAny(new[] { "migra", "Migra" }, file).And.NotContain("href=\"/members/manage\"", file);
        }
    }
}
