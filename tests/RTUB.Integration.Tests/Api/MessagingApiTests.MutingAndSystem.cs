// MessagingApiTests: MutingAndSystem tests.
using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;

public partial class MessagingApiTests
{
    [Fact]
    public async Task Muting_StopsPushForThatMember_AndPushNeverCarriesTheText()
    {
        var (a, aUser) = await SignInAsync();
        var (b, bUser) = await SignInAsync();
        var id = await StartAsync(a, bUser.Id, Secret);

        var pushes = Pushes(id);
        pushes.Should().ContainSingle();
        pushes[0].Recipients.Should().Equal(bUser.Id);
        pushes[0].Notification.Title.Should().Be("Nova mensagem");
        pushes[0].Notification.Url.Should().Be($"/messages?c={id}");
        (pushes[0].Notification.Title + pushes[0].Notification.Body).Should().NotContain(Secret).And.Contain(aUser.Nickname);

        (await AreaHttp.Json(await b.PostAsync($"/api/conversations/{id}/mute", null))).GetProperty("muted").GetBoolean().Should().BeTrue();
        Row(await AreaHttp.Json(b, "/api/conversations"), id).GetProperty("muted").GetBoolean().Should().BeTrue();
        Row(await AreaHttp.Json(a, "/api/conversations"), id).GetProperty("muted").GetBoolean().Should().BeFalse("muting is per member");
        (await a.PostAsJsonAsync($"/api/conversations/{id}/messages", new { body = "outra" })).EnsureSuccessStatusCode();
        Pushes(id).Should().HaveCount(1, "a muted member gets no push");
        Row(await AreaHttp.Json(b, "/api/conversations"), id).GetProperty("unreadCount").GetInt32().Should().Be(2, "muted, still unread");

        (await AreaHttp.Json(await b.PostAsync($"/api/conversations/{id}/unmute", null))).GetProperty("muted").GetBoolean().Should().BeFalse();
        (await a.PostAsJsonAsync($"/api/conversations/{id}/messages", new { body = "terceira" })).EnsureSuccessStatusCode();
        Pushes(id).Should().HaveCount(2);
    }

    [Fact]
    public async Task SystemConversations_AreReadOnly_AndAnnouncementChannels_TakeOnlyTheirSenders()
    {
        var (a, aUser) = await SignInAsync();
        var (b, bUser) = await SignInAsync();
        var (owner, ownerUser) = await SignInAsync("Owner");

        var system = await SeedAsync(new Conversation { Participants = aUser.Id, IsSystemConversation = true, Title = "Sistema RTUB" },
            new Message { Body = "Novo ensaio marcado", IsSystem = true, Link = "https://rtub.example/rehearsals/3" });
        var detail = await AreaHttp.Json(a, $"/api/conversations/{system}");
        detail.GetProperty("conversation").GetProperty("canSend").GetBoolean().Should().BeFalse();
        detail.GetProperty("conversation").GetProperty("kind").GetString().Should().Be("system");
        Messages(detail).Single().GetProperty("link").GetString().Should().Be("/rehearsals/3", "only this site's path is kept");
        (await a.PostAsJsonAsync($"/api/conversations/{system}/messages", new { body = "olá" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var systemMessage = Messages(detail).Single().GetProperty("id").GetInt32();
        (await a.PostAsJsonAsync($"/api/messages/{systemMessage}/reactions", new { emoji = "👍" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var channel = await SeedAsync(new Conversation
        {
            Participants = $"{aUser.Id};{bUser.Id};{ownerUser.Id}", IsGroup = true, IsAnnouncementOnly = true, Title = "ANÚNCIOS",
            CreatedByUserId = "system",
        });
        Row(await AreaHttp.Json(b, "/api/conversations"), channel).GetProperty("canSend").GetBoolean().Should().BeFalse();
        (await b.PostAsJsonAsync($"/api/conversations/{channel}/messages", new { body = "posso?" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await owner.PostAsJsonAsync($"/api/conversations/{channel}/messages", new { body = "Aviso do Owner" })).StatusCode.Should().Be(HttpStatusCode.OK);

        await GiveCurrentPositionAsync(bUser.Id, Position.Magister);
        (await b.PostAsJsonAsync($"/api/conversations/{channel}/messages", new { body = "Aviso do Magister" })).StatusCode.Should().Be(HttpStatusCode.OK);
        var names = Messages(await AreaHttp.Json(a, $"/api/conversations/{channel}")).Select(m => m.GetProperty("sender").GetProperty("id").GetString());
        names.Should().Equal(ownerUser.Id, bUser.Id);
    }

    [Fact]
    public async Task Groups_ShowWhoWrote_ArchivedOnesStayHidden_AndAChatWithARemovedMemberIsClosed()
    {
        var (a, aUser) = await SignInAsync();
        var (b, bUser) = await SignInAsync();
        var (outsider, _) = await SignInAsync();

        var group = await SeedAsync(new Conversation { Participants = $"{aUser.Id};{bUser.Id}", IsGroup = true, Title = "NAIPE" });
        (await b.PostAsJsonAsync($"/api/conversations/{group}/messages", new { body = "Olá naipe" })).EnsureSuccessStatusCode();
        var detail = await AreaHttp.Json(a, $"/api/conversations/{group}");
        detail.GetProperty("conversation").GetProperty("title").GetString().Should().Be("NAIPE");
        detail.GetProperty("participants").EnumerateArray().Select(p => p.GetProperty("id").GetString()).Should().Equal(bUser.Id);
        Messages(detail).Single().GetProperty("sender").GetProperty("displayName").GetString().Should().Be(bUser.Nickname);
        (await outsider.GetAsync($"/api/conversations/{group}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var archived = await SeedAsync(new Conversation { Participants = $"{aUser.Id};{bUser.Id}", IsGroup = true, Title = "VELHO", IsArchived = true });
        Ids(await AreaHttp.Json(a, "/api/conversations")).Should().NotContain(archived);
        (await a.GetAsync($"/api/conversations/{archived}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var (_, goneUser) = await SignInAsync();
        var withGone = await StartAsync(a, goneUser.Id);
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var gone = await users.FindByIdAsync(goneUser.Id);
            gone!.IsExpelled = true;
            (await users.UpdateAsync(gone)).Succeeded.Should().BeTrue();
        }

        Row(await AreaHttp.Json(a, "/api/conversations"), withGone).GetProperty("canSend").GetBoolean().Should().BeFalse();
        (await a.PostAsJsonAsync($"/api/conversations/{withGone}/messages", new { body = "olá?" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await AreaHttp.Json(a, "/api/conversations/recipients")).EnumerateArray().Select(p => p.GetProperty("id").GetString())
            .Should().NotContain(goneUser.Id).And.NotContain(aUser.Id).And.Contain(bUser.Id);
    }

    [Fact]
    public async Task OlderMessages_ComeInPages()
    {
        var (a, aUser) = await SignInAsync();
        var (_, bUser) = await SignInAsync();
        var conversation = new Conversation { Participants = $"{aUser.Id};{bUser.Id}" };
        var id = await SeedAsync(conversation, Enumerable.Range(1, 60).Select(i => new Message { SenderId = bUser.Id, Body = $"m{i}" }).ToArray());

        var first = await AreaHttp.Json(a, $"/api/conversations/{id}");
        first.GetProperty("hasMore").GetBoolean().Should().BeTrue();
        var page = Messages(first).Select(m => m.GetProperty("body").GetString()).ToList();
        page.Should().HaveCount(50).And.StartWith("m11").And.EndWith("m60");

        var before = Messages(first).First().GetProperty("id").GetInt32();
        var older = await AreaHttp.Json(a, $"/api/conversations/{id}?before={before}");
        older.GetProperty("hasMore").GetBoolean().Should().BeFalse();
        Messages(older).Select(m => m.GetProperty("body").GetString()).Should().Equal(Enumerable.Range(1, 10).Select(i => $"m{i}"));
    }

    [Fact]
    public async Task Tracing_NeverRecordsMessagingContent()
    {
        var (a, _) = await SignInAsync();
        var (b, bUser) = await SignInAsync();
        var id = await StartAsync(a, bUser.Id);
        var messageId = (await AreaHttp.Json(await a.PostAsJsonAsync($"/api/conversations/{id}/messages", new { body = Secret }))).GetProperty("id").GetInt32();
        await AreaHttp.Json(await b.PostAsJsonAsync($"/api/conversations/{id}/messages", new { body = Secret + "-reply", replyToMessageId = messageId }));
        await AreaHttp.Json(await b.PostAsJsonAsync($"/api/messages/{messageId}/reactions", new { emoji = "😂" }));
        await AreaHttp.Json(await b.PostAsync($"/api/conversations/{id}/mute", null));
        (await b.PostAsync($"/api/conversations/{id}/read", null)).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logs = await db.AuditLogs.AsNoTracking().ToListAsync();
        logs.Select(l => l.EntityType).Should().NotContain(new[] { "Message", "Conversation", "MessageReaction", "ConversationUserSettings" });
        logs.Should().NotContain(l => (l.Changes ?? "").Contains(Secret) || (l.EntityDisplayName ?? "").Contains(Secret));
    }
}
