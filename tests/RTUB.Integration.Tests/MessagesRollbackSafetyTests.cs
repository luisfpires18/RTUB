using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Integration.Tests;

/// <summary>
/// Messaging v2 uses the messaging tables. Outside /api/conversations and /api/messages, nothing touches them: the
/// session call, a push that is not kept in /messages and a member deletion run without a single SQL statement naming
/// them (no unread badge on every page). An ordinary push is kept in "Notificações RTUB" on purpose
/// (NotificationInboxTests); the push here opts out, as a chat-message push does.
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
            var notification = new SendPushNotificationDto { Title = "Teste", Body = "Sem caixa de entrada", KeepInInbox = false };
            await push.SendToUserAsync(user.Id, notification);
            await push.BroadcastAsync(notification);
            await push.SendToSelectedUsersAsync([user.Id], notification);

            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            (await users.DeleteAsync((await users.FindByIdAsync(user.Id))!)).Succeeded.Should().BeTrue();
        }

        foreach (var table in MessageTables)
        {
            _factory.Sql.Count(table).Should().Be(0, "nothing outside /messages and the kept pushes may read or write {0}", table);
        }
    }
}
