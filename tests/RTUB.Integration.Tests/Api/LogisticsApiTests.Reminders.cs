using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Factories;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;
/// <summary>
/// Task 069 (issue #249): card reminders (who may schedule what), "As minhas tarefas", the assignment / mention pushes and
/// reminder delivery with a controlled clock (Portugal DST, restarts, overlapping runs, transient failures, the pre-069
/// rollout). Delivery runs here by hand: the host's worker is off in tests (LogisticsReminders:Enabled = false).
/// </summary>
public partial class LogisticsApiTests
{
    private static object Remind(string frequency = "OneTime", DateTime? at = null, params string[] userIds) =>
        new { frequency, nextReminderDate = at ?? DateTime.Today.AddDays(2).AddHours(10), userIds };

    [Fact]
    public async Task Reminders_MembersScheduleOnlyForThemselves_ManagersOnlyForBoardViewers()
    {
        var (boardId, _, cardId) = await SeedBoardAsync();
        var (mod, modUser) = await SignInAsync("Mod");
        var (worker, workerUser) = await SignInAsync();
        var (other, otherUser) = await SignInAsync();
        var (viewer, _) = await SignInAsync();
        foreach (var c in new[] { mod, worker, other, viewer })
        {
            await AreaHttp.WithTokenAsync(c);
        }

        await UpdateCardAsync(cardId, c => c.AssignToUser(workerUser.Id));
        await AddCollaboratorAsync(cardId, otherUser.Id);
        var url = $"/api/logistics/cards/{cardId}/reminders";

        // A member who only reads the card schedules nothing; a worker never for somebody else.
        (await viewer.PostAsJsonAsync(url, Remind())).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await worker.PostAsJsonAsync(url, Remind("OneTime", null, otherUser.Id))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await AreaHttp.Errors(await worker.PostAsJsonAsync(url, Remind("Hourly", DateTime.Today.AddDays(-1))))).Keys
            .Should().BeEquivalentTo("frequency", "nextReminderDate");

        var mine = await Json(await worker.PostAsJsonAsync(url, Remind("Weekly")));
        var reminder = mine.GetProperty("reminders").EnumerateArray().Single();
        (reminder.GetProperty("personal").GetBoolean(), reminder.GetProperty("canEdit").GetBoolean()).Should().Be((true, true));
        var reminderId = reminder.GetProperty("id").GetInt32();
        (await ReminderAsync(reminderId)).Should().Match<LogisticsCardReminder>(r =>
            r.TargetUserIds == workerUser.Id && r.Personal && r.CreatedByUserId == workerUser.Id && r.ActivatedAt != null);

        // Another worker neither sees nor changes it; a manager sees and may change every reminder.
        var others = await AreaHttp.Json(other, $"/api/logistics/cards/{cardId}");
        others.GetProperty("reminders").GetArrayLength().Should().Be(0);
        others.GetProperty("canRemind").GetBoolean().Should().BeTrue();
        (await other.PutAsJsonAsync($"{url}/{reminderId}", Remind("Daily"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await other.DeleteAsync($"{url}/{reminderId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await AreaHttp.Json(mod, $"/api/logistics/cards/{cardId}")).GetProperty("reminders").GetArrayLength().Should().Be(1);
        (await viewer.GetAsync($"/api/logistics/cards/{cardId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await AreaHttp.Json(viewer, $"/api/logistics/cards/{cardId}")).GetProperty("canRemind").GetBoolean().Should().BeFalse();

        // Its creator edits it; the edit keeps it theirs.
        var edited = await Json(await worker.PutAsJsonAsync($"{url}/{reminderId}", Remind("Daily", DateTime.Today.AddDays(3).AddHours(9), otherUser.Id)));
        edited.GetProperty("reminders").EnumerateArray().Single().GetProperty("frequency").GetString().Should().Be("Daily");
        (await ReminderAsync(reminderId))!.TargetUserIds.Should().Be(workerUser.Id);

        // A manager targets board viewers only (a private board's outsider is refused).
        await UpdateBoardAsync(boardId, b => b.Visibility = BoardVisibility.Private);
        (await AreaHttp.Errors(await mod.PostAsJsonAsync(url, Remind("OneTime", null, otherUser.Id)))).Keys.Should().Equal("userIds");
        await AddBoardMemberAsync(boardId, otherUser.Id);
        await AddBoardMemberAsync(boardId, workerUser.Id);
        var managed = await Json(await mod.PostAsJsonAsync(url, Remind("OneTime", null, otherUser.Id)));
        managed.GetProperty("reminders").GetArrayLength().Should().Be(2);
        (await ReminderAsync(managed.GetProperty("reminders").EnumerateArray().Single(r => !r.GetProperty("personal").GetBoolean()).GetProperty("id").GetInt32()))!
            .Should().Match<LogisticsCardReminder>(r => !r.Personal && r.TargetUserIds == otherUser.Id && r.CreatedByUserId == modUser.Id);

        // A worker who loses the card can no longer change their reminder; a manager deletes it.
        await UpdateCardAsync(cardId, c => c.AssignToUser(null));
        (await worker.DeleteAsync($"{url}/{reminderId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await mod.DeleteAsync($"{url}/{reminderId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReminderAsync(reminderId)).Should().BeNull();
    }

    [Fact]
    public async Task MyTasks_ListOnlyAccessibleAssignedWork_AndUnownedCardsToManagersOnly()
    {
        var (open, openList, owned) = await SeedBoardAsync(cards: 3);
        var (hidden, _, privateCard) = await SeedBoardAsync();
        var (leitao, me) = await SignInAsync();
        var (mod, _) = await SignInAsync("Mod");
        await UpdateUserAsync(me.Id, u => u.Categories = new List<MemberCategory> { MemberCategory.Leitao });
        var cards = await CardIdsAsync(openList);
        var (collaborating, archived) = (cards[1], cards[2]);
        await UpdateCardAsync(owned, c => c.AssignToUser(me.Id));
        await AddCollaboratorAsync(collaborating, me.Id);
        await UpdateCardAsync(collaborating, c => c.SetStatus(CardStatus.Blocked));
        await UpdateCardAsync(archived, c => { c.AssignToUser(me.Id); c.ArchivedAt = DateTime.UtcNow; });
        // Assigned on a private board without access: not listed (assignment never grants access).
        await UpdateBoardAsync(hidden, b => b.Visibility = BoardVisibility.Private);
        await UpdateCardAsync(privateCard, c => c.AssignToUser(me.Id));

        var mine = await AreaHttp.Json(leitao, "/api/logistics/my-tasks");
        mine.GetProperty("canManage").GetBoolean().Should().BeFalse();
        var listed = mine.GetProperty("tasks").EnumerateArray().ToDictionary(t => t.GetProperty("cardId").GetInt32(), t => t.GetProperty("role").GetString());
        listed.Should().Equal(new Dictionary<int, string?> { [owned] = "owner", [collaborating] = "collaborator" });

        await AddBoardMemberAsync(hidden, me.Id);
        (await AreaHttp.Json(leitao, "/api/logistics/my-tasks")).GetProperty("tasks").EnumerateArray().Select(t => t.GetProperty("cardId").GetInt32())
            .Should().Contain(privateCard);

        // Managers also get the open cards nobody owns (here: the collaborating one); members never.
        var managed = await AreaHttp.Json(mod, "/api/logistics/my-tasks");
        var unowned = managed.GetProperty("tasks").EnumerateArray().Where(t => t.GetProperty("role").ValueKind == JsonValueKind.Null)
            .Select(t => t.GetProperty("cardId").GetInt32()).ToList();
        unowned.Should().Contain(collaborating).And.NotContain(new[] { owned, archived, privateCard });
        (await Anonymous().GetAsync("/api/logistics/my-tasks")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AssignmentAndMentionPushes_ReachEachRecipientOnce_NeverTheActor()
    {
        var (boardId, _, cardId) = await SeedBoardAsync();
        var (mod, modUser) = await SignInAsync("Mod");
        var (worker, workerUser) = await SignInAsync();
        var (helper, helperUser) = await SignInAsync();
        foreach (var c in new[] { mod, worker, helper })
        {
            await AreaHttp.WithTokenAsync(c);
        }

        var detail = await AreaHttp.Json(mod, $"/api/logistics/cards/{cardId}");
        var save = new
        {
            title = "Comprar cordas", description = "Detalhe privado", priority = "Normal", startDate = (string?)null, dueDate = (string?)null,
            assignedToUserId = workerUser.Id, version = detail.GetProperty("version").GetInt32(),
        };
        (await mod.PutAsJsonAsync($"/api/logistics/cards/{cardId}", save)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await mod.PostAsJsonAsync($"/api/logistics/cards/{cardId}/assignments", new { userId = helperUser.Id })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await mod.PostAsJsonAsync($"/api/logistics/cards/{cardId}/assignments", new { userId = modUser.Id })).StatusCode.Should().Be(HttpStatusCode.OK);
        var posted = await Json(await worker.PostAsJsonAsync($"/api/logistics/cards/{cardId}/comments",
            new { body = "@mod vê isto", mentionUserIds = new[] { modUser.Id, workerUser.Id } }));
        var commentId = posted.GetProperty("comments").EnumerateArray().Last().GetProperty("id").GetInt32();
        (await worker.PutAsJsonAsync($"/api/logistics/comments/{commentId}", new { body = "@mod vê isto, editado" })).StatusCode.Should().Be(HttpStatusCode.OK);

        var link = $"/logistics/{boardId}?card={cardId}";
        var worker1 = await InboxAsync(workerUser.Id, link);
        worker1.Should().ContainSingle().Which.Should().Contain("És o responsável por «Comprar cordas»").And.NotContain("Detalhe privado");
        (await InboxAsync(helperUser.Id, link)).Should().ContainSingle().Which.Should().Contain("colaborador");
        (await InboxAsync(modUser.Id, link)).Should().ContainSingle("the mention only: adding themselves never notifies the actor")
            .Which.Should().Contain("mencionou-te").And.NotContain("vê isto");
    }

    // ---------------------------------------------------------------- delivery (controlled time)

    private sealed record Sent(string[] To, SendPushNotificationDto Push);

    private (LogisticsReminderDelivery Delivery, TestClock Clock, List<Sent> Sent, Mock<IPushNotificationService> Push) Delivery(DateTime utc)
    {
        var clock = new TestClock(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)));
        var sent = new List<Sent>();
        var push = new Mock<IPushNotificationService>();
        push.Setup(p => p.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<SendPushNotificationDto>()))
            .Callback<IEnumerable<string>, SendPushNotificationDto>((to, dto) => { lock (sent) sent.Add(new Sent(to.ToArray(), dto)); })
            .ReturnsAsync((1, 0));
        var delivery = new LogisticsReminderDelivery(_factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>(), push.Object,
            new PushNotificationFactory(clock), NullLogger<LogisticsReminderDelivery>.Instance, clock);
        return (delivery, clock, sent, push);
    }

    private async Task<LogisticsCardReminder?> ReminderAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LogisticsCardReminders.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
    }

    private async Task AddCollaboratorAsync(int cardId, string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.LogisticsCardAssignments.Add(LogisticsCardAssignment.Create(cardId, userId));
        await db.SaveChangesAsync();
    }

    private async Task AddBoardMemberAsync(int boardId, string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.LogisticsBoardMembers.Add(new LogisticsBoardMember { BoardId = boardId, UserId = userId });
        await db.SaveChangesAsync();
    }

    /// <summary>The bodies of the member's "Notificações RTUB" messages that link to <paramref name="link"/>.</summary>
    private async Task<List<string>> InboxAsync(string userId, string link)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Messages.AsNoTracking()
            .Where(m => m.IsSystem && m.Link == link && m.Conversation!.IsSystemConversation && m.Conversation.Participants == userId)
            .Select(m => m.Body).ToListAsync();
    }
}
