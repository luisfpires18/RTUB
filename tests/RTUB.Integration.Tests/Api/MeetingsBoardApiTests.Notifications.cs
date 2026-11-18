// MeetingsBoardApiTests tests: pushes and emails sent for meetings.
using System.Net;
using FluentAssertions;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Core.Enums;
using static RTUB.Integration.Tests.Api.MeetingsKit;

namespace RTUB.Integration.Tests.Api;
public partial class MeetingsBoardApiTests
{
    // ---------- side effects ----------

    [Fact]
    public async Task CreatingAnAssembly_PushesEveryMember_ButNoLeitao()
    {
        var owner = await _kit.MemberAsync("Owner");
        var member = await _kit.MemberAsync(Who.Caloiro);
        var leitao = await _kit.MemberAsync(Who.Leitao);
        var title = $"Assembleia {Tag()}";

        await AreaHttp.Json(await owner.Client.PostAsJsonAsync("/api/meetings", NewMeeting(Ago, title: title)));

        _factory.Push.Verify(p => p.SendToUserAsync(member.Id, It.Is<SendPushNotificationDto>(n => n.Body.Contains(title) && n.Url!.StartsWith("/meetings?meeting="))),
            Times.Once);
        _factory.Push.Verify(p => p.SendToUserAsync(owner.Id, It.Is<SendPushNotificationDto>(n => n.Body.Contains(title))), Times.Once);
        _factory.Push.Verify(p => p.SendToUserAsync(leitao.Id, It.Is<SendPushNotificationDto>(n => n.Body.Contains(title))), Times.Never,
            "an assembly is not announced to a Leitão");
    }

    [Fact]
    public async Task AProposal_IsPushedToThePresident_AndARejection_ToItsAuthor()
    {
        var president = await _kit.MemberAsync(Who.Veterano, Who.Holding(Position.PresidenteConselhoVeteranos));
        var author = await _kit.MemberAsync(Who.Veterano);
        var title = $"Conselho {Tag()}";

        var id = await CreatedIdAsync(await author.Client.PostAsJsonAsync("/api/meetings/requests", NewRequest(Cv, title: title)));
        _factory.Push.Verify(p => p.SendToUserAsync(president.Id, It.Is<SendPushNotificationDto>(n => n.Body.Contains(title))), Times.Once);
        var stored = (await _kit.StoredRequestAsync(id))!;
        stored.AuthorUserId.Should().Be(author.Id);
        stored.Status.Should().Be(RequestStatus.Pending);

        (await president.Client.PostAsync($"/api/meetings/requests/{id}/reject", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        _factory.Push.Verify(p => p.SendToUserAsync(author.Id,
            It.Is<SendPushNotificationDto>(n => n.Title == "Pedido de reunião rejeitado" && n.Body.Contains(title))), Times.Once);
        (await _kit.StoredRequestAsync(id))!.Status.Should().Be(RequestStatus.Rejected);
        (await author.Client.PostAsync($"/api/meetings/requests/{id}/reminder", null)).StatusCode.Should().Be(HttpStatusCode.Conflict,
            "only a pending request is reminded");
    }

    [Fact]
    public async Task TheMeetingEmail_GoesToTheAssembly_WithoutLeitoes_AndShowsNoAddress()
    {
        var owner = await _kit.MemberAsync("Owner");
        var member = await _kit.MemberAsync(Who.Caloiro);
        var leitao = await _kit.MemberAsync(Who.Leitao);
        var meeting = await _kit.MeetingAsync(MeetingType.AssembleiaGeralOrdinaria, 11);

        var draftResponse = await owner.Client.GetAsync($"/api/meetings/{meeting}/email");
        var raw = await draftResponse.Content.ReadAsStringAsync();
        draftResponse.StatusCode.Should().Be(HttpStatusCode.OK, raw);
        raw.Should().NotContain(member.User.Email!, "recipient lists carry names, never addresses");
        var draft = await AreaHttp.Json(owner.Client, $"/api/meetings/{meeting}/email");
        Names(draft.GetProperty("recipients")).Should().Contain(member.User.Nickname).And.NotContain(leitao.User.Nickname);
        Names(draft.GetProperty("notReceiving")).Should().NotContain(leitao.User.Nickname);

        var token = $"Pauta{Tag()}";
        (await AreaHttp.Json(await owner.Client.PostAsJsonAsync($"/api/meetings/{meeting}/email/preview", new { body = token })))
            .GetProperty("html").GetString().Should().Contain(token);

        var sent = await AreaHttp.Json(await owner.Client.PostAsJsonAsync($"/api/meetings/{meeting}/email", new { subject = "[RTUB] Convocatória", body = token }));
        sent.GetProperty("sent").GetInt32().Should().Be(1, "what the email service reported");
        _factory.Email.Verify(e => e.SendMeetingNotificationAsync(meeting, "[RTUB] Convocatória", It.Is<string>(html => html.Contains(token)),
                It.Is<List<string>>(to => to.Contains(member.User.Email!) && to.Contains(owner.User.Email!) && !to.Contains(leitao.User.Email!)),
                It.IsAny<Dictionary<string, (string, string)>?>(), It.IsAny<IProgress<EmailSendProgress>?>()),
            Times.Once);

        // Cancelling with "notify by email" sends the cancellation to the same audience.
        await AreaHttp.Json(await owner.Client.PostAsJsonAsync($"/api/meetings/{meeting}/cancel", new { reason = "Sala ocupada", notifyByEmail = true }));
        _factory.Email.Verify(e => e.SendMeetingNotificationAsync(meeting, It.Is<string>(s => s.Contains("Cancelamento")), It.IsAny<string>(),
                It.Is<List<string>>(to => to.Contains(member.User.Email!) && !to.Contains(leitao.User.Email!)),
                It.IsAny<Dictionary<string, (string, string)>?>(), It.IsAny<IProgress<EmailSendProgress>?>()),
            Times.Once);
        (await _kit.StoredMeetingAsync(meeting))!.IsCancelled.Should().BeTrue();
    }

    [Fact]
    public async Task TheMeetingPush_GoesToSubscribedMembersOfItsAudience()
    {
        var owner = await _kit.MemberAsync("Owner");
        var subscribed = await _kit.MemberAsync(Who.Tuno);
        var unsubscribed = await _kit.MemberAsync(Who.Tuno);
        var leitao = await _kit.MemberAsync(Who.Leitao);
        _factory.Subscribed.Add(subscribed.Id);
        _factory.Subscribed.Add(leitao.Id);
        var meeting = await _kit.MeetingAsync(MeetingType.AssembleiaGeralExtraordinaria, 12);
        var message = $"Não faltem {Tag()}";

        var draft = await AreaHttp.Json(owner.Client, $"/api/meetings/{meeting}/push");
        Names(draft.GetProperty("recipients")).Should().Contain(subscribed.User.Nickname)
            .And.NotContain(unsubscribed.User.Nickname).And.NotContain(leitao.User.Nickname);
        Names(draft.GetProperty("notSubscribed")).Should().Contain(unsubscribed.User.Nickname).And.NotContain(leitao.User.Nickname);

        var sent = await AreaHttp.Json(await owner.Client.PostAsJsonAsync($"/api/meetings/{meeting}/push", new { message }));
        sent.GetProperty("sent").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        _factory.Push.Verify(p => p.SendToUserAsync(subscribed.Id, It.Is<SendPushNotificationDto>(n => n.Body == message)), Times.Once);
        _factory.Push.Verify(p => p.SendToUserAsync(unsubscribed.Id, It.Is<SendPushNotificationDto>(n => n.Body == message)), Times.Never);
        _factory.Push.Verify(p => p.SendToUserAsync(leitao.Id, It.Is<SendPushNotificationDto>(n => n.Body == message)), Times.Never,
            "an assembly is not pushed to a Leitão");
    }

    [Fact]
    public async Task EditingACancelledMeeting_KeepsItCancelled()
    {
        var owner = await _kit.MemberAsync("Owner");
        var meeting = await _kit.MeetingAsync(MeetingType.AssembleiaGeralOrdinaria, 13, cancelled: true);

        var saved = await AreaHttp.Json(await owner.Client.PutAsJsonAsync($"/api/meetings/{meeting}", NewMeeting(Ago, title: "Assembleia adiada")));

        var card = saved.GetProperty("card");
        card.GetProperty("cancelled").GetBoolean().Should().BeTrue("the old edit form reactivated it silently; now it stays cancelled");
        Can(card, "uncancel").Should().BeTrue();
        Can(card, "notify").Should().BeFalse();
        var stored = (await _kit.StoredMeetingAsync(meeting))!;
        stored.IsCancelled.Should().BeTrue();
        stored.CancellationReason.Should().Be("Motivo interno");
        stored.Title.Should().Be("Assembleia adiada");
    }
}
