// MeetingsAtaApiTests: test class moved here from MeetingsApiTests.cs.
using System.Globalization;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using static RTUB.Integration.Tests.Api.MeetingsKit;

namespace RTUB.Integration.Tests.Api;
/// <summary>
/// Atas on /api/meetings: decision A1 (a draft ata is only for whoever may write it; published atas follow the old reading
/// rule), publication to Documentação, confirmation by who attended, and the day rules.
/// </summary>
public class MeetingsAtaApiTests : IClassFixture<MeetingsApiFactory>
{
    private readonly MeetingsApiFactory _factory;
    private readonly MeetingsKit _kit;

    public MeetingsAtaApiTests(MeetingsApiFactory factory)
    {
        _factory = factory;
        _kit = new MeetingsKit(factory);
    }

    [Fact]
    public async Task ADraftAta_IsOnlyForItsWriters_UntilItIsPublished()
    {
        var caloiro = await _kit.MemberAsync(Who.Caloiro);
        var tuno = await _kit.MemberAsync(Who.Tuno);
        var president = await _kit.MemberAsync(Who.Holding(Position.PresidenteMesaAssembleia));
        var owner = await _kit.MemberAsync("Owner");
        const int days = -41;
        var meeting = await _kit.MeetingAsync(MeetingType.AssembleiaGeralOrdinaria, days);
        await _kit.AtaAsync(meeting, president.Id);

        foreach (var reader in new[] { caloiro, tuno })
        {
            (await reader.Client.GetAsync($"/api/meetings/{meeting}/ata")).StatusCode.Should().Be(HttpStatusCode.NotFound, "A1: a draft is never shown to a reader");
            (await reader.Client.GetAsync($"/api/meetings/{meeting}/ata/pdf")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            var card = await CardAsync(reader.Client, meeting);
            IsNull(card, "ataStatus").Should().BeTrue("the card does not even say a draft exists");
            Can(card, "viewAta").Should().BeFalse();
            Can(card, "writeAta").Should().BeFalse();
            IsNull(Cards(await BoardAsync(reader.Client)).Single(c => c.GetProperty("id").GetInt32() == meeting), "ataStatus").Should().BeTrue();
        }

        foreach (var writer in new[] { president, owner })
        {
            var card = await CardAsync(writer.Client, meeting);
            card.GetProperty("ataStatus").GetString().Should().Be("draft");
            Can(card, "viewAta").Should().BeTrue();
            Can(card, "writeAta").Should().BeTrue();
            (await AreaHttp.Json(writer.Client, $"/api/meetings/{meeting}/ata")).GetProperty("status").GetString().Should().Be("draft");
            var pdf = await writer.Client.GetAsync($"/api/meetings/{meeting}/ata/pdf");
            pdf.StatusCode.Should().Be(HttpStatusCode.OK);
            pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        }

        (await AreaHttp.Json(await president.Client.PostAsync($"/api/meetings/{meeting}/ata/publish", null)))
            .GetProperty("status").GetString().Should().Be("published");
        var fileName = $"Ata_AssembleiaGeralOrdinaria_{DateTime.Today.AddDays(days):yyyyMMdd}.pdf";
        _factory.Documents.Verify(d => d.UploadDocumentAsync(It.Is<string>(folder => folder.Contains("Atas AG")), fileName, It.IsAny<Stream>(), "application/pdf"),
            Times.Once, "the published PDF goes to Documentação, as before");
        (await president.Client.PostAsync($"/api/meetings/{meeting}/ata/publish", null)).StatusCode.Should().Be(HttpStatusCode.Conflict, "already published");

        foreach (var reader in new[] { caloiro, tuno })
        {
            var view = await AreaHttp.Json(reader.Client, $"/api/meetings/{meeting}/ata");
            view.GetProperty("status").GetString().Should().Be("published");
            view.GetProperty("agendaPoints").GetArrayLength().Should().Be(1);
            var card = await CardAsync(reader.Client, meeting);
            card.GetProperty("ataStatus").GetString().Should().Be("published");
            Can(card, "viewAta").Should().BeTrue();
            Can(card, "writeAta").Should().BeFalse();
            var pdf = await reader.Client.GetAsync($"/api/meetings/{meeting}/ata/pdf");
            pdf.StatusCode.Should().Be(HttpStatusCode.OK);
            pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        }
    }

    [Fact]
    public async Task APublishedAta_IsConfirmedOnlyByWhoSaidVou()
    {
        var attendee = await _kit.MemberAsync(Who.Caloiro);
        var absent = await _kit.MemberAsync(Who.Tuno);
        var stranger = await _kit.MemberAsync(Who.Tuno);
        var president = await _kit.MemberAsync(Who.Holding(Position.PresidenteMesaAssembleia));
        var meeting = await _kit.MeetingAsync(MeetingType.AssembleiaGeralExtraordinaria, -43);
        var draftMeeting = await _kit.MeetingAsync(MeetingType.AssembleiaGeralExtraordinaria, -44);
        await _kit.ParticipationAsync(meeting, attendee.Id, willAttend: true);
        await _kit.ParticipationAsync(meeting, absent.Id, willAttend: false);
        await _kit.ParticipationAsync(draftMeeting, attendee.Id, willAttend: true);
        await _kit.AtaAsync(meeting, president.Id, MeetingAtaStatus.Published);
        await _kit.AtaAsync(draftMeeting, president.Id);

        var view = await AreaHttp.Json(attendee.Client, $"/api/meetings/{meeting}/ata");
        view.GetProperty("present").EnumerateArray().Select(p => p.GetString()).Should().ContainSingle(p => p!.Contains(attendee.User.Nickname!));
        IsNull(view.GetProperty("mine"), "confirmed").Should().BeTrue("not answered yet");

        var confirmed = await AreaHttp.Json(await attendee.Client.PostAsJsonAsync($"/api/meetings/{meeting}/ata/confirmation", new { confirm = true }));
        confirmed.GetProperty("mine").GetProperty("confirmed").GetBoolean().Should().BeTrue();
        confirmed.GetProperty("confirmed").EnumerateArray().Select(p => p.GetString()).Should().Contain(p => p!.Contains(attendee.User.Nickname!));
        var refused = await AreaHttp.Json(await attendee.Client.PostAsJsonAsync($"/api/meetings/{meeting}/ata/confirmation", new { confirm = false }));
        refused.GetProperty("mine").GetProperty("confirmed").GetBoolean().Should().BeFalse();

        IsNull(await AreaHttp.Json(absent.Client, $"/api/meetings/{meeting}/ata"), "mine").Should().BeTrue("only who attended answers");
        (await absent.Client.PostAsJsonAsync($"/api/meetings/{meeting}/ata/confirmation", new { confirm = true })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await stranger.Client.PostAsJsonAsync($"/api/meetings/{meeting}/ata/confirmation", new { confirm = true })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await attendee.Client.PostAsJsonAsync($"/api/meetings/{draftMeeting}/ata/confirmation", new { confirm = true })).StatusCode
            .Should().Be(HttpStatusCode.NotFound, "a draft is neither shown nor confirmed");
    }

    [Fact]
    public async Task Atas_FollowTheMeetingsDay_AndOnlyASavedDraftIsPublished()
    {
        var president = await _kit.MemberAsync(Who.Holding(Position.PresidenteMesaAssembleia));
        var upcoming = await _kit.MeetingAsync(MeetingType.AssembleiaGeralOrdinaria, 4);
        var cancelled = await _kit.MeetingAsync(MeetingType.AssembleiaGeralOrdinaria, -4, cancelled: true);
        var unsaved = await _kit.MeetingAsync(MeetingType.AssembleiaGeralOrdinaria, -45);

        (await president.Client.GetAsync($"/api/meetings/{upcoming}/ata/edit")).StatusCode.Should().Be(HttpStatusCode.Conflict, "no ata before the meeting's day");
        (await president.Client.PutAsJsonAsync($"/api/meetings/{upcoming}/ata", NewAta(assembly: true))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await president.Client.GetAsync($"/api/meetings/{cancelled}/ata/edit")).StatusCode.Should().Be(HttpStatusCode.Conflict, "nor for a cancelled meeting");
        (await president.Client.PostAsync($"/api/meetings/{unsaved}/ata/publish", null)).StatusCode.Should().Be(HttpStatusCode.Conflict, "only a saved draft is published");
        (await president.Client.GetAsync($"/api/meetings/{unsaved}/ata")).StatusCode.Should().Be(HttpStatusCode.NotFound, "nothing to read yet");

        var errors = await AreaHttp.Errors(await president.Client.PutAsJsonAsync($"/api/meetings/{unsaved}/ata", new
        {
            ataNumber = new string('n', 51),
            actualStartTime = "",
            location = "",
            quorumBasis = "Sempre",
            agendaPoints = new[] { new { title = "", result = "Talvez" } },
            closingText = new string('c', 5001),
        }));
        errors.Keys.Should().Contain(new[] { "ataNumber", "actualStartTime", "location", "quorumBasis", "agendaPoints[0]", "closingText" });
        (await _kit.StoredAtaAsync(unsaved)).Should().BeNull();

        var saved = await AreaHttp.Json(await president.Client.PutAsJsonAsync($"/api/meetings/{unsaved}/ata", NewAta(assembly: true)));
        saved.GetProperty("status").GetString().Should().Be("draft");
        saved.GetProperty("agendaPoints").GetArrayLength().Should().Be(1);
        (await _kit.StoredAtaAsync(unsaved))!.QuorumBasis.Should().Be("HoraAgendada");
    }
}
