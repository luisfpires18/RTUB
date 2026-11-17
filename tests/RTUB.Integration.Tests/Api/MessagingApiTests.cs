using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// Messaging v2 on /api/conversations and /api/messages (task 041) through the real host: real login, antiforgery,
/// SQLite and the preserved v1 tables. Push is the recording fake of <see cref="EventsApiFactory"/>: nothing is sent,
/// but every call is seen. Every test makes its own members and conversations.
/// </summary>
public partial class MessagingApiTests : IClassFixture<EventsApiFactory>
{
    private const string Secret = "segredo-da-tuna-41";
    private readonly EventsApiFactory _factory;

    public MessagingApiTests(EventsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Visitors_GetNothing()
    {
        var anonymous = Anonymous();
        await WithTokenAsync(anonymous);
        foreach (var path in new[] { "/api/conversations", "/api/conversations/unread", "/api/conversations/recipients", "/api/conversations/1" })
        {
            (await anonymous.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, path);
        }

        (await anonymous.PostAsJsonAsync("/api/conversations", new { userIds = new[] { "x" }, body = "olá" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync("/api/conversations/1/messages", new { body = "olá" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsync("/api/conversations/1/read", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsync("/api/conversations/1/mute", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync("/api/messages/1/reactions", new { emoji = "👍" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.DeleteAsync("/api/messages/1/reactions")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Writes_NeedTheAntiforgeryHeader()
    {
        var (a, _) = await SignInAsync(token: false);
        var (_, b) = await SignInAsync();

        (await a.PostAsJsonAsync("/api/conversations", new { userIds = new[] { b.Id }, body = "olá" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await a.PostAsJsonAsync("/api/conversations/1/messages", new { body = "olá" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await a.PostAsJsonAsync("/api/messages/1/reactions", new { emoji = "👍" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await a.DeleteAsync("/api/messages/1/reactions")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------- helpers ----------

    // A conversation starts with its first message (nothing is stored before it).
    private async Task<int> StartAsync(HttpClient client, string userId, string body = "Olá") =>
        (await Json(await client.PostAsJsonAsync("/api/conversations", new { userIds = new[] { userId }, body })))
            .GetProperty("conversation").GetProperty("id").GetInt32();

    private async Task<int> SeedAsync(Conversation conversation, params Message[] messages)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        conversation.LastMessageAt = DateTime.UtcNow;
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync();
        foreach (var message in messages)
        {
            message.ConversationId = conversation.Id;
            db.Messages.Add(message);
            await db.SaveChangesAsync(); // one at a time, so ids follow the order
        }

        return conversation.Id;
    }

    private async Task GiveCurrentPositionAsync(string userId, Position position)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;
        var start = now.Month >= 9 ? now.Year : now.Year - 1;
        db.RoleAssignments.Add(RoleAssignment.Create(userId, position, start, start + 1));
        await db.SaveChangesAsync();
    }

    private List<(IReadOnlyList<string> Recipients, SendPushNotificationDto Notification)> Pushes(int conversationId) =>
        _factory.Push.Invocations
            .Where(i => i.Method.Name == "SendToSelectedUsersAsync")
            .Select(i => (Recipients: (IReadOnlyList<string>)((IEnumerable<string>)i.Arguments[0]).ToList(), Notification: (SendPushNotificationDto)i.Arguments[1]))
            .Where(p => p.Notification.Tag == $"message-{conversationId}")
            .ToList();

    private static JsonElement Row(JsonElement list, int id) =>
        list.EnumerateArray().Single(c => c.GetProperty("id").GetInt32() == id);

    private static IEnumerable<int> Ids(JsonElement list) => list.EnumerateArray().Select(c => c.GetProperty("id").GetInt32());

    private static List<JsonElement> Messages(JsonElement detail) => detail.GetProperty("messages").EnumerateArray().ToList();

    private static List<(string, int, bool)> Reactions(JsonElement message) =>
        message.GetProperty("reactions").EnumerateArray()
            .Select(r => (r.GetProperty("emoji").GetString()!, r.GetProperty("count").GetInt32(), r.GetProperty("mine").GetBoolean()))
            .ToList();

    private static async Task<JsonElement> Json(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.StatusCode.Should().Be(HttpStatusCode.OK, path);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<Dictionary<string, string[]>> Errors(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("errors").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.EnumerateArray().Select(v => v.GetString()!).ToArray());
    }

    private static int _ip;

    private async Task<(HttpClient Client, ApplicationUser User)> SignInAsync(string? role = null, bool token = true)
    {
        var n = Interlocked.Increment(ref _ip);
        var session = await CookieTestSession.SignInAsync(_factory, $"msg{Guid.NewGuid():N}"[..20], $"10.47.{n / 250}.{n % 250 + 1}", role);
        if (token)
        {
            await WithTokenAsync(session.Client);
        }

        return session;
    }

    private HttpClient Anonymous()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, "10.48.0.1");
        return client;
    }

    private static async Task WithTokenAsync(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/public/antiforgery-token");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }
}
