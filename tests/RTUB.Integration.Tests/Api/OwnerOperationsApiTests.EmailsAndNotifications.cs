// OwnerOperationsApiTests: EmailsAndNotifications tests.
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Helpers;
using Xunit;

namespace RTUB.Integration.Tests.Api;

public partial class OwnerOperationsApiTests
{
    // ---------- /emails ----------

    [Fact]
    public async Task Emails_ListsConfirmedAddresses_BySubscription_AndTheOwnerTogglesThem()
    {
        var owner = await SignInAsync("Owner");
        var subscribed = await SeedUserAsync(subscribed: true);
        var unsubscribed = await SeedUserAsync(subscribed: false);
        var unconfirmed = await SeedUserAsync(subscribed: true, confirmed: false);

        var list = await AreaHttp.Json(owner, "/api/owner/emails");
        Ids(list.GetProperty("subscribed")).Should().Contain(subscribed.Id).And.NotContain(unsubscribed.Id).And.NotContain(unconfirmed.Id);
        Ids(list.GetProperty("notSubscribed")).Should().Contain(unsubscribed.Id).And.NotContain(unconfirmed.Id);

        var removed = await AreaHttp.Json(await owner.PutAsJsonAsync("/api/owner/emails/recipients", new { userIds = new[] { subscribed.Id }, subscribed = false }));
        removed.GetProperty("changed").GetInt32().Should().Be(1);
        Ids(removed.GetProperty("recipients").GetProperty("notSubscribed")).Should().Contain(subscribed.Id);
        (await StoredUserAsync(subscribed.Id)).Subscribed.Should().BeFalse();

        var added = await AreaHttp.Json(await owner.PutAsJsonAsync("/api/owner/emails/recipients",
            new { userIds = new[] { subscribed.Id, unsubscribed.Id }, subscribed = true }));
        added.GetProperty("changed").GetInt32().Should().Be(2);
        (await StoredUserAsync(subscribed.Id)).Subscribed.Should().BeTrue();
        (await StoredUserAsync(unsubscribed.Id)).Subscribed.Should().BeTrue();

        (await AreaHttp.Errors(await owner.PutAsJsonAsync("/api/owner/emails/recipients", new { userIds = Array.Empty<string>(), subscribed = true })))
            .Keys.Should().Equal("userIds");
    }

    [Fact]
    public async Task Emails_ValidatesTheForm_AndSendsToTheSubscribedConfirmedAddressesOnly()
    {
        var owner = await SignInAsync("Owner");
        var subscribed = await SeedUserAsync(subscribed: true);
        var unsubscribed = await SeedUserAsync(subscribed: false);
        var tag = Tag();

        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/owner/emails", new { title = " ", content = "" }))).Keys.Should().BeEquivalentTo("title", "content");
        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/owner/emails", new { title = new string('t', 201), content = "x" }))).Keys.Should().Equal("title");
        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/owner/emails", new { title = "x", content = new string('c', 5001) }))).Keys.Should().Equal("content");

        var sent = await AreaHttp.Json(await owner.PostAsJsonAsync("/api/owner/emails", new { title = $"Anúncio {tag}", content = "Ensaio geral amanhã." }));
        sent.GetProperty("success").GetBoolean().Should().BeTrue();

        _factory.Email.Verify(e => e.SendAnnouncementEmailAsync($"Anúncio {tag}", "Ensaio geral amanhã.",
            It.Is<List<string>>(to => to.Contains(subscribed.Email!) && !to.Contains(unsubscribed.Email!) && to.Count == to.Distinct().Count()),
            It.Is<Dictionary<string, (string, string)>>(d => d.ContainsKey(subscribed.Email!)),
            It.IsAny<IProgress<EmailSendProgress>?>()), Times.Once);
    }

    [Fact]
    public async Task Emails_ReportsTheSendersRefusal()
    {
        var owner = await SignInAsync("Owner");
        await SeedUserAsync(subscribed: true);
        var tag = Tag();
        _factory.Email.Setup(e => e.SendAnnouncementEmailAsync($"Recusado {tag}", It.IsAny<string>(), It.IsAny<List<string>>(),
                It.IsAny<Dictionary<string, (string, string)>>(), It.IsAny<IProgress<EmailSendProgress>?>()))
            .ReturnsAsync((false, 0, "Email de anúncio já enviado recentemente."));

        var result = await AreaHttp.Json(await owner.PostAsJsonAsync("/api/owner/emails", new { title = $"Recusado {tag}", content = "x" }));
        result.GetProperty("success").GetBoolean().Should().BeFalse();
        result.GetProperty("message").GetString().Should().Be("Email de anúncio já enviado recentemente.");
    }

    // ---------- /notifications ----------

    [Fact]
    public async Task Notifications_ListsThePushSubscribers_AndSendsToTheChosenOnes_Audited()
    {
        var owner = await SignInAsync("Owner");
        var withPush = await SeedUserAsync(subscribed: true);
        var withoutPush = await SeedUserAsync(subscribed: true);
        _factory.PushSubscribed.Add(withPush.Id);
        var tag = Tag();

        var list = await AreaHttp.Json(owner, "/api/owner/notifications");
        Ids(list.GetProperty("recipients")).Should().Contain(withPush.Id).And.NotContain(withoutPush.Id);

        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/owner/notifications", new { title = "", body = " ", userIds = new[] { withPush.Id } })))
            .Keys.Should().BeEquivalentTo("title", "body");
        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/owner/notifications", new { title = new string('t', 101), body = "x", userIds = new[] { withPush.Id } })))
            .Keys.Should().Equal("title");
        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/owner/notifications", new { title = "x", body = new string('b', 501), userIds = new[] { withPush.Id } })))
            .Keys.Should().Equal("body");
        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/owner/notifications", new { title = "x", body = "y", userIds = new[] { withoutPush.Id } })))
            .Keys.Should().Equal("userIds");

        var sent = await AreaHttp.Json(await owner.PostAsJsonAsync("/api/owner/notifications",
            new { title = $"Aviso {tag}", body = "Ensaio às 21h.", userIds = new[] { withPush.Id, withoutPush.Id } }));
        sent.GetProperty("recipients").GetInt32().Should().Be(1, "only accounts with a push subscription are sent to");

        _factory.Push.Verify(p => p.SendToSelectedUsersAsync(
            It.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { withPush.Id })),
            It.Is<SendPushNotificationDto>(n => n.Title == $"Aviso {tag}" && n.Body == "Ensaio às 21h." && n.Icon == "/icons/rtub-logo-192.png"
                && n.Tag!.StartsWith("announcement-"))), Times.Once);

        var audit = await WithDbAsync(db => db.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityDisplayName == $"Aviso {tag}"));
        audit.Action.Should().Be("PushNotificationSent");
        audit.EntityType.Should().Be("Notification");
        audit.Changes.Should().Contain("\"RecipientCount\":1").And.Contain("Ensaio");
    }
}
