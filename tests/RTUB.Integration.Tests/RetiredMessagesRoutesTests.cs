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
/// Task 027 (Phase A) retired Messages / Conversas with no replacement: the inbox pages and the SignalR hub are gone,
/// so every old URL is a plain 404 (no redirect) for visitors and members alike, and neither the Blazor layout nor the
/// PWA manifest offers them any more. The four message tables stay until the later contract task (N-1 rule).
/// </summary>
public class RetiredMessagesRoutesTests : IntegrationTestBase
{
    private const string Negotiate = "/hubs/messages/negotiate?negotiateVersion=1";

    public RetiredMessagesRoutesTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Theory]
    [InlineData("/messages")]
    [InlineData("/messages/1")]
    public async Task RetiredPage_IsNotFound_ForVisitors(string url)
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        (await client.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.NotFound, "{0} was retired in 027", url);
    }

    [Fact]
    public async Task TheMessagesHub_IsNotFound_ForVisitors()
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        (await client.PostAsync(Negotiate, content: null)).StatusCode.Should().Be(HttpStatusCode.NotFound,
            "the hub is no longer mapped (it used to answer 401 to a visitor)");
    }

    [Fact]
    public async Task RetiredUrls_AreNotFound_ForAMember_AndTheLayoutNoLongerOffersMessages()
    {
        var (member, _) = await CookieTestSession.SignInAsync(Factory, "retired-messages-member", "10.94.0.2");

        (await member.GetAsync("/messages")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await member.GetAsync("/messages/1")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await member.PostAsync(Negotiate, content: null)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var page = await member.GetAsync("/naipes");
        page.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await page.Content.ReadAsStringAsync();
        html.Should().Contain("href=\"/leaderboard\"", "the signed-in nav is rendered, so the absences below mean something");
        html.Should().NotContain("href=\"/messages\"").And.NotContain("navbar-message-badge")
            .And.NotContain("unreadMessages.js").And.NotContain("messageComposer.js").And.NotContain("messageScroller.js")
            .And.NotContain(">Mensagens<");
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
/// Rollback safety for the contract task that will drop the message tables: a 027 build is the rollback target of that
/// release, so it must run against a database without them. With the four tables dropped, a signed-in page, every push
/// fan-out and a member deletion run without a single SQL statement naming them. (Before 027 every push wrote an inbox
/// copy, and the layout's unread badge queried <c>Messages</c> on every Blazor page.)
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
            (await member.GetAsync("/naipes")).StatusCode.Should().Be(HttpStatusCode.OK);

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
