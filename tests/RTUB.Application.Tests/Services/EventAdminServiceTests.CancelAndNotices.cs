using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

// EventAdminServiceTests: Cancel And Notices tests.
public partial class EventAdminServiceTests
{
    // ---------- cancel / reactivate ----------

    [Fact]
    public async Task Cancel_NeedsAReason_OfAtMost1000Characters()
    {
        var id = AddEvent("Serenata", _future);

        (await _service.CancelAsync(id, new EventCancelInput("  ", false), Admin, "x")).Errors!.Keys.Should().Contain("reason");
        (await _service.CancelAsync(id, new EventCancelInput(new string('r', 1001), false), Admin, "x")).Errors!.Keys.Should().Contain("reason");
        _events.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Cancel_WithoutEmail_CancelsAndSendsNothing()
    {
        var id = AddEvent("Serenata", _future);
        AddUser("a@rtub.test");

        var result = await _service.CancelAsync(id, new EventCancelInput("  Chuva  ", false), Admin, "https://rtub.test");

        result.Status.Should().Be(EventResultStatus.Ok);
        result.Value!.Sent.Should().Be(0);
        _events.Verify(s => s.CancelEventAsync(id, "Chuva", default), Times.Once);
        _email.VerifyNoOtherCalls();
        (await EventAsync(id)).IsCancelled.Should().BeTrue();
    }

    [Fact]
    public async Task Cancel_WithEmail_WritesToSubscribedConfirmedAddressesOnly()
    {
        var id = AddEvent("Serenata", _future);
        AddUser("yes@rtub.test");
        AddUser("unsubscribed@rtub.test", subscribed: false);
        AddUser("unconfirmed@rtub.test", confirmed: false);

        var result = await _service.CancelAsync(id, new EventCancelInput("Chuva", true), Admin, "https://rtub.test");

        result.Value!.Sent.Should().Be(1);
        _email.Verify(m => m.SendEventCancellationNotificationAsync(id, "Serenata", It.IsAny<DateTime>(), "Bragança", "Chuva",
            "https://rtub.test/events", It.Is<List<string>>(l => l.SequenceEqual(new[] { "yes@rtub.test" })),
            It.IsAny<Dictionary<string, (string, string)>>(), null, null), Times.Once);
    }

    [Fact]
    public async Task Cancel_StaysCancelled_WhenTheEmailFails()
    {
        var id = AddEvent("Serenata", _future);
        AddUser("yes@rtub.test");
        _email.Setup(m => m.SendEventCancellationNotificationAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<Dictionary<string, (string, string)>>(),
                It.IsAny<DateTime?>(), It.IsAny<IProgress<EmailSendProgress>?>()))
            .ThrowsAsync(new InvalidOperationException("smtp down"));

        var result = await _service.CancelAsync(id, new EventCancelInput("Chuva", true), Admin, "x");

        result.Status.Should().Be(EventResultStatus.Ok);
        result.Value!.Warning.Should().Contain("foi cancelada").And.Contain("não foram enviados");
        (await EventAsync(id)).IsCancelled.Should().BeTrue();
    }

    [Fact]
    public async Task Cancel_IsClosed_ForAPastOrAlreadyCancelledEvent()
    {
        var past = AddEvent("Passada", DateTime.Today.AddDays(-3));
        var cancelled = AddEvent("Cancelada", _future, cancelled: true);

        (await _service.CancelAsync(past, new EventCancelInput("x", false), Admin, "x")).Status.Should().Be(EventResultStatus.Closed);
        (await _service.CancelAsync(cancelled, new EventCancelInput("x", false), Admin, "x")).Status.Should().Be(EventResultStatus.Closed);
        _events.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Cancel_IsOpen_OnTheLastDayOfAMultiDayEvent()
    {
        var id = AddEvent("Festival", DateTime.Today.AddDays(-2), endDate: DateTime.Today);

        (await _service.CancelAsync(id, new EventCancelInput("x", false), Admin, "x")).Status.Should().Be(EventResultStatus.Ok);
    }

    [Fact]
    public async Task Reactivate_OnlyACancelledUpcomingEvent()
    {
        var open = AddEvent("Aberta", _future);
        var pastCancelled = AddEvent("Passada", DateTime.Today.AddDays(-3), cancelled: true);
        var cancelled = AddEvent("Cancelada", _future, cancelled: true);

        (await _service.ReactivateAsync(open, Admin)).Status.Should().Be(EventResultStatus.Closed);
        (await _service.ReactivateAsync(pastCancelled, Admin)).Status.Should().Be(EventResultStatus.Closed);
        (await _service.ReactivateAsync(cancelled, Admin)).Status.Should().Be(EventResultStatus.Ok);

        _events.Verify(s => s.UncancelEventAsync(cancelled, default), Times.Once);
        _events.Verify(s => s.UncancelEventAsync(It.Is<int>(i => i != cancelled), default), Times.Never);
        _email.VerifyNoOtherCalls();
        _push.VerifyNoOtherCalls();
    }

    // ---------- notices ----------

    [Fact]
    public async Task Audience_IsCountsOnly()
    {
        var id = AddEvent("Serenata", _future);
        AddUser("a@rtub.test", push: true);
        AddUser("b@rtub.test", subscribed: false, push: true, category: MemberCategory.Leitao);
        AddUser("c@rtub.test", confirmed: false, category: MemberCategory.Caloiro);

        var audience = (await _service.GetNoticeAudienceAsync(id, Admin)).Value!;

        audience.Should().Be(new EventNoticeAudienceDto(EmailSubscribed: 1, EmailTotal: 2, PushSubscribed: 2, PushTotal: 3,
            PushLeitoesCaloirosSubscribed: 1, PushLeitoesCaloirosTotal: 2));
    }

    [Theory]
    [InlineData("new")]
    [InlineData("reminder")]
    public async Task EmailNotice_UsesTheOldTemplates_ForSubscribedConfirmedAddresses(string kind)
    {
        var id = AddEvent("Serenata", _future);
        AddUser("yes@rtub.test");
        AddUser("no@rtub.test", subscribed: false);

        var result = await _service.SendNoticeAsync(id, Email(kind), Admin, "https://rtub.test");

        result.Value!.Sent.Should().Be(1);
        if (kind == "new")
        {
            _email.Verify(m => m.SendEventNotificationAsync(id, "Serenata", It.IsAny<DateTime>(), "Bragança", "https://rtub.test/events",
                It.Is<List<string>>(l => l.SequenceEqual(new[] { "yes@rtub.test" })), It.IsAny<Dictionary<string, (string, string)>>(), It.IsAny<string>(), null, null), Times.Once);
        }
        else
        {
            _email.Verify(m => m.SendEventReminderNotificationAsync(id, "Serenata", It.IsAny<DateTime>(), "Bragança", "https://rtub.test/events",
                It.Is<List<string>>(l => l.SequenceEqual(new[] { "yes@rtub.test" })), It.IsAny<Dictionary<string, (string, string)>>(), It.IsAny<string>(), null, null), Times.Once);
        }

        _push.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EmailNotice_ShowsTheEmailServicesRefusal_ForExampleItsRateLimit()
    {
        var id = AddEvent("Serenata", _future);
        AddUser("yes@rtub.test");
        _email.Setup(m => m.SendEventNotificationAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<Dictionary<string, (string, string)>>(), It.IsAny<string>(),
                It.IsAny<DateTime?>(), It.IsAny<IProgress<EmailSendProgress>?>()))
            .ReturnsAsync((false, 0, "Email já enviado recentemente para este evento."));

        var result = await _service.SendNoticeAsync(id, Email("new"), Admin, "x");

        result.Status.Should().Be(EventResultStatus.Invalid);
        result.Errors!["notice"].Single().Should().Be("Email já enviado recentemente para este evento.");
    }

    [Fact]
    public async Task Notices_AreClosed_ForCancelledOrPastEvents()
    {
        var past = AddEvent("Passada", DateTime.Today.AddDays(-3));
        var cancelled = AddEvent("Cancelada", _future, cancelled: true);
        AddUser("yes@rtub.test", push: true);

        (await _service.SendNoticeAsync(past, Email("new"), Admin, "x")).Status.Should().Be(EventResultStatus.Closed);
        (await _service.SendNoticeAsync(cancelled, Push("Olá"), Admin, "x")).Status.Should().Be(EventResultStatus.Closed);
        _email.VerifyNoOtherCalls();
        _push.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Notices_RefuseAnUnknownChannelOrKind_AndAnEmptyAudience()
    {
        var id = AddEvent("Serenata", _future);

        (await _service.SendNoticeAsync(id, new EventNoticeInput("sms", null, "x", false), Admin, "x")).Errors!.Keys.Should().Contain("channel");
        (await _service.SendNoticeAsync(id, Email("weekly"), Admin, "x")).Errors!.Keys.Should().Contain("kind");
        (await _service.SendNoticeAsync(id, Email("new"), Admin, "x")).Errors!.Keys.Should().Contain("notice", "nobody is subscribed");
        _email.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PushNotice_NeedsAMessage_OfAtMost500Characters()
    {
        var id = AddEvent("Serenata", _future);
        AddUser("a@rtub.test", push: true);

        (await _service.SendNoticeAsync(id, Push(" "), Admin, "x")).Errors!.Keys.Should().Contain("message");
        (await _service.SendNoticeAsync(id, Push(new string('m', 501)), Admin, "x")).Errors!.Keys.Should().Contain("message");
        _push.Verify(p => p.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<SendPushNotificationDto>()), Times.Never);
    }

    [Fact]
    public async Task PushNotice_GoesToSubscribedMembers_OptionallyLeitoesAndCaloirosOnly_AndIsAudited()
    {
        var id = AddEvent("Serenata", _future);
        var tuno = AddUser("t@rtub.test", push: true);
        var leitao = AddUser("l@rtub.test", push: true, category: MemberCategory.Leitao);
        AddUser("n@rtub.test", category: MemberCategory.Caloiro);

        (await _service.SendNoticeAsync(id, Push("Ensaio extra"), Admin, "https://rtub.test")).Value!.Sent.Should().Be(2);
        _push.Verify(p => p.SendToSelectedUsersAsync(It.Is<IEnumerable<string>>(ids => ids.OrderBy(x => x).SequenceEqual(new[] { tuno, leitao }.OrderBy(x => x))),
            It.Is<SendPushNotificationDto>(n => n.Title == "Serenata" && n.Body == "Ensaio extra")), Times.Once);

        (await _service.SendNoticeAsync(id, Push("Só os novos", young: true), Admin, "x")).Value!.Sent.Should().Be(1);
        _push.Verify(p => p.SendToSelectedUsersAsync(It.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { leitao })),
            It.IsAny<SendPushNotificationDto>()), Times.Once);

        _audit.Verify(a => a.AddAsync(It.Is<AuditLog>(l => l.Action == "PushNotificationSent" && l.EntityId == id && l.EntityType == "Event")), Times.Exactly(2));
        _email.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PushNotice_ReportsPartialDelivery_AndAnAuditFailureDoesNotFailIt()
    {
        var id = AddEvent("Serenata", _future);
        AddUser("a@rtub.test", push: true);
        _push.Setup(p => p.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<SendPushNotificationDto>())).ReturnsAsync((0, 1));
        _audit.Setup(a => a.AddAsync(It.IsAny<AuditLog>())).ThrowsAsync(new InvalidOperationException("db"));

        var result = await _service.SendNoticeAsync(id, Push("Olá"), Admin, "x");

        result.Status.Should().Be(EventResultStatus.Ok);
        result.Value!.Should().Be(new EventNoticeResultDto(0, 1, "Algumas notificações não foram entregues."));
    }

    private async Task<Event> EventAsync(int id)
    {
        await using var db = _db.CreateContext();
        return await db.Events.AsNoTracking().SingleAsync(e => e.Id == id);
    }
}
