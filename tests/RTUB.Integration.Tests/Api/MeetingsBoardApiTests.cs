// MeetingsBoardApiTests: test class moved here from MeetingsApiTests.cs.
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
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using static RTUB.Integration.Tests.Api.MeetingsKit;

namespace RTUB.Integration.Tests.Api;
/// <summary>
/// /api/meetings, the page itself: antiforgery, caching, search, the fiscal-year filter, ordering, answers, server-side
/// validation and the old side effects (push, email, Documentação), all through the recording fakes.
/// </summary>
public partial class MeetingsBoardApiTests : IClassFixture<MeetingsApiFactory>
{
    private readonly MeetingsApiFactory _factory;

    private readonly MeetingsKit _kit;

    public MeetingsBoardApiTests(MeetingsApiFactory factory)
    {
        _factory = factory;
        _kit = new MeetingsKit(factory);
    }

    [Fact]
    public async Task Writes_NeedTheAntiforgeryToken()
    {
        var owner = await _kit.MemberAsync("Owner");
        var meeting = await _kit.MeetingAsync(MeetingType.AssembleiaGeralOrdinaria, 6);
        owner.Client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        var title = $"Sem token {Tag()}";

        (await owner.Client.PostAsJsonAsync("/api/meetings", NewMeeting(Ago, title: title))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.Client.PutAsJsonAsync($"/api/meetings/{meeting}/participation", new { willAttend = true })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.Client.DeleteAsync($"/api/meetings/{meeting}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.Client.PostAsJsonAsync("/api/meetings/requests", NewRequest(Age, title: title))).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await _kit.StoredMeetingAsync(meeting)).Should().NotBeNull("nothing was deleted");
        _factory.Push.Verify(p => p.SendToUserAsync(It.IsAny<string>(), It.Is<SendPushNotificationDto>(n => n.Body.Contains(title))), Times.Never);
    }

    [Fact]
    public async Task EveryRead_IsNeverCached()
    {
        var member = await _kit.MemberAsync();
        var meeting = await _kit.MeetingAsync(MeetingType.AssembleiaGeralOrdinaria, 6);

        // ReadsAsync asserts Cache-Control: no-store on each answer, whatever its status.
        var statuses = await ReadsAsync(member.Client, meeting);
        statuses["/api/meetings"].Should().Be(HttpStatusCode.OK);
        statuses[$"/api/meetings/{meeting}"].Should().Be(HttpStatusCode.OK);
        statuses["/api/meetings/form"].Should().Be(HttpStatusCode.Forbidden);
        (await member.Client.GetAsync("/api/meetings/999999")).Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task TheSearch_ReadsTitleAndStatement_IgnoringCase()
    {
        var member = await _kit.MemberAsync();
        var tag = Tag();
        var byTitle = await _kit.MeetingAsync(MeetingType.AssembleiaGeralOrdinaria, 5, title: $"Orçamento {tag}");
        var byStatement = await _kit.MeetingAsync(MeetingType.AssembleiaGeralExtraordinaria, -5, statement: $"Discutir {tag} e outros assuntos");
        var neither = await _kit.MeetingAsync(MeetingType.AssembleiaGeralOrdinaria, 5, title: "Sem a palavra");

        var found = BoardIds(await BoardAsync(member.Client, $"fy=all&q={tag.ToUpperInvariant()}"));
        found.Should().Contain(new[] { byTitle, byStatement }).And.NotContain(neither);
        BoardIds(await BoardAsync(member.Client, $"fy=all&q={Uri.EscapeDataString($"  {tag} ")}")).Should().Contain(new[] { byTitle, byStatement },
            "the search is trimmed");
        BoardIds(await BoardAsync(member.Client, "fy=all")).Should().Contain(new[] { byTitle, byStatement, neither });
    }

    [Fact]
    public async Task TheFiscalYearFilter_IsTheCurrentYearByDefault_AllYearsOrOne()
    {
        var member = await _kit.MemberAsync();
        var tag = Tag();
        var start = FiscalYearHelper.GetCurrentFiscalYearStartYear();
        var inside = await _kit.MeetingOnAsync(MeetingType.AssembleiaGeralOrdinaria, new DateTime(start + 1, 1, 15, 21, 0, 0), title: $"Dentro {tag}");
        var old = await _kit.MeetingOnAsync(MeetingType.AssembleiaGeralOrdinaria, new DateTime(start - 3, 1, 15, 21, 0, 0), title: $"Antiga {tag}");

        var current = await BoardAsync(member.Client, $"q={tag}");
        BoardIds(current).Should().Contain(inside).And.NotContain(old);
        current.GetProperty("fiscalYear").GetString().Should().Be($"{start}-{start + 1}");
        current.GetProperty("currentFiscalYear").GetString().Should().Be($"{start}-{start + 1}");

        var all = await BoardAsync(member.Client, $"fy=all&q={tag}");
        BoardIds(all).Should().Contain(new[] { inside, old });
        IsNull(all, "fiscalYear").Should().BeTrue("every year");

        BoardIds(await BoardAsync(member.Client, $"fy={start - 4}-{start - 3}&q={tag}")).Should().Equal(old);

        foreach (var bad in new[] { "2025", "2025-2027", "abcd-efgh", "1999-2000" })
        {
            (await AreaHttp.Errors(await member.Client.GetAsync($"/api/meetings?fy={bad}"))).Should().ContainKey("fy", bad);
        }
    }

    [Fact]
    public async Task TheBoard_ListsUpcomingSoonestFirst_AndPastNewestFirst()
    {
        var member = await _kit.MemberAsync();
        var tag = Tag();
        var later = await _kit.MeetingAsync(MeetingType.AssembleiaGeralOrdinaria, 20, title: $"Mais tarde {tag}");
        var soon = await _kit.MeetingAsync(MeetingType.AssembleiaGeralExtraordinaria, 2, title: $"Em breve {tag}");
        var recent = await _kit.MeetingAsync(MeetingType.AssembleiaGeralOrdinaria, -2, title: $"Recente {tag}");
        var old = await _kit.MeetingAsync(MeetingType.AssembleiaGeralExtraordinaria, -20, title: $"Antiga {tag}");
        var today = await _kit.MeetingAsync(MeetingType.AssembleiaGeralOrdinaria, 0, title: $"Hoje {tag}");

        var board = await BoardAsync(member.Client, $"fy=all&q={tag}");
        Ids(board.GetProperty("upcoming")).Should().Equal(today, soon, later);
        Ids(board.GetProperty("past")).Should().Equal(recent, old);

        var todays = Cards(board).Single(c => c.GetProperty("id").GetInt32() == today);
        todays.GetProperty("completed").GetBoolean().Should().BeTrue("on the day itself the answers are closed");
        todays.GetProperty("past").GetBoolean().Should().BeFalse("it is still listed with the upcoming ones");
        Cards(board).Single(c => c.GetProperty("id").GetInt32() == recent).GetProperty("past").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Answers_AreGivenOnlyBeforeTheMeetingsDay()
    {
        var member = await _kit.MemberAsync(Who.Tuno);
        var upcoming = await _kit.MeetingAsync(MeetingType.AssembleiaGeralOrdinaria, 3);
        var today = await _kit.MeetingAsync(MeetingType.AssembleiaGeralOrdinaria, 0);
        var past = await _kit.MeetingAsync(MeetingType.AssembleiaGeralOrdinaria, -3);
        var cancelled = await _kit.MeetingAsync(MeetingType.AssembleiaGeralOrdinaria, 3, cancelled: true);
        await _kit.ParticipationAsync(past, member.Id, willAttend: true);

        var going = await AreaHttp.Json(await member.Client.PutAsJsonAsync($"/api/meetings/{upcoming}/participation", new { willAttend = true, notes = "Levo a guitarra" }));
        going.GetProperty("myParticipation").GetProperty("willAttend").GetBoolean().Should().BeTrue();
        going.GetProperty("myParticipation").GetProperty("notes").GetString().Should().Be("Levo a guitarra");
        going.GetProperty("goingCount").GetInt32().Should().Be(1);

        var notGoing = await AreaHttp.Json(await member.Client.PutAsJsonAsync($"/api/meetings/{upcoming}/participation", new { willAttend = false, notes = "" }));
        notGoing.GetProperty("myParticipation").GetProperty("willAttend").GetBoolean().Should().BeFalse("the same answer is changed, not duplicated");
        IsNull(notGoing.GetProperty("myParticipation"), "notes").Should().BeTrue();
        notGoing.GetProperty("goingCount").GetInt32().Should().Be(0);

        foreach (var closed in new[] { today, past, cancelled })
        {
            (await member.Client.PutAsJsonAsync($"/api/meetings/{closed}/participation", new { willAttend = true })).StatusCode
                .Should().Be(HttpStatusCode.Conflict, "meeting {0} no longer takes answers", closed);
        }

        var pastCard = await CardAsync(member.Client, past);
        Can(pastCard, "respond").Should().BeFalse();
        Can(pastCard, "removeOwn").Should().BeTrue("after the day a member may still remove their answer");
        var mine = pastCard.GetProperty("myParticipation").GetProperty("id").GetInt32();
        (await member.Client.DeleteAsync($"/api/meetings/{past}/participants/{mine}")).StatusCode.Should().Be(HttpStatusCode.OK);
        IsNull(await CardAsync(member.Client, past), "myParticipation").Should().BeTrue();
        (await member.Client.GetAsync($"/api/meetings/{cancelled}/participants")).StatusCode.Should().Be(HttpStatusCode.Conflict,
            "a cancelled meeting has no participants list");
    }

    [Fact]
    public async Task Forms_AreValidatedByTheServer()
    {
        var owner = await _kit.MemberAsync("Owner");
        var veterano = await _kit.MemberAsync(Who.Veterano);
        var meeting = await _kit.MeetingAsync(MeetingType.AssembleiaGeralOrdinaria, 7);

        async Task<Dictionary<string, string[]>> Create(object input) =>
            await AreaHttp.Errors(await owner.Client.PostAsJsonAsync("/api/meetings", input));
        (await Create(NewMeeting(Ago, title: new string('t', 201)))).Should().ContainKey("title");
        (await Create(NewMeeting(Ago, title: "   "))).Should().ContainKey("title");
        (await Create(NewMeeting(Ago, statement: ""))).Should().ContainKey("statement");
        (await Create(NewMeeting(Ago, statement: "   "))).Should().ContainKey("statement");
        (await Create(NewMeeting(Ago, statement: new string('s', 5001)))).Should().ContainKey("statement");
        (await Create(NewMeeting(Ago, location: new string('l', 201)))).Should().ContainKey("location");
        (await Create(new { type = "Assembleia", title = "Reunião", date = "amanhã", statement = "Ordem" })).Keys.Should().Contain(new[] { "type", "date" });
        (await Create(new { type = "2", title = "Reunião", date = Date(3), statement = "Ordem" })).Should().ContainKey("type", "a number is not a type");

        (await AreaHttp.Errors(await owner.Client.PutAsJsonAsync($"/api/meetings/{meeting}", NewMeeting(Ago, statement: new string('s', 5001)))))
            .Should().ContainKey("statement");
        (await AreaHttp.Errors(await owner.Client.PutAsJsonAsync($"/api/meetings/{meeting}/participation", new { willAttend = true, notes = new string('n', 501) })))
            .Should().ContainKey("notes");
        (await AreaHttp.Errors(await owner.Client.PostAsJsonAsync($"/api/meetings/{meeting}/cancel", new { reason = new string('r', 1001), notifyByEmail = false })))
            .Should().ContainKey("reason");
        (await AreaHttp.Errors(await owner.Client.PostAsJsonAsync($"/api/meetings/{meeting}/cancel", new { reason = "  ", notifyByEmail = false })))
            .Should().ContainKey("reason");
        (await AreaHttp.Errors(await owner.Client.PostAsJsonAsync($"/api/meetings/{meeting}/push", new { message = new string('m', 501) })))
            .Should().ContainKey("message");
        (await AreaHttp.Errors(await owner.Client.PostAsJsonAsync($"/api/meetings/{meeting}/email", new { subject = "", body = "" })))
            .Keys.Should().Contain(new[] { "subject", "body" });
        (await AreaHttp.Errors(await owner.Client.GetAsync("/api/meetings?fy=2025"))).Should().ContainKey("fy");

        async Task<Dictionary<string, string[]>> Propose(object input) =>
            await AreaHttp.Errors(await veterano.Client.PostAsJsonAsync("/api/meetings/requests", input));
        (await Propose(NewRequest(Cv, title: ""))).Should().ContainKey("title");
        (await Propose(NewRequest(Cv, title: new string('t', 201)))).Should().ContainKey("title");
        (await Propose(NewRequest(Cv, description: new string('d', 2001)))).Should().ContainKey("description");
        (await Propose(new { type = Cv, title = "Pedido", proposedDate = "", description = "Assuntos" })).Should().ContainKey("proposedDate");
        (await Propose(new { type = "Outra", title = "Pedido", proposedDate = Date(3), description = "Assuntos" })).Should().ContainKey("type");
        (await AreaHttp.Errors(await veterano.Client.GetAsync("/api/meetings/requests?status=Analysing"))).Should().ContainKey("status");

        var stored = (await _kit.StoredMeetingAsync(meeting))!;
        stored.IsCancelled.Should().BeFalse();
        stored.Statement.Should().Be("Ordem de trabalhos", "an invalid edit changes nothing");
    }
}
