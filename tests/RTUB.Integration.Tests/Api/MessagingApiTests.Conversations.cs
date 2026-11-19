// MessagingApiTests: Conversations tests.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;

namespace RTUB.Integration.Tests.Api;

public partial class MessagingApiTests
{
    [Fact]
    public async Task StartingAndSending_ReachOnlyTheTwoMembers_AndTheListShowsUnreadUntilRead()
    {
        var (a, aUser) = await SignInAsync();
        var (b, bUser) = await SignInAsync();
        var (stranger, _) = await SignInAsync();

        var started = await AreaHttp.Json(await a.PostAsJsonAsync("/api/conversations", new { userIds = new[] { bUser.Id }, body = "  Olá, tudo bem?  " }));
        var id = started.GetProperty("conversation").GetProperty("id").GetInt32();
        var sent = Messages(started).Single();
        sent.GetProperty("body").GetString().Should().Be("Olá, tudo bem?", "the first message is sent with the start, trimmed");
        sent.GetProperty("isMine").GetBoolean().Should().BeTrue();

        var row = Row(await AreaHttp.Json(b, "/api/conversations"), id);
        row.GetProperty("unreadCount").GetInt32().Should().Be(1);
        row.GetProperty("lastMessagePreview").GetString().Should().Be("Olá, tudo bem?");
        row.GetProperty("title").GetString().Should().Be(aUser.Nickname);
        row.GetProperty("canSend").GetBoolean().Should().BeTrue();
        Row(await AreaHttp.Json(a, "/api/conversations"), id).GetProperty("unreadCount").GetInt32().Should().Be(0, "my own message is never unread");

        (await b.PostAsync($"/api/conversations/{id}/read", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        Row(await AreaHttp.Json(b, "/api/conversations"), id).GetProperty("unreadCount").GetInt32().Should().Be(0);
        Messages(await AreaHttp.Json(a, $"/api/conversations/{id}")).Single().GetProperty("seen").GetBoolean().Should().BeTrue();

        // Somebody else's conversation does not exist for a stranger - not even for reading.
        Ids(await AreaHttp.Json(stranger, "/api/conversations")).Should().NotContain(id);
        (await stranger.GetAsync($"/api/conversations/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await stranger.PostAsJsonAsync($"/api/conversations/{id}/messages", new { body = "intruso" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await stranger.PostAsync($"/api/conversations/{id}/read", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await stranger.PostAsync($"/api/conversations/{id}/mute", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await StartAsync(b, aUser.Id)).Should().Be(id, "one one-to-one conversation per pair, whoever starts it");
    }

    [Fact]
    public async Task TheHeaderBadge_CountsUnreadMessages_ForTheRecipientOnly_AndAgreesWithTheList()
    {
        var (a, aUser) = await SignInAsync();
        var (b, bUser) = await SignInAsync();
        var (c, _) = await SignInAsync();

        async Task<int> Unread(HttpClient client) => (await AreaHttp.Json(client, "/api/conversations/unread")).GetProperty("unreadCount").GetInt32();

        (await Unread(b)).Should().Be(0);
        var id = await StartAsync(a, bUser.Id, "um");
        await a.PostAsJsonAsync($"/api/conversations/{id}/messages", new { body = "dois" });

        (await Unread(b)).Should().Be(2, "messages, not conversations");
        (await Unread(a)).Should().Be(0, "my own messages never count");
        (await Unread(c)).Should().Be(0, "a stranger sees nothing");
        Row(await AreaHttp.Json(b, "/api/conversations"), id).GetProperty("unreadCount").GetInt32().Should().Be(await Unread(b));

        await b.PostAsJsonAsync($"/api/conversations/{id}/messages", new { body = "resposta" });
        (await Unread(b)).Should().Be(2, "sending does not change my own count");
        (await Unread(a)).Should().Be(1);

        await b.PostAsync($"/api/conversations/{id}/read", null);
        (await Unread(b)).Should().Be(0);
    }

    [Fact]
    public async Task AdminAndOwner_CannotReadOtherPeoplesConversations()
    {
        var (a, _) = await SignInAsync();
        var (_, bUser) = await SignInAsync();
        var id = await StartAsync(a, bUser.Id);
        (await a.PostAsJsonAsync($"/api/conversations/{id}/messages", new { body = Secret })).EnsureSuccessStatusCode();

        foreach (var role in new[] { "Admin", "Owner" })
        {
            var (boss, _) = await SignInAsync(role);
            Ids(await AreaHttp.Json(boss, "/api/conversations")).Should().NotContain(id, role);
            var response = await boss.GetAsync($"/api/conversations/{id}");
            response.StatusCode.Should().Be(HttpStatusCode.NotFound, role);
            (await response.Content.ReadAsStringAsync()).Should().NotContain(Secret);
        }
    }

    [Fact]
    public async Task EmptyTooLongAndBadTargets_AreRefused()
    {
        var (a, aUser) = await SignInAsync();
        var (_, bUser) = await SignInAsync();
        var id = await StartAsync(a, bUser.Id);

        (await AreaHttp.Errors(await a.PostAsJsonAsync($"/api/conversations/{id}/messages", new { body = "   \n\t " }))).Should().ContainKey("body");
        (await AreaHttp.Errors(await a.PostAsJsonAsync($"/api/conversations/{id}/messages", new { body = (string?)null }))).Should().ContainKey("body");
        (await AreaHttp.Errors(await a.PostAsJsonAsync($"/api/conversations/{id}/messages", new { body = new string('x', 2001) }))).Should().ContainKey("body");
        (await a.PostAsJsonAsync($"/api/conversations/{id}/messages", new { body = new string('x', 2000) })).StatusCode.Should().Be(HttpStatusCode.OK);

        (await AreaHttp.Errors(await a.PostAsJsonAsync("/api/conversations", new { userIds = new[] { aUser.Id }, body = "olá" }))).Should().ContainKey("userIds", "not with yourself");
        (await AreaHttp.Errors(await a.PostAsJsonAsync("/api/conversations", new { userIds = new[] { "nobody" }, body = "olá" }))).Should().ContainKey("userIds");
        (await a.GetAsync("/api/conversations/999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NothingIsStored_UntilTheFirstMessage_AndAnExistingChatIsFoundInstead()
    {
        var (a, aUser) = await SignInAsync();
        var (b, bUser) = await SignInAsync();

        (await AreaHttp.Json(a, $"/api/conversations/direct/{bUser.Id}")).GetProperty("id").ValueKind.Should().Be(JsonValueKind.Null);
        (await AreaHttp.Errors(await a.PostAsJsonAsync("/api/conversations", new { userIds = new[] { bUser.Id } }))).Should().ContainKey("body");
        (await AreaHttp.Errors(await a.PostAsJsonAsync("/api/conversations", new { userIds = new[] { bUser.Id }, body = "  " }))).Should().ContainKey("body");
        (await a.PostAsJsonAsync("/api/conversations", new { userId = bUser.Id })).StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "the old pick-a-member call no longer creates an empty conversation");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Conversations.AsNoTracking().ToListAsync()).Should().NotContain(c => c.HasParticipant(aUser.Id) && c.HasParticipant(bUser.Id));
        }

        var id = await StartAsync(a, bUser.Id);
        (await AreaHttp.Json(a, $"/api/conversations/direct/{bUser.Id}")).GetProperty("id").GetInt32().Should().Be(id);
        (await AreaHttp.Json(b, $"/api/conversations/direct/{aUser.Id}")).GetProperty("id").GetInt32().Should().Be(id);
        Messages(await AreaHttp.Json(b, $"/api/conversations/{id}")).Should().ContainSingle();
    }
}
