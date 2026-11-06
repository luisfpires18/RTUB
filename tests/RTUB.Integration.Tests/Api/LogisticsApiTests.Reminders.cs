using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Factories;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

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
            await WithTokenAsync(c);
        }

        await UpdateCardAsync(cardId, c => c.AssignToUser(workerUser.Id));
        await AddCollaboratorAsync(cardId, otherUser.Id);
        var url = $"/api/logistics/cards/{cardId}/reminders";

        // A member who only reads the card schedules nothing; a worker never for somebody else.
        (await viewer.PostAsJsonAsync(url, Remind())).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await worker.PostAsJsonAsync(url, Remind("OneTime", null, otherUser.Id))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Errors(await worker.PostAsJsonAsync(url, Remind("Hourly", DateTime.Today.AddDays(-1))))).Keys
            .Should().BeEquivalentTo("frequency", "nextReminderDate");

        var mine = await Json(await worker.PostAsJsonAsync(url, Remind("Weekly")));
        var reminder = mine.GetProperty("reminders").EnumerateArray().Single();
        (reminder.GetProperty("personal").GetBoolean(), reminder.GetProperty("canEdit").GetBoolean()).Should().Be((true, true));
        var reminderId = reminder.GetProperty("id").GetInt32();
        (await ReminderAsync(reminderId)).Should().Match<LogisticsCardReminder>(r =>
            r.TargetUserIds == workerUser.Id && r.Personal && r.CreatedByUserId == workerUser.Id && r.ActivatedAt != null);

        // Another worker neither sees nor changes it; a manager sees and may change every reminder.
        var others = await Json(other, $"/api/logistics/cards/{cardId}");
        others.GetProperty("reminders").GetArrayLength().Should().Be(0);
        others.GetProperty("canRemind").GetBoolean().Should().BeTrue();
        (await other.PutAsJsonAsync($"{url}/{reminderId}", Remind("Daily"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await other.DeleteAsync($"{url}/{reminderId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Json(mod, $"/api/logistics/cards/{cardId}")).GetProperty("reminders").GetArrayLength().Should().Be(1);
        (await viewer.GetAsync($"/api/logistics/cards/{cardId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Json(viewer, $"/api/logistics/cards/{cardId}")).GetProperty("canRemind").GetBoolean().Should().BeFalse();

        // Its creator edits it; the edit keeps it theirs.
        var edited = await Json(await worker.PutAsJsonAsync($"{url}/{reminderId}", Remind("Daily", DateTime.Today.AddDays(3).AddHours(9), otherUser.Id)));
        edited.GetProperty("reminders").EnumerateArray().Single().GetProperty("frequency").GetString().Should().Be("Daily");
        (await ReminderAsync(reminderId))!.TargetUserIds.Should().Be(workerUser.Id);

        // A manager targets board viewers only (a private board's outsider is refused).
        await UpdateBoardAsync(boardId, b => b.Visibility = BoardVisibility.Private);
        (await Errors(await mod.PostAsJsonAsync(url, Remind("OneTime", null, otherUser.Id)))).Keys.Should().Equal("userIds");
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

        var mine = await Json(leitao, "/api/logistics/my-tasks");
        mine.GetProperty("canManage").GetBoolean().Should().BeFalse();
        var listed = mine.GetProperty("tasks").EnumerateArray().ToDictionary(t => t.GetProperty("cardId").GetInt32(), t => t.GetProperty("role").GetString());
        listed.Should().Equal(new Dictionary<int, string?> { [owned] = "owner", [collaborating] = "collaborator" });

        await AddBoardMemberAsync(hidden, me.Id);
        (await Json(leitao, "/api/logistics/my-tasks")).GetProperty("tasks").EnumerateArray().Select(t => t.GetProperty("cardId").GetInt32())
            .Should().Contain(privateCard);

        // Managers also get the open cards nobody owns (here: the collaborating one); members never.
        var managed = await Json(mod, "/api/logistics/my-tasks");
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
            await WithTokenAsync(c);
        }

        var detail = await Json(mod, $"/api/logistics/cards/{cardId}");
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

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed record Sent(string[] To, SendPushNotificationDto Push);

    private (LogisticsReminderDelivery Delivery, Clock Clock, List<Sent> Sent, Mock<IPushNotificationService> Push) Delivery(DateTime utc)
    {
        var clock = new Clock { Now = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)) };
        var sent = new List<Sent>();
        var push = new Mock<IPushNotificationService>();
        push.Setup(p => p.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<SendPushNotificationDto>()))
            .Callback<IEnumerable<string>, SendPushNotificationDto>((to, dto) => { lock (sent) sent.Add(new Sent(to.ToArray(), dto)); })
            .ReturnsAsync((1, 0));
        var delivery = new LogisticsReminderDelivery(_factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>(), push.Object,
            new PushNotificationFactory(clock), NullLogger<LogisticsReminderDelivery>.Instance, clock);
        return (delivery, clock, sent, push);
    }

    private static List<Sent> For(List<Sent> sent, int cardId) => sent.Where(s => s.Push.Url!.EndsWith($"?card={cardId}")).ToList();

    [Fact]
    public async Task Delivery_FollowsPortugalTime_AcrossBothDstChanges()
    {
        var (_, _, cardId) = await SeedBoardAsync();
        var (_, user) = await SignInAsync();
        var daily = await SeedReminderAsync(cardId, ReminderFrequency.Daily, new DateTime(2027, 3, 27, 10, 0, 0), user.Id);
        var (delivery, clock, sent, _) = Delivery(new DateTime(2027, 3, 27, 9, 59, 0));

        await delivery.RunOnceAsync();
        For(sent, cardId).Should().BeEmpty("10:00 in Lisbon on 27 March 2027 is 10:00 UTC (WET)");
        clock.Now = new DateTimeOffset(2027, 3, 27, 10, 0, 0, TimeSpan.Zero);
        await delivery.RunOnceAsync();
        For(sent, cardId).Should().ContainSingle().Which.To.Should().Equal(user.Id);
        (await ReminderAsync(daily))!.NextReminderDate.Should().Be(new DateTime(2027, 3, 28, 10, 0, 0), "still 10:00 the next day");

        // 28 March: summer time, 10:00 in Lisbon is 09:00 UTC.
        clock.Now = new DateTimeOffset(2027, 3, 28, 8, 59, 0, TimeSpan.Zero);
        await delivery.RunOnceAsync();
        For(sent, cardId).Should().HaveCount(1);
        clock.Now = new DateTimeOffset(2027, 3, 28, 9, 0, 0, TimeSpan.Zero);
        await delivery.RunOnceAsync();
        For(sent, cardId).Should().HaveCount(2);

        // Autumn: 01:30 happens twice on 31 October 2027; the reminder fires at the first one only.
        var (_, _, autumnCard) = await SeedBoardAsync();
        var repeated = await SeedReminderAsync(autumnCard, ReminderFrequency.Daily, new DateTime(2027, 10, 31, 1, 30, 0), user.Id);
        clock.Now = new DateTimeOffset(2027, 10, 31, 0, 30, 0, TimeSpan.Zero);
        await delivery.RunOnceAsync();
        clock.Now = new DateTimeOffset(2027, 10, 31, 1, 30, 0, TimeSpan.Zero);
        await delivery.RunOnceAsync();
        For(sent, autumnCard).Should().ContainSingle();
        (await ReminderAsync(repeated))!.NextReminderDate.Should().Be(new DateTime(2027, 11, 1, 1, 30, 0));

        // Spring gap: 01:30 does not exist on 28 March 2027; it fires when the clock jumps past it (01:00 UTC + 30 min).
        var (_, _, springCard) = await SeedBoardAsync();
        await SeedReminderAsync(springCard, ReminderFrequency.OneTime, new DateTime(2027, 3, 28, 1, 30, 0), user.Id);
        clock.Now = new DateTimeOffset(2027, 3, 28, 0, 59, 0, TimeSpan.Zero);
        await delivery.RunOnceAsync();
        For(sent, springCard).Should().BeEmpty();
        clock.Now = new DateTimeOffset(2027, 3, 28, 1, 30, 0, TimeSpan.Zero);
        await delivery.RunOnceAsync();
        For(sent, springCard).Should().ContainSingle();
    }

    [Fact]
    public async Task Delivery_ClaimsEachOccurrenceOnce_AcrossOverlappingRunsAndRestarts()
    {
        var (_, _, cardId) = await SeedBoardAsync();
        var (_, user) = await SignInAsync();
        var id = await SeedReminderAsync(cardId, ReminderFrequency.Weekly, new DateTime(2027, 5, 3, 10, 0, 0), user.Id);
        var at = new DateTime(2027, 5, 3, 9, 0, 0); // 10:00 WEST
        var (a, _, sentA, _) = Delivery(at);
        var (b, _, sentB, _) = Delivery(at);

        await Task.WhenAll(a.RunOnceAsync(), b.RunOnceAsync());
        (For(sentA, cardId).Count + For(sentB, cardId).Count).Should().Be(1, "two runs at once still send one occurrence once");
        (await ReminderAsync(id))!.NextReminderDate.Should().Be(new DateTime(2027, 5, 10, 10, 0, 0));

        // A run that died holding the next occurrence: nobody sends it while the claim lasts; it is sent after.
        var next = new DateTime(2027, 5, 10, 9, 0, 0);
        await SetReminderAsync(id, r => r.ClaimedUntil = next.AddMinutes(10));
        var (c, clock, sentC, _) = Delivery(next);
        await c.RunOnceAsync();
        For(sentC, cardId).Should().BeEmpty();
        clock.Now = new DateTimeOffset(next.AddMinutes(11), TimeSpan.Zero);
        await c.RunOnceAsync();
        For(sentC, cardId).Should().ContainSingle();
    }

    [Fact]
    public async Task Delivery_RetriesATransientFailure_ThenSkipsTheOccurrence_AndSkipsLongMissedOnes()
    {
        var (_, _, cardId) = await SeedBoardAsync();
        var (_, user) = await SignInAsync();
        var id = await SeedReminderAsync(cardId, ReminderFrequency.Daily, new DateTime(2027, 6, 1, 10, 0, 0), user.Id);
        var at = new DateTime(2027, 6, 1, 9, 0, 0);
        var (delivery, clock, sent, push) = Delivery(at);
        var calls = 0;
        push.Setup(p => p.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<SendPushNotificationDto>()))
            .Returns<IEnumerable<string>, SendPushNotificationDto>((to, dto) =>
                ++calls == 1 ? throw new HttpRequestException("push service down") : Task.FromResult((1, 0)));

        await delivery.RunOnceAsync();
        (await ReminderAsync(id))!.Should().Match<LogisticsCardReminder>(r => r.Attempts == 1 && r.NextReminderDate == new DateTime(2027, 6, 1, 10, 0, 0));
        await delivery.RunOnceAsync();
        calls.Should().Be(1, "the retry waits 2 minutes");
        clock.Now = new DateTimeOffset(at.AddMinutes(2).AddSeconds(1), TimeSpan.Zero);
        await delivery.RunOnceAsync();
        calls.Should().Be(2);
        (await ReminderAsync(id))!.Should().Match<LogisticsCardReminder>(r => r.Attempts == 0 && r.LastSentAt != null
            && r.NextReminderDate == new DateTime(2027, 6, 2, 10, 0, 0));

        // Always failing: three attempts, then the occurrence is skipped (the next day's stays).
        push.Setup(p => p.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<SendPushNotificationDto>()))
            .ThrowsAsync(new HttpRequestException("still down"));
        var day2 = new DateTime(2027, 6, 2, 9, 0, 0);
        foreach (var minutes in new[] { 0, 3, 8 })
        {
            clock.Now = new DateTimeOffset(day2.AddMinutes(minutes), TimeSpan.Zero);
            await delivery.RunOnceAsync();
        }

        (await ReminderAsync(id))!.Should().Match<LogisticsCardReminder>(r => r.IsActive && r.Attempts == 0
            && r.NextReminderDate == new DateTime(2027, 6, 3, 10, 0, 0));

        // The app was down for a day: the missed occurrence is skipped, not sent late.
        push.Setup(p => p.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<SendPushNotificationDto>()))
            .Callback<IEnumerable<string>, SendPushNotificationDto>((to, dto) => sent.Add(new Sent(to.ToArray(), dto)))
            .ReturnsAsync((1, 0));
        clock.Now = new DateTimeOffset(2027, 6, 3, 20, 0, 0, TimeSpan.Zero);
        await delivery.RunOnceAsync();
        For(sent, cardId).Should().BeEmpty();
        (await ReminderAsync(id))!.NextReminderDate.Should().Be(new DateTime(2027, 6, 4, 10, 0, 0));
    }

    [Fact]
    public async Task Delivery_StopsForDoneArchivedOrRevokedWork_AndPersonalRemindersFollowTheAssignment()
    {
        var (boardId, listId, _) = await SeedBoardAsync(cards: 5);
        var (_, member) = await SignInAsync();
        var (_, expelled) = await SignInAsync();
        var cards = await CardIdsAsync(listId);
        var when = new DateTime(2027, 7, 1, 10, 0, 0);
        var done = await SeedReminderAsync(cards[0], ReminderFrequency.Daily, when, member.Id);
        var archived = await SeedReminderAsync(cards[1], ReminderFrequency.Daily, when, member.Id);
        var personal = await SeedReminderAsync(cards[2], ReminderFrequency.Daily, when, member.Id, personal: true, creator: member.Id);
        var gone = await SeedReminderAsync(cards[3], ReminderFrequency.Daily, when, expelled.Id);
        var kept = await SeedReminderAsync(cards[4], ReminderFrequency.Daily, when, member.Id);
        await UpdateCardAsync(cards[0], c => c.SetStatus(CardStatus.Done));
        await UpdateCardAsync(cards[1], c => c.ArchivedAt = DateTime.UtcNow);
        await UpdateUserAsync(expelled.Id, u => u.IsExpelled = true);
        // cards[2]: the member is no longer its owner or collaborator.

        var (delivery, _, sent, _) = Delivery(new DateTime(2027, 7, 1, 9, 0, 0));
        await delivery.RunOnceAsync();
        foreach (var stopped in new[] { done, archived, personal, gone })
        {
            (await ReminderAsync(stopped))!.IsActive.Should().BeFalse();
        }

        cards.Take(4).SelectMany(c => For(sent, c)).Should().BeEmpty();
        For(sent, cards[4]).Should().ContainSingle();
        For(sent, cards[4])[0].Push.Url.Should().Be($"/logistics/{boardId}?card={cards[4]}");

        // A private board: only its access list (here: nobody) - the reminder stops instead of reaching an outsider.
        await UpdateBoardAsync(boardId, b => b.Visibility = BoardVisibility.Private);
        var (later, _, laterSent, _) = Delivery(new DateTime(2027, 7, 2, 9, 0, 0));
        await later.RunOnceAsync();
        For(laterSent, cards[4]).Should().BeEmpty();
        (await ReminderAsync(kept))!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Rollout_ActivatesPre069Reminders_WithoutReplayingThePast()
    {
        var (_, listId, _) = await SeedBoardAsync(cards: 3);
        var (_, user) = await SignInAsync();
        var cards = await CardIdsAsync(listId);
        var pastOnce = await SeedReminderAsync(cards[0], ReminderFrequency.OneTime, new DateTime(2027, 1, 5, 10, 0, 0), user.Id, legacy: true);
        var pastWeekly = await SeedReminderAsync(cards[1], ReminderFrequency.Weekly, new DateTime(2027, 1, 4, 10, 0, 0), user.Id, legacy: true);
        var future = await SeedReminderAsync(cards[2], ReminderFrequency.OneTime, new DateTime(2027, 2, 10, 10, 0, 0), user.Id, legacy: true);

        var (delivery, clock, sent, _) = Delivery(new DateTime(2027, 2, 1, 12, 0, 0)); // Monday
        await delivery.RunOnceAsync();
        cards.SelectMany(c => For(sent, c)).Should().BeEmpty("nothing old is replayed on activation");
        (await ReminderAsync(pastOnce))!.Should().Match<LogisticsCardReminder>(r => !r.IsActive && r.ActivatedAt != null && r.LastSentAt == null);
        (await ReminderAsync(pastWeekly))!.NextReminderDate.Should().Be(new DateTime(2027, 2, 8, 10, 0, 0), "the next Monday at 10:00");
        (await ReminderAsync(future))!.Should().Match<LogisticsCardReminder>(r => r.IsActive && r.NextReminderDate == new DateTime(2027, 2, 10, 10, 0, 0));

        (await delivery.ActivateLegacyAsync()).Activated.Should().Be(0, "idempotent");
        clock.Now = new DateTimeOffset(2027, 2, 10, 10, 0, 0, TimeSpan.Zero);
        await delivery.RunOnceAsync();
        For(sent, cards[2]).Should().ContainSingle("a pre-069 reminder still ahead is delivered when due");
    }

    // ---------------------------------------------------------------- helpers

    private async Task<int> SeedReminderAsync(int cardId, ReminderFrequency frequency, DateTime nextLocal, string targets, bool personal = false,
        string? creator = null, bool legacy = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reminder = LogisticsCardReminder.Create(cardId, frequency, targets, nextLocal);
        reminder.Personal = personal;
        reminder.CreatedByUserId = creator;
        reminder.ActivatedAt = legacy ? null : new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        db.LogisticsCardReminders.Add(reminder);
        await db.SaveChangesAsync();
        return reminder.Id;
    }

    private async Task<LogisticsCardReminder?> ReminderAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LogisticsCardReminders.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
    }

    private async Task SetReminderAsync(int id, Action<LogisticsCardReminder> change)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        change(await db.LogisticsCardReminders.SingleAsync(r => r.Id == id));
        await db.SaveChangesAsync();
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
