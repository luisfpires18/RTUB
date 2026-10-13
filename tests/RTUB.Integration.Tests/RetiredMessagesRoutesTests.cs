using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using Xunit;

namespace RTUB.Integration.Tests;

/// <summary>
/// Task 027 retired the old Messages / Conversas inbox; task 041 brought Messaging back as v2 on the same, preserved
/// tables. What 027 removed stays removed: <c>/messages/{id}</c> and the SignalR hub are plain 404s, the old inbox
/// scripts never come back, and the PWA manifest has no shortcut. <c>/messages</c> itself is the React v2 page now:
/// a visitor goes to sign in, a member gets the React shell (never an old Blazor inbox).
/// </summary>
public class RetiredMessagesRoutesTests : IntegrationTestBase
{
    private const string Negotiate = "/hubs/messages/negotiate?negotiateVersion=1";

    public RetiredMessagesRoutesTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task OldInboxUrls_StayNotFound_AndMessagesSendsVisitorsToSignIn()
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        (await client.GetAsync("/messages/1")).StatusCode.Should().Be(HttpStatusCode.NotFound, "the 027 inbox URL stays retired");
        var page = await client.GetAsync("/messages");
        page.StatusCode.Should().Be(HttpStatusCode.Redirect, "Messaging v2 is for members");
        page.Headers.Location!.OriginalString.Should().Be("/login?returnUrl=%2Fmessages");
    }

    [Fact]
    public async Task TheMessagesHub_IsNotFound_ForVisitors()
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        (await client.PostAsync(Negotiate, content: null)).StatusCode.Should().Be(HttpStatusCode.NotFound,
            "the hub is no longer mapped (it used to answer 401 to a visitor)");
    }

    [Fact]
    public async Task AMember_GetsTheReactV2Page_AndNothingOfTheOldInbox()
    {
        var (member, _) = await CookieTestSession.SignInAsync(Factory, "retired-messages-member", "10.94.0.2");

        (await member.GetAsync("/messages/1")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await member.PostAsync(Negotiate, content: null)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var page = await member.GetAsync("/messages");
        page.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await page.Content.ReadAsStringAsync();
        html.Should().Contain("id=\"root\"").And.NotContain("blazor.web.js").And.NotContain("_blazor");
        html.Should().NotContain("navbar-message-badge")
            .And.NotContain("unreadMessages.js").And.NotContain("messageComposer.js").And.NotContain("messageScroller.js");
    }

    [Fact]
    public async Task PwaManifest_HasNoMessagesShortcut()
    {
        using var manifest = JsonDocument.Parse(await Factory.CreateClient().GetStringAsync("/manifest.webmanifest"));

        var urls = manifest.RootElement.GetProperty("shortcuts").EnumerateArray()
            .Select(s => s.GetProperty("url").GetString())
            .ToList();
        urls.Should().NotBeEmpty().And.NotContain("/messages");
    }
}

/// <summary>
/// Written for a contract task that would have dropped the message tables (cancelled: they are preserved and Messaging v2
/// uses them since 041). Kept because it still proves something useful: outside /api/conversations and /api/messages,
/// nothing touches the messaging tables - the session call, every push fan-out and a member deletion run without a single
/// SQL statement naming them (no inbox copy per push since 027, no unread badge on every page).
/// </summary>
public class MessagesRollbackSafetyTests : IClassFixture<CookieValidationFactory>
{
    private static readonly string[] MessageTables =
        ["\"Conversations\"", "\"Messages\"", "\"MessageReactions\"", "\"ConversationUserSettings\""];

    private readonly CookieValidationFactory _factory;

    public MessagesRollbackSafetyTests(CookieValidationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task WithTheMessageTablesDropped_PagesPushAndMemberDeletion_NeverTouchThem()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.ExecuteSqlRawAsync(
                "DROP TABLE \"MessageReactions\"; DROP TABLE \"Messages\"; " +
                "DROP TABLE \"ConversationUserSettings\"; DROP TABLE \"Conversations\";");
        }

        var (member, user) = await CookieTestSession.SignInAsync(_factory, "rollback-messages", "10.94.0.1");

        using (_factory.Sql.Recording())
        {
            (await member.GetAsync("/api/account/me")).StatusCode.Should().Be(HttpStatusCode.OK);

            using var scope = _factory.Services.CreateScope();
            var push = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
            var notification = new SendPushNotificationDto { Title = "Teste", Body = "Sem caixa de entrada" };
            await push.SendToUserAsync(user.Id, notification);
            await push.BroadcastAsync(notification);
            await push.SendToSelectedUsersAsync([user.Id], notification);

            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            (await users.DeleteAsync((await users.FindByIdAsync(user.Id))!)).Succeeded.Should().BeTrue();
        }

        foreach (var table in MessageTables)
        {
            _factory.Sql.Count(table).Should().Be(0, "nothing in a 027 build may read or write {0}", table);
        }
    }
}
