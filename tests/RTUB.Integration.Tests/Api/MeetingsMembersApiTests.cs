// MeetingsMembersApiTests: test class moved here from MeetingsApiTests.cs.
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
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using static RTUB.Integration.Tests.Api.MeetingsKit;

namespace RTUB.Integration.Tests.Api;
/// <summary>
/// /api/meetings (task 034, was the Blazor /meetings) through the real host - real login, antiforgery, SQLite and the old
/// meeting services - member type by member type: visitors, Leitões, Caloiros, Tunos, Veteranos / Tunossauros, Admin and
/// Owner (docs/react-meetings.md, "Permission matrix"). Push, email and document storage are recording fakes. Every test
/// seeds its own meetings and asserts by id, never by count: the database is shared by the class.
/// </summary>
public partial class MeetingsMembersApiTests : IClassFixture<MeetingsApiFactory>
{
    private readonly MeetingsApiFactory _factory;

    private readonly MeetingsKit _kit;

    public MeetingsMembersApiTests(MeetingsApiFactory factory)
    {
        _factory = factory;
        _kit = new MeetingsKit(factory);
    }

    // ---------- visitors ----------

    [Fact]
    public async Task Visitors_GetNothing_AndThePageSendsThemToSignIn()
    {
        var author = await _kit.MemberAsync(Who.Veterano);
        var meeting = await _kit.MeetingAsync(MeetingType.AssembleiaGeralOrdinaria, 10);
        var request = await _kit.RequestAsync(MeetingType.ConselhoVeteranos, author.Id);
        var anonymous = AreaHttp.Anonymous(_factory, "10.66.0.1");
        await AreaHttp.WithTokenAsync(anonymous);

        AllAnswer(await ReadsAsync(anonymous, meeting), HttpStatusCode.Unauthorized);
        AllAnswer(await MeetingWritesAsync(anonymous, meeting), HttpStatusCode.Unauthorized);
        AllAnswer(await OtherWritesAsync(anonymous, request), HttpStatusCode.Unauthorized);

        var page = await anonymous.GetAsync("/meetings");
        page.StatusCode.Should().Be(HttpStatusCode.Redirect);
        page.Headers.Location!.ToString().Should().Be("/login?returnUrl=%2Fmeetings");
        (await _kit.StoredMeetingAsync(meeting)).Should().NotBeNull("nothing a visitor sent was applied");
    }

    [Fact]
    public async Task Members_GetTheReactPage()
    {
        var member = await _kit.MemberAsync();

        (await member.Client.GetStringAsync("/meetings")).Should().Contain("id=\"root\"").And.NotContain("blazor.web.js");
    }

    // ---------- Leitão ----------

    [Theory]
    [InlineData("Member")]
    [InlineData("Mod")]
    [InlineData("Admin")]
    [InlineData("Owner")]
    public async Task ALeitao_IsRefusedEverything_WhateverTheirRole(string role)
    {
        var leitao = await _kit.MemberAsync(role, Who.Leitao);
        var author = await _kit.MemberAsync(Who.Veterano);
        var meeting = await _kit.MeetingAsync(MeetingType.AssembleiaGeralOrdinaria, 10);
        var request = await _kit.RequestAsync(MeetingType.AssembleiaGeralExtraordinaria, author.Id);

        AllAnswer(await ReadsAsync(leitao.Client, meeting), HttpStatusCode.Forbidden);
        AllAnswer(await MeetingWritesAsync(leitao.Client, meeting), HttpStatusCode.Forbidden);
        AllAnswer(await OtherWritesAsync(leitao.Client, request), HttpStatusCode.Forbidden);
        (await _kit.StoredMeetingAsync(meeting)).Should().NotBeNull();
        (await _kit.StoredRequestAsync(request))!.Status.Should().Be(RequestStatus.Pending);
    }

    // ---------- Caloiro ----------

    [Fact]
    public async Task ACaloiro_SeesTheAssemblies_NotCvNorDirecao_AndManagesNothing()
    {
        var caloiro = await _kit.MemberAsync(Who.Caloiro);
        var other = await _kit.MemberAsync(Who.Tuno);
        var ago = await _kit.MeetingAsync(MeetingType.AssembleiaGeralOrdinaria, 10);
        var age = await _kit.MeetingAsync(MeetingType.AssembleiaGeralExtraordinaria, -10);
        var cv = await _kit.MeetingAsync(MeetingType.ConselhoVeteranos, 10);
        var direcao = await _kit.MeetingAsync(MeetingType.ReuniaoDirecao, 10);
        await _kit.ParticipationAsync(ago, other.Id, willAttend: true, notes: "Levo o estandarte");

        var board = await BoardAsync(caloiro.Client);
        BoardIds(board).Should().Contain(new[] { ago, age }).And.NotContain(cv).And.NotContain(direcao);
        var access = board.GetProperty("access");
        access.GetProperty("canCreate").GetBoolean().Should().BeFalse();
        access.GetProperty("meetingTypes").GetArrayLength().Should().Be(0);
        access.GetProperty("canProposeCv").GetBoolean().Should().BeFalse();
        access.GetProperty("canProposeDirecao").GetBoolean().Should().BeFalse();
        access.GetProperty("canProposeAg").GetBoolean().Should().BeFalse();
        access.GetProperty("canSeeRequests").GetBoolean().Should().BeFalse();
        access.GetProperty("canAddParticipants").GetBoolean().Should().BeFalse();

        var card = await CardAsync(caloiro.Client, ago);
        Can(card, "manage").Should().BeFalse();
        Can(card, "notify").Should().BeFalse();
        Can(card, "respond").Should().BeTrue("an upcoming meeting takes Vou / Não vou");
        IsNull(card, "tunoRepresentativeId").Should().BeTrue("only managers get it");

        // A7: the participants and their notes are for everyone who sees the meeting.
        var participants = await AreaHttp.Json(caloiro.Client, $"/api/meetings/{ago}/participants");
        var going = participants.GetProperty("going").EnumerateArray().Single(p => p.GetProperty("nickname").GetString() == other.User.Nickname);
        going.GetProperty("notes").GetString().Should().Be("Levo o estandarte");
        going.GetProperty("canRemove").GetBoolean().Should().BeFalse("nobody removes someone else's answer");
        participants.GetProperty("canAdd").GetBoolean().Should().BeFalse();

        foreach (var hidden in new[] { cv, direcao })
        {
            (await caloiro.Client.GetAsync($"/api/meetings/{hidden}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await caloiro.Client.GetAsync($"/api/meetings/{hidden}/participants")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            AllAnswer(await MeetingWritesAsync(caloiro.Client, hidden), HttpStatusCode.NotFound);
        }

        (await caloiro.Client.GetAsync("/api/meetings/form")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await caloiro.Client.PostAsJsonAsync("/api/meetings", NewMeeting(Ago))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await caloiro.Client.PutAsJsonAsync($"/api/meetings/{ago}", NewMeeting(Ago))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await caloiro.Client.DeleteAsync($"/api/meetings/{ago}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await caloiro.Client.PostAsJsonAsync($"/api/meetings/{ago}/cancel", new { reason = "Não", notifyByEmail = false })).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        foreach (var type in new[] { Cv, Direcao, Age, Ago })
        {
            (await caloiro.Client.PostAsJsonAsync("/api/meetings/requests", NewRequest(type))).StatusCode.Should().Be(HttpStatusCode.Forbidden, type);
        }

        (await caloiro.Client.GetAsync("/api/meetings/requests")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _kit.StoredMeetingAsync(ago))!.IsCancelled.Should().BeFalse();
    }

    // ---------- Tuno (under 2 years) ----------

    [Fact]
    public async Task ATuno_SeesTheCvTheyRepresent_AndWritesItsAta_ButNoOtherCvNorAnAssemblyAta()
    {
        var tuno = await _kit.MemberAsync(Who.Tuno);
        var represented = await _kit.MeetingAsync(MeetingType.ConselhoVeteranos, -4, representativeId: tuno.Id);
        var otherCv = await _kit.MeetingAsync(MeetingType.ConselhoVeteranos, -4);
        var assembly = await _kit.MeetingAsync(MeetingType.AssembleiaGeralOrdinaria, -4);
        var direcao = await _kit.MeetingAsync(MeetingType.ReuniaoDirecao, 5);

        BoardIds(await BoardAsync(tuno.Client)).Should().Contain(new[] { represented, assembly }).And.NotContain(otherCv).And.NotContain(direcao);
        var card = await CardAsync(tuno.Client, represented);
        card.GetProperty("tunoRepresentative").GetString().Should().Be(tuno.User.Nickname);
        Can(card, "writeAta").Should().BeTrue("A5: the Tuno representative writes their CV's ata");
        (await tuno.Client.GetAsync($"/api/meetings/{otherCv}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await tuno.Client.GetAsync($"/api/meetings/{otherCv}/ata/edit")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await tuno.Client.GetAsync($"/api/meetings/{direcao}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await tuno.Client.GetAsync($"/api/meetings/{represented}/ata/edit")).StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = await AreaHttp.Json(await tuno.Client.PutAsJsonAsync($"/api/meetings/{represented}/ata", NewAta(assembly: false)));
        saved.GetProperty("status").GetString().Should().Be("draft");
        saved.GetProperty("kind").GetString().Should().Be("cv");
        var published = await AreaHttp.Json(await tuno.Client.PostAsync($"/api/meetings/{represented}/ata/publish", null));
        published.GetProperty("status").GetString().Should().Be("published");
        (await AreaHttp.Json(tuno.Client, $"/api/meetings/{represented}/ata")).GetProperty("status").GetString().Should().Be("published");
        (await _kit.StoredAtaAsync(represented))!.Status.Should().Be(MeetingAtaStatus.Published);

        (await tuno.Client.GetAsync($"/api/meetings/{assembly}/ata/edit")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await tuno.Client.PutAsJsonAsync($"/api/meetings/{assembly}/ata", NewAta(assembly: true))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await tuno.Client.PostAsync($"/api/meetings/{assembly}/ata/publish", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _kit.StoredAtaAsync(assembly)).Should().BeNull();
        (await tuno.Client.GetAsync("/api/meetings/requests")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------- Veterano / Tunossauro (by time) ----------

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    public async Task VeteranosAndTunossauros_SeeTheCvAndTheRequests_ButNotDirecao(int years)
    {
        var member = await _kit.MemberAsync(Who.TunoFor(years));
        var author = await _kit.MemberAsync(Who.Holding(Position.ViceMagister));
        var cv = await _kit.MeetingAsync(MeetingType.ConselhoVeteranos, 6);
        var assembly = await _kit.MeetingAsync(MeetingType.AssembleiaGeralExtraordinaria, 6);
        var direcao = await _kit.MeetingAsync(MeetingType.ReuniaoDirecao, 6);
        var direcaoRequest = await _kit.RequestAsync(MeetingType.ReuniaoDirecao, author.Id);

        var board = await BoardAsync(member.Client);
        BoardIds(board).Should().Contain(new[] { cv, assembly }).And.NotContain(direcao);
        board.GetProperty("access").GetProperty("canSeeRequests").GetBoolean().Should().BeTrue();
        board.GetProperty("access").GetProperty("canProposeCv").GetBoolean().Should().BeTrue();
        (await member.Client.GetAsync($"/api/meetings/{direcao}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // A4: the section shows every type (a Direção request included); deleting is for its author.
        var seen = RequestIn(await AllRequestsAsync(member.Client), direcaoRequest);
        seen.GetProperty("type").GetString().Should().Be(Direcao);
        seen.GetProperty("canDelete").GetBoolean().Should().BeFalse();
        seen.GetProperty("canDecide").GetBoolean().Should().BeFalse();
        (await member.Client.DeleteAsync($"/api/meetings/requests/{direcaoRequest}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var proposed = await CreatedIdAsync(await member.Client.PostAsJsonAsync("/api/meetings/requests", NewRequest(Cv)));
        var mine = RequestIn(await AllRequestsAsync(member.Client), proposed);
        mine.GetProperty("canDelete").GetBoolean().Should().BeTrue("the author deletes their own request");
        mine.GetProperty("canRemind").GetBoolean().Should().BeTrue();
        (await member.Client.PostAsJsonAsync("/api/meetings/requests", NewRequest(Direcao))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.Client.PostAsJsonAsync("/api/meetings/requests", NewRequest(Age))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.Client.DeleteAsync($"/api/meetings/requests/{proposed}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _kit.StoredRequestAsync(proposed)).Should().BeNull();
    }

    [Fact]
    public async Task TheVeteranoCategory_WithoutYearsAsTuno_IsNoVeterano()
    {
        var member = await _kit.MemberAsync(u => u.Categories = [MemberCategory.Tuno, MemberCategory.Veterano]);
        var cv = await _kit.MeetingAsync(MeetingType.ConselhoVeteranos, 6);

        BoardIds(await BoardAsync(member.Client)).Should().NotContain(cv, "Veterano is counted by time (YearTuno), not by category");
        (await member.Client.GetAsync("/api/meetings/requests")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.Client.PostAsJsonAsync("/api/meetings/requests", NewRequest(Cv))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
