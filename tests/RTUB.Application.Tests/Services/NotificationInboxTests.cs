using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Task 058: every push is kept in the recipients' "Notificações RTUB" (read, with its link, push configured or not), and
/// the premade /messages groups follow the members (<see cref="MessagingGroups"/>).
/// </summary>
public sealed class NotificationInboxTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);
    private readonly DatabaseFixture _db = new();

    /// <summary>The host's wiring: the push service with the inbox, here without VAPID keys (no push is sent at all).</summary>
    private PushNotificationService Push()
    {
        var factory = new Mock<IDbContextFactory<ApplicationDbContext>>();
        factory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _db.CreateContext());
        var inbox = new NotificationInbox(factory.Object, NullLogger<NotificationInbox>.Instance);
        return new PushNotificationService(Mock.Of<IPushSubscriptionRepository>(), Mock.Of<IUserProfileRepository>(),
            Options.Create(new WebPushOptions()), NullLogger<PushNotificationService>.Instance, (HttpClient?)null, inbox);
    }

    private async Task<string> MemberAsync(string nick, bool retired = false, bool expelled = false, params MemberCategory[] categories)
    {
        await using var db = _db.CreateContext();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(), UserName = nick.ToLowerInvariant(), Email = $"{nick.ToLowerInvariant()}@rtub.test", Nickname = nick, FirstName = nick, LastName = "Teste",
            IsRetired = retired, IsExpelled = expelled, Categories = categories.ToList(),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<List<Message>> InboxAsync(string userId)
    {
        await using var db = _db.CreateContext();
        return await db.Messages.Include(m => m.Conversation)
            .Where(m => m.Conversation!.IsSystemConversation && m.Conversation.Participants == userId)
            .OrderBy(m => m.Id).ToListAsync();
    }

    private static SendPushNotificationDto Notice(string title = "Novo evento", string url = "/events/7") =>
        new() { Title = title, Body = "Confirma a tua presença.", Url = url, Tag = "event-7" };

    // ---------- Notificações RTUB ----------

    [Fact]
    public async Task EveryPush_IsKept_ForEachRecipient_AsRead_WithItsLink_EvenWithoutPushConfigured()
    {
        var ana = await MemberAsync("Ana");
        var rui = await MemberAsync("Rui");

        await Push().SendToSelectedUsersAsync(new[] { ana, rui }, Notice());
        await Push().SendToUserAsync(ana, Notice("Lembrete: Serenata"));

        var kept = await InboxAsync(ana);
        kept.Select(m => m.Body).Should().Equal("Novo evento\n\nConfirma a tua presença.", "Lembrete: Serenata\n\nConfirma a tua presença.");
        kept.Should().AllSatisfy(m =>
        {
            m.IsSystem.Should().BeTrue();
            m.SenderId.Should().BeNull();
            m.Link.Should().Be("/events/7");
            m.IsReadBy(ana).Should().BeTrue("a kept push never moves the unread badge");
        });
        kept.Select(m => m.ConversationId).Distinct().Should().ContainSingle("one Notificações RTUB per member");
        (await InboxAsync(rui)).Should().ContainSingle();
    }

    [Fact]
    public async Task ABroadcast_ReachesEveryAccount_ButTheExpelled()
    {
        var ana = await MemberAsync("Ana");
        var reformado = await MemberAsync("Velho", retired: true);
        var expelled = await MemberAsync("Fora", expelled: true);

        await Push().BroadcastAsync(Notice("Novo 1º Lugar no Ranking!", "/leaderboard"));

        (await InboxAsync(ana)).Should().ContainSingle();
        (await InboxAsync(reformado)).Should().ContainSingle();
        (await InboxAsync(expelled)).Should().BeEmpty();
    }

    [Fact]
    public async Task AnExistingSystemConversation_IsReused_AndShownWithTheNewName()
    {
        var ana = await MemberAsync("Ana");
        await using (var db = _db.CreateContext())
        {
            db.Conversations.Add(new Conversation { Participants = ana, IsSystemConversation = true, Title = "Sistema RTUB", IsArchived = true });
            await db.SaveChangesAsync();
        }

        await Push().SendToUserAsync(ana, Notice());

        await using var check = _db.CreateContext();
        var inbox = await check.Conversations.SingleAsync(c => c.IsSystemConversation);
        inbox.IsArchived.Should().BeFalse("a new notification brings it back");
        (await InboxAsync(ana)).Should().ContainSingle().Which.ConversationId.Should().Be(inbox.Id);
        MessagingService.SystemTitle.Should().Be("Notificações RTUB");
    }

    [Fact]
    public async Task AChatMessagePush_IsNotCopied()
    {
        var ana = await MemberAsync("Ana");

        await Push().SendToSelectedUsersAsync(new[] { ana },
            new RTUB.Application.Factories.PushNotificationFactory().CreateConversationMessageNotification("Rui", null, 4));

        (await InboxAsync(ana)).Should().BeEmpty();
    }

    // ---------- premade groups ----------

    private async Task<Conversation> GroupAsync(string title, bool system = true, params string[] members)
    {
        await using var db = _db.CreateContext();
        var group = new Conversation { Title = title, IsGroup = true, CreatedByUserId = system ? MessagingGroups.SystemCreator : "someone" };
        group.SetParticipants(members);
        db.Conversations.Add(group);
        await db.SaveChangesAsync();
        return group;
    }

    private async Task<Dictionary<string, Conversation>> SyncAsync()
    {
        await using (var db = _db.CreateContext())
        {
            await MessagingGroups.SyncAsync(db, Now);
        }

        await using var read = _db.CreateContext();
        return (await read.Conversations.Where(c => c.IsGroup).ToListAsync()).ToDictionary(c => $"{c.Title}#{c.Id}");
    }

    private static Conversation Live(Dictionary<string, Conversation> groups, string title) =>
        groups.Values.Single(g => g.Title == title && !g.IsArchived);

    [Fact]
    public async Task ManagedGroups_FollowTheMembers_AndExpelledAreInNone()
    {
        var tuno = await MemberAsync("Tuno", categories: [MemberCategory.Tuno]);
        var honorario = await MemberAsync("Honorario", categories: [MemberCategory.Tuno, MemberCategory.TunoHonorario]);
        var caloiro = await MemberAsync("Caloiro", categories: [MemberCategory.Caloiro]);
        var leitao = await MemberAsync("Leitao", categories: [MemberCategory.Leitao]);
        var reformado = await MemberAsync("Reformado", retired: true, categories: [MemberCategory.Tuno]);
        var expelled = await MemberAsync("Expulso", expelled: true, categories: [MemberCategory.Tuno]);
        await GroupAsync("GERAL", members: [expelled]);
        await GroupAsync("TUNOS", members: [tuno, caloiro]);

        var groups = await SyncAsync();

        Live(groups, "GERAL").GetParticipantIds().Should().BeEquivalentTo([tuno, honorario, caloiro, leitao, reformado]);
        Live(groups, "NO ATIVO").GetParticipantIds().Should().BeEquivalentTo([tuno, honorario, caloiro, leitao]);
        Live(groups, "TUNOS").GetParticipantIds().Should().BeEquivalentTo([tuno, reformado]);
        Live(groups, "LEITÕES & CALOIROS").GetParticipantIds().Should().BeEquivalentTo([caloiro, leitao]);
        groups.Values.Should().NotContain(g => g.GetParticipantIds().Contains(expelled));
    }

    [Fact]
    public async Task OtherPremadeGroups_AreArchived_MembersOwnGroupsAreUntouched_AndASecondRunChangesNothing()
    {
        var tuno = await MemberAsync("Tuno", categories: [MemberCategory.Tuno]);
        foreach (var title in new[] { "VETERANOS", "TUNOSSAUROS", "ANUNCIOS", "ORGANIZAÇÃO (2025/2026)", "Orgãos Sociais 2025/2026" })
        {
            await GroupAsync(title, members: [tuno]);
        }

        var mine = await GroupAsync("Malucos", system: false, members: [tuno]);
        var duplicate = await GroupAsync("GERAL", members: [tuno]);
        await GroupAsync("geral", members: [tuno]);

        var groups = await SyncAsync();

        groups.Values.Where(g => !g.IsArchived).Select(g => g.Title)
            .Should().BeEquivalentTo(["GERAL", "NO ATIVO", "TUNOS", "LEITÕES & CALOIROS", "Malucos"]);
        Live(groups, "GERAL").Id.Should().Be(duplicate.Id, "the oldest group of a title is the one kept");
        groups[$"Malucos#{mine.Id}"].IsArchived.Should().BeFalse();

        await using var db = _db.CreateContext();
        (await MessagingGroups.SyncAsync(db, Now)).Should().Be(0);
    }

    public void Dispose() => _db.Dispose();
}
