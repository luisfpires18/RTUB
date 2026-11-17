// LogisticsApiTests tests: reminder delivery by the clock.
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;
public partial class LogisticsApiTests
{
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

    private async Task SetReminderAsync(int id, Action<LogisticsCardReminder> change)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        change(await db.LogisticsCardReminders.SingleAsync(r => r.Id == id));
        await db.SaveChangesAsync();
    }
}
