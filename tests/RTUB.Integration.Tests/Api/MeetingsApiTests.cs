using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
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
using Xunit;
using static RTUB.Integration.Tests.Api.MeetingsKit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// /api/meetings (task 034, was the Blazor /meetings) through the real host - real login, antiforgery, SQLite and the old
/// meeting services - member type by member type: visitors, Leitões, Caloiros, Tunos, Veteranos / Tunossauros, Admin and
/// Owner (docs/react-meetings.md, "Permission matrix"). Push, email and document storage are recording fakes. Every test
/// seeds its own meetings and asserts by id, never by count: the database is shared by the class.
/// </summary>
public class MeetingsMembersApiTests : IClassFixture<MeetingsApiFactory>
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

    // ---------- Admin ----------

    [Fact]
    public async Task AnAdmin_AddsMembers_ButNeitherRemovesOthersAnswersNorWritesAtas()
    {
        var admin = await _kit.MemberAsync("Admin");
        var owner = await _kit.MemberAsync("Owner");
        var tuno = await _kit.MemberAsync(Who.Tuno);
        var leitao = await _kit.MemberAsync(Who.Leitao);
        var caloiro = await _kit.MemberAsync(Who.Caloiro);
        var upcoming = await _kit.MeetingAsync(MeetingType.AssembleiaGeralOrdinaria, 8);
        var past = await _kit.MeetingAsync(MeetingType.AssembleiaGeralOrdinaria, -8);

        (await BoardAsync(admin.Client)).GetProperty("access").GetProperty("canAddParticipants").GetBoolean().Should().BeTrue();

        async Task<List<string?>> Candidates(string? name) =>
            (await AreaHttp.Json(admin.Client, $"/api/meetings/{upcoming}/participants/candidates?q={Uri.EscapeDataString(name!)}"))
            .EnumerateArray().Select(c => c.GetProperty("id").GetString()).ToList();

        (await Candidates(tuno.User.Nickname)).Should().Equal(tuno.Id);
        (await Candidates(leitao.User.Nickname)).Should().BeEmpty("a Leitão is never added");
        (await Candidates(caloiro.User.Nickname)).Should().BeEmpty("nor a Caloiro");
        (await AreaHttp.Errors(await admin.Client.PostAsJsonAsync($"/api/meetings/{upcoming}/participants", new { userId = leitao.Id })))
            .Should().ContainKey("userId");

        var added = await AreaHttp.Json(await admin.Client.PostAsJsonAsync($"/api/meetings/{upcoming}/participants", new { userId = tuno.Id }));
        var row = added.GetProperty("going").EnumerateArray().Single(p => p.GetProperty("nickname").GetString() == tuno.User.Nickname);
        row.GetProperty("canRemove").GetBoolean().Should().BeFalse("Admin adds members but does not remove their answers");
        (await Candidates(tuno.User.Nickname)).Should().BeEmpty("they already answer");
        var participation = row.GetProperty("id").GetInt32();

        // Someone who already answered "Não vou" (with a reason) is neither offered nor overwritten.
        var away = await _kit.MemberAsync(Who.Tuno);
        var awayAnswer = await _kit.ParticipationAsync(upcoming, away.Id, willAttend: false, notes: "Estou fora nessa semana");
        (await Candidates(away.User.Nickname)).Should().BeEmpty("they already answered");
        (await AreaHttp.Errors(await admin.Client.PostAsJsonAsync($"/api/meetings/{upcoming}/participants", new { userId = away.Id })))
            .Should().ContainKey("userId");
        var kept = await _kit.StoredParticipationAsync(awayAnswer);
        kept!.WillAttend.Should().BeFalse("an existing answer is never turned into \"Vou\" from here");
        kept.Notes.Should().Be("Estou fora nessa semana");

        (await admin.Client.DeleteAsync($"/api/meetings/{upcoming}/participants/{participation}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await AreaHttp.Json(await owner.Client.DeleteAsync($"/api/meetings/{upcoming}/participants/{participation}")))
            .GetProperty("going").EnumerateArray().Select(p => p.GetProperty("id").GetInt32()).Should().NotContain(participation,
                "Owner removes anyone's answer");

        // A2: Admin alone never writes an ata; A6: nor manages meetings.
        (await admin.Client.GetAsync($"/api/meetings/{past}/ata/edit")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.Client.PutAsJsonAsync($"/api/meetings/{past}/ata", NewAta(assembly: true))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        Can(await CardAsync(admin.Client, past), "writeAta").Should().BeFalse();
        (await admin.Client.GetAsync("/api/meetings/form")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.Client.PutAsJsonAsync($"/api/meetings/{upcoming}", NewMeeting(Ago))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _kit.StoredAtaAsync(past)).Should().BeNull();
    }

    [Fact]
    public async Task AnAdmin_SeesAndProposesDirecao_OnlyWithTheTunoCategory()
    {
        var admin = await _kit.MemberAsync("Admin");
        var adminTuno = await _kit.MemberAsync("Admin", Who.TunoCategory);
        var direcao = await _kit.MeetingAsync(MeetingType.ReuniaoDirecao, 9);
        var cv = await _kit.MeetingAsync(MeetingType.ConselhoVeteranos, 9);

        BoardIds(await BoardAsync(admin.Client)).Should().NotContain(direcao).And.NotContain(cv);
        (await admin.Client.GetAsync($"/api/meetings/{direcao}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.Client.PostAsJsonAsync("/api/meetings/requests", NewRequest(Direcao))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var board = await BoardAsync(adminTuno.Client);
        BoardIds(board).Should().Contain(direcao).And.NotContain(cv);
        board.GetProperty("access").GetProperty("canProposeDirecao").GetBoolean().Should().BeTrue();
        (await adminTuno.Client.GetAsync($"/api/meetings/{direcao}")).StatusCode.Should().Be(HttpStatusCode.OK);
        var proposed = await CreatedIdAsync(await adminTuno.Client.PostAsJsonAsync("/api/meetings/requests", NewRequest(Direcao)));
        (await _kit.StoredRequestAsync(proposed))!.RequestedMeetingType.Should().Be(MeetingType.ReuniaoDirecao);
    }

    // ---------- Owner ----------

    [Fact]
    public async Task AnAnswer_IsRemovedOnlyThroughItsOwnMeeting()
    {
        var owner = await _kit.MemberAsync("Owner");
        var magister = await _kit.MemberAsync(Who.Veterano, Who.Holding(Position.Magister));
        var assembly = await _kit.MeetingAsync(MeetingType.AssembleiaGeralOrdinaria, 10);
        var direcao = await _kit.MeetingAsync(MeetingType.ReuniaoDirecao, 10);
        var answer = await _kit.ParticipationAsync(direcao, magister.Id, willAttend: true);

        // The Owner sees the assembly but not the Direção meeting: an answer of the second is not reachable through the first.
        (await owner.Client.DeleteAsync($"/api/meetings/{assembly}/participants/{answer}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.Client.DeleteAsync($"/api/meetings/{direcao}/participants/{answer}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await magister.Client.DeleteAsync($"/api/meetings/{assembly}/participants/{answer}")).StatusCode.Should().Be(HttpStatusCode.NotFound,
            "even your own answer goes through its own meeting");
        (await _kit.StoredParticipationAsync(answer)).Should().NotBeNull();
    }

    [Fact]
    public async Task EditingACv_KeepsItsSavedRepresentative_EvenOnceNoLongerAPlainTuno()
    {
        var owner = await _kit.MemberAsync("Owner", Who.Veterano);
        var formerTuno = await _kit.MemberAsync(Who.Veterano);
        var otherVeterano = await _kit.MemberAsync(Who.Veterano);
        var cv = await _kit.MeetingAsync(MeetingType.ConselhoVeteranos, 14, representativeId: formerTuno.Id);

        (await AreaHttp.Json(owner.Client, "/api/meetings/form")).GetProperty("tunoRepresentatives").EnumerateArray()
            .Select(t => t.GetProperty("id").GetString()).Should().NotContain(formerTuno.Id, "a Veterano is no longer offered");
        (await owner.Client.PutAsJsonAsync($"/api/meetings/{cv}", NewMeeting(Cv, title: "CV de outono", representativeId: formerTuno.Id)))
            .StatusCode.Should().Be(HttpStatusCode.OK, "the saved representative stays valid");
        (await _kit.StoredMeetingAsync(cv))!.TunoRepresentativeUserId.Should().Be(formerTuno.Id);
        (await AreaHttp.Errors(await owner.Client.PutAsJsonAsync($"/api/meetings/{cv}", NewMeeting(Cv, representativeId: otherVeterano.Id))))
            .Should().ContainKey("tunoRepresentativeId", "a new representative must be one the form offers");
    }

    [Fact]
    public async Task TheOwner_CreatesEveryType_ButManagesOnlyTheMeetingsItSees()
    {
        var owner = await _kit.MemberAsync("Owner");

        var form = await AreaHttp.Json(owner.Client, "/api/meetings/form");
        form.GetProperty("types").EnumerateArray().Select(t => t.GetProperty("value").GetString()).Should().Equal(Ago, Age, Cv, Direcao);

        foreach (var type in new[] { Ago, Age, Cv, Direcao })
        {
            var response = await owner.Client.PostAsJsonAsync("/api/meetings", NewMeeting(type));
            response.StatusCode.Should().Be(HttpStatusCode.Created, type);
            var saved = await response.Content.ReadFromJsonAsync<JsonElement>();
            (await _kit.StoredMeetingAsync(saved.GetProperty("id").GetInt32()))!.OrganizerUserId.Should().Be(owner.Id);
            if (type is Ago or Age)
            {
                Can(saved.GetProperty("card"), "manage").Should().BeTrue(type);
            }
            else
            {
                IsNull(saved, "card").Should().BeTrue("A3: Owner creates a {0} meeting it does not see", type);
            }
        }

        // A3: a CV meeting is not on the Owner's board, so it is not the Owner's to manage either.
        var cv = await _kit.MeetingAsync(MeetingType.ConselhoVeteranos, 12);
        BoardIds(await BoardAsync(owner.Client)).Should().NotContain(cv);
        (await owner.Client.GetAsync($"/api/meetings/{cv}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.Client.PutAsJsonAsync($"/api/meetings/{cv}", NewMeeting(Cv))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.Client.DeleteAsync($"/api/meetings/{cv}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.Client.PostAsJsonAsync($"/api/meetings/{cv}/cancel", new { reason = "Sala ocupada", notifyByEmail = false })).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        (await _kit.StoredMeetingAsync(cv))!.IsCancelled.Should().BeFalse();

        // An assembly it sees: edit, cancel, reactivate, delete.
        var ago = await _kit.MeetingAsync(MeetingType.AssembleiaGeralOrdinaria, 12);
        var edited = await AreaHttp.Json(await owner.Client.PutAsJsonAsync($"/api/meetings/{ago}", NewMeeting(Ago, title: "Assembleia de Primavera")));
        edited.GetProperty("card").GetProperty("title").GetString().Should().Be("Assembleia de Primavera");
        (await AreaHttp.Json(await owner.Client.PostAsJsonAsync($"/api/meetings/{ago}/cancel", new { reason = "Sala ocupada", notifyByEmail = false })))
            .GetProperty("sent").GetInt32().Should().Be(0);
        (await _kit.StoredMeetingAsync(ago))!.IsCancelled.Should().BeTrue();
        (await owner.Client.PostAsJsonAsync($"/api/meetings/{ago}/cancel", new { reason = "Outra vez", notifyByEmail = false })).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "it is already cancelled");
        (await AreaHttp.Json(await owner.Client.PostAsync($"/api/meetings/{ago}/uncancel", null))).GetProperty("cancelled").GetBoolean().Should().BeFalse();
        (await owner.Client.PostAsync($"/api/meetings/{ago}/uncancel", null)).StatusCode.Should().Be(HttpStatusCode.Conflict, "it is not cancelled");
        (await owner.Client.DeleteAsync($"/api/meetings/{ago}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await owner.Client.GetAsync($"/api/meetings/{ago}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TheOwner_GetsTheRequestsSection_OnlyAsAVeterano()
    {
        var owner = await _kit.MemberAsync("Owner");
        var veteranOwner = await _kit.MemberAsync("Owner", Who.Veterano);
        var author = await _kit.MemberAsync(Who.Veterano);
        var request = await _kit.RequestAsync(MeetingType.ConselhoVeteranos, author.Id);

        // A4: the requests gate comes first, for Owner too.
        (await owner.Client.GetAsync("/api/meetings/requests")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await owner.Client.PostAsync($"/api/meetings/requests/{request}/accept", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await owner.Client.DeleteAsync($"/api/meetings/requests/{request}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await BoardAsync(owner.Client)).GetProperty("access").GetProperty("canSeeRequests").GetBoolean().Should().BeFalse();

        var seen = RequestIn(await AllRequestsAsync(veteranOwner.Client), request);
        seen.GetProperty("canDecide").GetBoolean().Should().BeTrue("Owner decides any request it sees");
        seen.GetProperty("canDelete").GetBoolean().Should().BeTrue("and deletes any");
        seen.GetProperty("canRemind").GetBoolean().Should().BeTrue();
        (await veteranOwner.Client.PostAsync($"/api/meetings/requests/{request}/reminder", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await veteranOwner.Client.DeleteAsync($"/api/meetings/requests/{request}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _kit.StoredRequestAsync(request)).Should().BeNull();
    }
}

/// <summary>
/// /api/meetings, position by position: the Direção, the Mesa da Assembleia, the Conselho de Veteranos and the Conselho
/// Fiscal, and the secretaries named on an ata (docs/react-meetings.md, "Roles and positions"). Positions are looked up
/// globally (the old "holder"), so no test asserts which member holds one.
/// </summary>
public class MeetingsPositionsApiTests : IClassFixture<MeetingsApiFactory>
{
    private readonly MeetingsKit _kit;

    public MeetingsPositionsApiTests(MeetingsApiFactory factory)
    {
        _kit = new MeetingsKit(factory);
    }

    [Fact]
    public async Task TheMagister_SeesCvAndDirecao_ProposesThem_ReadsTheRequests_AndWritesDirecaoAtas()
    {
        var magister = await _kit.MemberAsync(Who.Holding(Position.Magister));
        var cv = await _kit.MeetingAsync(MeetingType.ConselhoVeteranos, 7);
        var direcao = await _kit.MeetingAsync(MeetingType.ReuniaoDirecao, -7);
        var assembly = await _kit.MeetingAsync(MeetingType.AssembleiaGeralOrdinaria, -7);

        var board = await BoardAsync(magister.Client);
        BoardIds(board).Should().Contain(new[] { cv, direcao, assembly });
        var access = board.GetProperty("access");
        access.GetProperty("canProposeCv").GetBoolean().Should().BeTrue();
        access.GetProperty("canProposeDirecao").GetBoolean().Should().BeTrue();
        access.GetProperty("canProposeAg").GetBoolean().Should().BeTrue();
        access.GetProperty("canSeeRequests").GetBoolean().Should().BeTrue();
        access.GetProperty("canCreate").GetBoolean().Should().BeFalse("A6: the Magister does not create meetings");
        (await magister.Client.GetAsync("/api/meetings/requests")).StatusCode.Should().Be(HttpStatusCode.OK);

        foreach (var type in new[] { Cv, Age, Direcao })
        {
            (await magister.Client.PostAsJsonAsync("/api/meetings/requests", NewRequest(type))).StatusCode.Should().Be(HttpStatusCode.Created, type);
        }

        (await magister.Client.PostAsJsonAsync("/api/meetings/requests", NewRequest(Ago))).StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "nothing proposes an Assembleia Geral Ordinária");
        (await magister.Client.PostAsJsonAsync("/api/meetings", NewMeeting(Direcao))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await magister.Client.GetAsync($"/api/meetings/{direcao}/ata/edit")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await AreaHttp.Json(await magister.Client.PutAsJsonAsync($"/api/meetings/{direcao}/ata", NewAta(assembly: false))))
            .GetProperty("kind").GetString().Should().Be("direcao");
        (await magister.Client.GetAsync($"/api/meetings/{assembly}/ata/edit")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(Position.ViceMagister)]
    [InlineData(Position.Secretario)]
    [InlineData(Position.PrimeiroTesoureiro)]
    [InlineData(Position.SegundoTesoureiro)]
    public async Task TheOtherDirecaoPositions_SeeAndProposeDirecao_ButNotTheRequestsNorTheCv(Position position)
    {
        var member = await _kit.MemberAsync(Who.Holding(position));
        var direcao = await _kit.MeetingAsync(MeetingType.ReuniaoDirecao, -3);
        var cv = await _kit.MeetingAsync(MeetingType.ConselhoVeteranos, 3);

        BoardIds(await BoardAsync(member.Client)).Should().Contain(direcao).And.NotContain(cv);
        (await member.Client.PostAsJsonAsync("/api/meetings/requests", NewRequest(Direcao))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await member.Client.PostAsJsonAsync("/api/meetings/requests", NewRequest(Cv))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.Client.PostAsJsonAsync("/api/meetings/requests", NewRequest(Age))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.Client.GetAsync("/api/meetings/requests")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "A4: Veterano or Magister only");
        (await member.Client.GetAsync($"/api/meetings/{direcao}/ata/edit")).StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "a Direção ata is the Magister's, or a secretary's once saved with them");
    }

    [Fact]
    public async Task ThePresidenteDaMesa_CreatesAndManagesAssemblies_AndNothingElse()
    {
        var president = await _kit.MemberAsync(Who.Holding(Position.PresidenteMesaAssembleia));

        var form = await AreaHttp.Json(president.Client, "/api/meetings/form");
        form.GetProperty("types").EnumerateArray().Select(t => t.GetProperty("value").GetString()).Should().Equal(Ago, Age);

        var created = await AreaHttp.Json(await president.Client.PostAsJsonAsync("/api/meetings", NewMeeting(Ago)));
        Can(created.GetProperty("card"), "manage").Should().BeTrue();
        Can(created.GetProperty("card"), "notify").Should().BeTrue();
        (await AreaHttp.Errors(await president.Client.PostAsJsonAsync("/api/meetings", NewMeeting(Cv)))).Should().ContainKey("type");
        (await AreaHttp.Errors(await president.Client.PostAsJsonAsync("/api/meetings", NewMeeting(Direcao)))).Should().ContainKey("type");

        var assembly = await _kit.MeetingAsync(MeetingType.AssembleiaGeralExtraordinaria, 9);
        (await president.Client.PutAsJsonAsync($"/api/meetings/{assembly}", NewMeeting(Age, title: "AGE de Outono"))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _kit.StoredMeetingAsync(assembly))!.Title.Should().Be("AGE de Outono");
        foreach (var draft in new[] { "cancel", "email", "push" })
        {
            (await president.Client.GetAsync($"/api/meetings/{assembly}/{draft}")).StatusCode.Should().Be(HttpStatusCode.OK, draft);
        }

        // Not a Veterano: the CV is not even seen.
        var cv = await _kit.MeetingAsync(MeetingType.ConselhoVeteranos, 9);
        (await president.Client.PutAsJsonAsync($"/api/meetings/{cv}", NewMeeting(Cv))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await president.Client.DeleteAsync($"/api/meetings/{cv}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await president.Client.GetAsync("/api/meetings/requests")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await president.Client.PostAsJsonAsync("/api/meetings/requests", NewRequest(Age))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var past = await _kit.MeetingAsync(MeetingType.AssembleiaGeralOrdinaria, -9);
        (await president.Client.GetAsync($"/api/meetings/{past}/ata/edit")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await AreaHttp.Json(await president.Client.PutAsJsonAsync($"/api/meetings/{past}/ata", NewAta(assembly: true))))
            .GetProperty("kind").GetString().Should().Be("ag");
        (await _kit.StoredAtaAsync(past))!.Status.Should().Be(MeetingAtaStatus.Draft);
    }

    [Fact]
    public async Task ThePresidenteDaMesa_AsAVeterano_DecidesAssemblyRequests_ButCannotManageACv()
    {
        var president = await _kit.MemberAsync(Who.Veterano, Who.Holding(Position.PresidenteMesaAssembleia));
        var author = await _kit.MemberAsync(Who.Holding(Position.PresidenteConselhoFiscal));
        var cv = await _kit.MeetingAsync(MeetingType.ConselhoVeteranos, 9);

        BoardIds(await BoardAsync(president.Client)).Should().Contain(cv);
        Can(await CardAsync(president.Client, cv), "manage").Should().BeFalse("the Mesa manages assemblies only");
        (await president.Client.PutAsJsonAsync($"/api/meetings/{cv}", NewMeeting(Cv))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await president.Client.DeleteAsync($"/api/meetings/{cv}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await president.Client.PostAsJsonAsync($"/api/meetings/{cv}/cancel", new { reason = "Não", notifyByEmail = false })).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await president.Client.GetAsync($"/api/meetings/{cv}/email")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var assemblyRequest = await _kit.RequestAsync(MeetingType.AssembleiaGeralExtraordinaria, author.Id, title: "Revisão dos estatutos");
        var councilRequest = await _kit.RequestAsync(MeetingType.ConselhoVeteranos, author.Id);
        (await president.Client.PostAsync($"/api/meetings/requests/{councilRequest}/accept", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await president.Client.PostAsync($"/api/meetings/requests/{councilRequest}/reject", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var draft = await AreaHttp.Json(await president.Client.PostAsync($"/api/meetings/requests/{assemblyRequest}/accept", null));
        draft.GetProperty("type").GetString().Should().Be(Age);
        draft.GetProperty("title").GetString().Should().Be("Revisão dos estatutos");
        draft.GetProperty("statement").GetString().Should().Be("Assuntos a tratar", "the create form opens prefilled with the request");
        (await _kit.StoredRequestAsync(assemblyRequest))!.Status.Should().Be(RequestStatus.Confirmed);
        (await president.Client.PostAsync($"/api/meetings/requests/{assemblyRequest}/reject", null)).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "only a pending request is decided");
        (await president.Client.DeleteAsync($"/api/meetings/requests/{assemblyRequest}")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden, "only its author or Owner deletes it");
    }

    [Fact]
    public async Task TheSecretariosDaMesa_WriteAnAssemblyAta_OnlyOnceSavedWithThem_AndFromTheNextDay()
    {
        var first = await _kit.MemberAsync(Who.Holding(Position.PrimeiroSecretarioMesaAssembleia));
        var second = await _kit.MemberAsync(Who.Holding(Position.SegundoSecretarioMesaAssembleia));
        var president = await _kit.MemberAsync(Who.Holding(Position.PresidenteMesaAssembleia));
        var past = await _kit.MeetingAsync(MeetingType.AssembleiaGeralOrdinaria, -5);
        var today = await _kit.MeetingAsync(MeetingType.AssembleiaGeralExtraordinaria, 0);

        foreach (var secretary in new[] { first, second })
        {
            (await secretary.Client.GetAsync($"/api/meetings/{past}/ata/edit")).StatusCode.Should().Be(HttpStatusCode.Forbidden,
                "the position alone does not write an ata that was never saved");
            (await secretary.Client.PutAsJsonAsync($"/api/meetings/{past}/ata", NewAta(assembly: true))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        await _kit.AtaAsync(past, president.Id, firstSecretaryId: first.Id, secondSecretaryId: second.Id);
        await _kit.AtaAsync(today, president.Id, firstSecretaryId: first.Id, secondSecretaryId: second.Id);

        (await AreaHttp.Json(first.Client, $"/api/meetings/{past}/ata/edit")).GetProperty("firstSecretaryId").GetString().Should().Be(first.Id);
        (await AreaHttp.Json(await first.Client.PutAsJsonAsync($"/api/meetings/{past}/ata", NewAta(assembly: true, first.Id, second.Id))))
            .GetProperty("status").GetString().Should().Be("draft");
        var stored = (await _kit.StoredAtaAsync(past))!;
        stored.FirstSecretaryUserId.Should().Be(first.Id);
        stored.SecondSecretaryUserId.Should().Be(second.Id);

        (await AreaHttp.Json(await second.Client.PostAsync($"/api/meetings/{past}/ata/publish", null)))
            .GetProperty("status").GetString().Should().Be("published");
        (await first.Client.PutAsJsonAsync($"/api/meetings/{past}/ata", NewAta(assembly: true, first.Id, second.Id))).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "a published ata is not edited");

        // On the meeting's day the old page did not know its ata yet: the secretaries write it from the next day.
        (await first.Client.GetAsync($"/api/meetings/{today}/ata/edit")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await second.Client.GetAsync($"/api/meetings/{today}/ata/edit")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await president.Client.GetAsync($"/api/meetings/{today}/ata/edit")).StatusCode.Should().Be(HttpStatusCode.OK,
            "the Presidente da Mesa writes it from the day itself");
    }

    [Fact]
    public async Task ThePresidenteDoCv_CreatesAndManagesCv_DecidesCvRequests_AndProposesAssemblies()
    {
        var president = await _kit.MemberAsync(Who.Veterano, Who.Holding(Position.PresidenteConselhoVeteranos));
        var tuno = await _kit.MemberAsync(Who.Tuno);
        var caloiro = await _kit.MemberAsync(Who.Caloiro);
        var author = await _kit.MemberAsync(Who.Tunossauro);

        var form = await AreaHttp.Json(president.Client, "/api/meetings/form");
        form.GetProperty("types").EnumerateArray().Select(t => t.GetProperty("value").GetString()).Should().Equal(Cv);
        var representatives = form.GetProperty("tunoRepresentatives").EnumerateArray().Select(t => t.GetProperty("id").GetString()).ToList();
        representatives.Should().Contain(tuno.Id).And.NotContain(caloiro.Id).And.NotContain(author.Id);

        (await AreaHttp.Errors(await president.Client.PostAsJsonAsync("/api/meetings", NewMeeting(Ago)))).Should().ContainKey("type");
        (await AreaHttp.Errors(await president.Client.PostAsJsonAsync("/api/meetings", NewMeeting(Cv, representativeId: caloiro.Id))))
            .Should().ContainKey("tunoRepresentativeId");

        var created = await AreaHttp.Json(await president.Client.PostAsJsonAsync("/api/meetings", NewMeeting(Cv, representativeId: tuno.Id)));
        var card = created.GetProperty("card");
        Can(card, "manage").Should().BeTrue();
        card.GetProperty("tunoRepresentativeId").GetString().Should().Be(tuno.Id, "a manager gets the representative's id for the form");
        var cv = created.GetProperty("id").GetInt32();

        // A5: the representative sees it, without the id.
        BoardIds(await BoardAsync(tuno.Client)).Should().Contain(cv);
        IsNull(await CardAsync(tuno.Client, cv), "tunoRepresentativeId").Should().BeTrue();

        (await president.Client.PutAsJsonAsync($"/api/meetings/{cv}", NewMeeting(Cv, title: "CV de Inverno", representativeId: tuno.Id))).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await _kit.StoredMeetingAsync(cv))!.TunoRepresentativeUserId.Should().Be(tuno.Id);
        var assembly = await _kit.MeetingAsync(MeetingType.AssembleiaGeralOrdinaria, 9);
        (await president.Client.PutAsJsonAsync($"/api/meetings/{assembly}", NewMeeting(Ago))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await president.Client.DeleteAsync($"/api/meetings/{assembly}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await president.Client.PostAsJsonAsync("/api/meetings/requests", NewRequest(Cv))).StatusCode
            .Should().Be(HttpStatusCode.Forbidden, "the Presidente do CV does not propose a CV");
        (await president.Client.PostAsJsonAsync("/api/meetings/requests", NewRequest(Age))).StatusCode.Should().Be(HttpStatusCode.Created);

        var councilRequest = await _kit.RequestAsync(MeetingType.ConselhoVeteranos, author.Id);
        var assemblyRequest = await _kit.RequestAsync(MeetingType.AssembleiaGeralExtraordinaria, author.Id);
        (await president.Client.PostAsync($"/api/meetings/requests/{assemblyRequest}/accept", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await AreaHttp.Json(await president.Client.PostAsync($"/api/meetings/requests/{councilRequest}/accept", null)))
            .GetProperty("type").GetString().Should().Be(Cv);
        (await _kit.StoredRequestAsync(councilRequest))!.Status.Should().Be(RequestStatus.Confirmed);
    }

    [Fact]
    public async Task ThePresidenteDoCf_ProposesAnAssembly_AndNothingElse()
    {
        var president = await _kit.MemberAsync(Who.Holding(Position.PresidenteConselhoFiscal));

        var proposed = await CreatedIdAsync(await president.Client.PostAsJsonAsync("/api/meetings/requests", NewRequest(Age)));
        var stored = (await _kit.StoredRequestAsync(proposed))!;
        stored.RequestedMeetingType.Should().Be(MeetingType.AssembleiaGeralExtraordinaria);
        stored.AuthorUserId.Should().Be(president.Id);
        stored.Status.Should().Be(RequestStatus.Pending);

        (await president.Client.PostAsJsonAsync("/api/meetings/requests", NewRequest(Cv))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await president.Client.PostAsJsonAsync("/api/meetings/requests", NewRequest(Direcao))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await president.Client.GetAsync("/api/meetings/requests")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await president.Client.PostAsJsonAsync("/api/meetings", NewMeeting(Ago))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ASecretaryNamedOnACvAta_WritesThatAta_AndOtherVeteranosDoNotSeeTheDraft()
    {
        var secretary = await _kit.MemberAsync(Who.Veterano);
        var other = await _kit.MemberAsync(Who.Veterano);
        var president = await _kit.MemberAsync(Who.Veterano, Who.Holding(Position.PresidenteConselhoVeteranos));
        var cv = await _kit.MeetingAsync(MeetingType.ConselhoVeteranos, -6);
        await _kit.ParticipationAsync(cv, secretary.Id, willAttend: true);

        (await secretary.Client.GetAsync($"/api/meetings/{cv}/ata/edit")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "not named yet");

        // The president writes it, with a secretary chosen among those who said "Vou".
        var editor = await AreaHttp.Json(president.Client, $"/api/meetings/{cv}/ata/edit");
        editor.GetProperty("firstSecretaryOptions").EnumerateArray().Select(o => o.GetProperty("id").GetString())
            .Should().Contain(secretary.Id).And.NotContain(other.Id);
        (await AreaHttp.Errors(await president.Client.PutAsJsonAsync($"/api/meetings/{cv}/ata", NewAta(assembly: false, firstSecretaryId: other.Id))))
            .Should().ContainKey("firstSecretaryId");
        (await AreaHttp.Json(await president.Client.PutAsJsonAsync($"/api/meetings/{cv}/ata", NewAta(assembly: false, firstSecretaryId: secretary.Id))))
            .GetProperty("firstSecretaryId").GetString().Should().Be(secretary.Id);

        (await secretary.Client.GetAsync($"/api/meetings/{cv}/ata/edit")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await AreaHttp.Json(await secretary.Client.PutAsJsonAsync($"/api/meetings/{cv}/ata", NewAta(assembly: false, firstSecretaryId: secretary.Id))))
            .GetProperty("status").GetString().Should().Be("draft");
        Can(await CardAsync(secretary.Client, cv), "writeAta").Should().BeTrue();

        (await other.Client.GetAsync($"/api/meetings/{cv}/ata/edit")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await other.Client.GetAsync($"/api/meetings/{cv}/ata")).StatusCode.Should().Be(HttpStatusCode.NotFound, "A1: a draft is for its writers");
        IsNull(await CardAsync(other.Client, cv), "ataStatus").Should().BeTrue();
    }
}

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

/// <summary>
/// /api/meetings, the page itself: antiforgery, caching, search, the fiscal-year filter, ordering, answers, server-side
/// validation and the old side effects (push, email, Documentação), all through the recording fakes.
/// </summary>
public class MeetingsBoardApiTests : IClassFixture<MeetingsApiFactory>
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

    // ---------- side effects ----------

    [Fact]
    public async Task CreatingAnAssembly_PushesEveryMember_ButNoLeitao()
    {
        var owner = await _kit.MemberAsync("Owner");
        var member = await _kit.MemberAsync(Who.Caloiro);
        var leitao = await _kit.MemberAsync(Who.Leitao);
        var title = $"Assembleia {Tag()}";

        await AreaHttp.Json(await owner.Client.PostAsJsonAsync("/api/meetings", NewMeeting(Ago, title: title)));

        _factory.Push.Verify(p => p.SendToUserAsync(member.Id, It.Is<SendPushNotificationDto>(n => n.Body.Contains(title) && n.Url!.EndsWith("/meetings"))),
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

/// <summary>034: the Blazor Meetings page and what only it used are gone; the menus link the React page.</summary>
public class MeetingsRetirementTests
{
    [Fact]
    public void TheBlazorPage_AndWhatOnlyItUsed_AreGone()
    {
        var src = Path.Combine(RepoRoot(), "src");
        foreach (var gone in new[]
                 {
                     "RTUB.Web/Pages/Activities/Meetings.razor", "RTUB.Shared/Components/Cards/MeetingCard.razor",
                     "RTUB.Shared/Components/Cards/MeetingRequestCard.razor", "RTUB.Shared/Components/Modals/MeetingParticipationModal.razor",
                     "RTUB.Web/wwwroot/css/3-components/meeting-card.css",
                 })
        {
            File.Exists(Path.Combine(src, gone)).Should().BeFalse("{0} was retired in 034", gone);
        }

        File.ReadAllText(Path.Combine(src, "RTUB.Web", "wwwroot", "css", "site.css")).Should().NotContain("meeting-card.css");

        // 4-pages/meetings.css stays: its .member-avatar-small still sizes the avatars of the /users dialogs. The page's own
        // grid went with the page.
        File.ReadAllText(Path.Combine(src, "RTUB.Web", "wwwroot", "css", "4-pages", "meetings.css")).Should().NotContain(".meeting-grid");
        File.ReadAllText(Path.Combine(src, "RTUB.Web", "Shared", "MainLayout.razor")).Should()
            .Contain("href=\"/meetings\" data-enhance-nav=\"false\"", "a full navigation leaves Blazor for the React page");

        typeof(RTUB.App).Assembly.GetType("RTUB.Pages.Activities.Meetings").Should().BeNull();
    }

    [Fact]
    public void TheReactPage_IsLinked_AndSaysNoMigration()
    {
        var portal = Path.Combine(RepoRoot(), "src", "RTUB.Web", "portal", "src");
        File.ReadAllText(Path.Combine(portal, "content.ts")).Should().Contain("meetings: '/meetings'");
        File.ReadAllText(Path.Combine(portal, "MemberShell.tsx")).Should().Contain("href: portal.meetings,").And.NotContain("blazor.meetings");
        File.ReadAllText(Path.Combine(portal, "main.tsx")).Should().Contain("'/meetings': lazy(");

        foreach (var file in new[] { "Meetings.tsx", "MeetingDialogs.tsx", "MeetingAta.tsx", "meetingsApi.ts" })
        {
            File.ReadAllText(Path.Combine(portal, file)).Should().NotContainAny(new[] { "migra", "Migra" }, file);
        }
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src", "RTUB.Web")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find the repository root");
    }
}

/// <summary>The host with push, email and document storage replaced by recording fakes: nothing reaches a device, a mailbox or R2.</summary>
public sealed class MeetingsApiFactory : TestWebApplicationFactory
{
    public Mock<IPushNotificationService> Push { get; } = new();
    public Mock<IEmailNotificationService> Email { get; } = new();
    public Mock<IDocumentStorageService> Documents { get; } = new();

    /// <summary>The members the fake push service reports as subscribed; tests add their own.</summary>
    public ConcurrentBag<string> Subscribed { get; } = new();

    public MeetingsApiFactory()
    {
        Push.Setup(p => p.GetSubscribedUserIdsAsync()).ReturnsAsync(() => Subscribed.ToArray().AsEnumerable());
        Email.Setup(e => e.SendMeetingNotificationAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(),
                It.IsAny<Dictionary<string, (string, string)>?>(), It.IsAny<IProgress<EmailSendProgress>?>()))
            .ReturnsAsync((true, 1, (string?)null));
        Documents.Setup(d => d.UploadDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync((string folder, string name, Stream _, string _) => folder + name);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPushNotificationService>();
            services.AddSingleton(Push.Object);
            services.RemoveAll<IEmailNotificationService>();
            services.AddSingleton(Email.Object);
            services.RemoveAll<IDocumentStorageService>();
            services.AddSingleton(Documents.Object);
        });
    }
}

/// <summary>A signed-in member, with the antiforgery header already set, and the account behind it.</summary>
internal sealed record Persona(HttpClient Client, ApplicationUser User)
{
    public string Id => User.Id;
}

/// <summary>
/// Who a test member is. Veterano / Tunossauro / Tuno are by time (CurrentRole, from YearTuno / MonthTuno), as the rules
/// read them; Leitão and Caloiro are categories; positions are the member's own.
/// </summary>
internal static class Who
{
    public static readonly Action<ApplicationUser> Leitao = u => u.Categories = [MemberCategory.Leitao];
    public static readonly Action<ApplicationUser> Caloiro = u => u.Categories = [MemberCategory.Caloiro];
    public static readonly Action<ApplicationUser> TunoCategory = u => u.Categories = [MemberCategory.Tuno];
    public static readonly Action<ApplicationUser> Tuno = TunoFor(1);
    public static readonly Action<ApplicationUser> Veterano = TunoFor(3);
    public static readonly Action<ApplicationUser> Tunossauro = TunoFor(7);

    /// <summary>The Tuno category, Tuno since this month <paramref name="years"/> years ago.</summary>
    public static Action<ApplicationUser> TunoFor(int years) => u =>
    {
        u.Categories = [MemberCategory.Tuno];
        u.YearTuno = DateTime.Now.Year - years;
        u.MonthTuno = DateTime.Now.Month;
    };

    public static Action<ApplicationUser> Holding(params Position[] positions) => u => u.Positions = positions.ToList();
}

/// <summary>Personas, seeded rows and the calls the /api/meetings tests share.</summary>
internal sealed class MeetingsKit
{
    public const string Ago = nameof(MeetingType.AssembleiaGeralOrdinaria);
    public const string Age = nameof(MeetingType.AssembleiaGeralExtraordinaria);
    public const string Cv = nameof(MeetingType.ConselhoVeteranos);
    public const string Direcao = nameof(MeetingType.ReuniaoDirecao);

    private static int _ip;
    private readonly MeetingsApiFactory _factory;

    public MeetingsKit(MeetingsApiFactory factory)
    {
        _factory = factory;
    }

    // ---------- personas ----------

    /// <summary>A new member signed in through the real login (own name and IP), then given categories, years and positions.</summary>
    public Task<Persona> MemberAsync(params Action<ApplicationUser>[] setup) => SignInAsync(null, setup);

    /// <summary>The same, with a role ("Owner", "Admin", ...) on the session.</summary>
    public Task<Persona> MemberAsync(string role, params Action<ApplicationUser>[] setup) => SignInAsync(role, setup);

    private async Task<Persona> SignInAsync(string? role, Action<ApplicationUser>[] setup)
    {
        var n = Interlocked.Increment(ref _ip);
        var (client, user) = await CookieTestSession.SignInAsync(_factory, $"mtg{Guid.NewGuid():N}"[..20], $"10.64.{n / 250}.{n % 250 + 1}", role);
        if (setup.Length > 0)
        {
            // The service reloads the member on every request, so changes after sign-in apply at once.
            using var scope = _factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var stored = await users.FindByIdAsync(user.Id) ?? throw new InvalidOperationException($"Member {user.Id} was not created.");
            foreach (var change in setup)
            {
                change(stored);
                change(user);
            }

            (await users.UpdateAsync(stored)).Succeeded.Should().BeTrue();
        }

        await AreaHttp.WithTokenAsync(client);
        return new Persona(client, user);
    }

    // ---------- seeded rows ----------

    /// <summary>A meeting at 21:00, <paramref name="days"/> from today (negative = past, 0 = today).</summary>
    public Task<int> MeetingAsync(MeetingType type, int days, string? title = null, string? statement = null,
        string? representativeId = null, bool cancelled = false) =>
        MeetingOnAsync(type, DateTime.Today.AddDays(days).AddHours(21), title, statement, representativeId, cancelled);

    public async Task<int> MeetingOnAsync(MeetingType type, DateTime date, string? title = null, string? statement = null,
        string? representativeId = null, bool cancelled = false)
    {
        var meeting = new Meeting
        {
            Type = type,
            Title = title ?? $"Reunião {Tag()}",
            Date = date,
            Location = "Sede da RTUB",
            Statement = statement ?? "Ordem de trabalhos",
            TunoRepresentativeUserId = representativeId,
            IsCancelled = cancelled,
            CancellationReason = cancelled ? "Motivo interno" : null,
        };
        await SaveAsync(meeting);
        return meeting.Id;
    }

    public async Task<int> AtaAsync(int meetingId, string presidentId, MeetingAtaStatus status = MeetingAtaStatus.Draft,
        string? firstSecretaryId = null, string? secondSecretaryId = null)
    {
        var ata = new MeetingAta
        {
            MeetingId = meetingId,
            AtaNumber = "1/2026",
            ActualStartTime = DateTime.Today.AddDays(-1).AddHours(21),
            Location = "Sede da RTUB",
            PresidentUserId = presidentId,
            FirstSecretaryUserId = firstSecretaryId,
            SecondSecretaryUserId = secondSecretaryId,
            QuorumBasis = "HoraAgendada",
            AttendeesPresent = "[]",
            AttendeesAbsent = "[]",
            Status = status,
            ClosingText = "Nada mais havendo a tratar, encerrou-se a sessão.",
            AgendaPoints = [new MeetingAtaAgendaPoint { PointNumber = 1, Title = "Aprovação das contas", VoteResult = "Aprovado" }],
        };
        await SaveAsync(ata);
        return ata.Id;
    }

    public async Task<int> ParticipationAsync(int meetingId, string userId, bool willAttend, string? notes = null)
    {
        var participation = new MeetingParticipation { MeetingId = meetingId, UserId = userId, WillAttend = willAttend, Notes = notes };
        await SaveAsync(participation);
        return participation.Id;
    }

    public async Task<int> RequestAsync(MeetingType type, string authorId, string? title = null, RequestStatus status = RequestStatus.Pending)
    {
        var request = new MeetingRequest
        {
            RequestedMeetingType = type,
            Title = title ?? $"Pedido {Tag()}",
            ProposedDateTime = DateTime.Today.AddDays(15).AddHours(21),
            Location = "Sede da RTUB",
            Description = "Assuntos a tratar",
            AuthorUserId = authorId,
            Status = status,
        };
        await SaveAsync(request);
        return request.Id;
    }

    public Task<Meeting?> StoredMeetingAsync(int id) =>
        WithDbAsync<Meeting?>(db => db.Meetings.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id));

    public Task<MeetingAta?> StoredAtaAsync(int meetingId) =>
        WithDbAsync<MeetingAta?>(db => db.MeetingAtas.AsNoTracking().FirstOrDefaultAsync(a => a.MeetingId == meetingId));

    public Task<MeetingParticipation?> StoredParticipationAsync(int id) =>
        WithDbAsync<MeetingParticipation?>(db => db.MeetingParticipations.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id));

    public Task<MeetingRequest?> StoredRequestAsync(int id) =>
        WithDbAsync<MeetingRequest?>(db => db.MeetingRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id));

    private Task<int> SaveAsync(object entity) => WithDbAsync(db =>
    {
        db.Add(entity);
        return db.SaveChangesAsync();
    });

    private async Task<T> WithDbAsync<T>(Func<ApplicationDbContext, Task<T>> work)
    {
        using var scope = _factory.Services.CreateScope();
        await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();
        return await work(db);
    }

    // ---------- reading answers ----------

    public static Task<JsonElement> BoardAsync(HttpClient client, string query = "fy=all") => AreaHttp.Json(client, $"/api/meetings?{query}");

    public static Task<JsonElement> CardAsync(HttpClient client, int id) => AreaHttp.Json(client, $"/api/meetings/{id}");

    /// <summary>The board's cards, upcoming then past.</summary>
    public static List<JsonElement> Cards(JsonElement board) =>
        board.GetProperty("upcoming").EnumerateArray().Concat(board.GetProperty("past").EnumerateArray()).ToList();

    public static List<int> BoardIds(JsonElement board) => Cards(board).Select(c => c.GetProperty("id").GetInt32()).ToList();

    public static List<int> Ids(JsonElement cards) => cards.EnumerateArray().Select(c => c.GetProperty("id").GetInt32()).ToList();

    public static List<string?> Names(JsonElement people) => people.EnumerateArray().Select(p => p.GetProperty("name").GetString()).ToList();

    /// <summary>One of the card's buttons, as the server decided it.</summary>
    public static bool Can(JsonElement card, string action) => card.GetProperty("can").GetProperty(action).GetBoolean();

    public static bool IsNull(JsonElement element, string property) =>
        !element.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null;

    public static async Task<int> CreatedIdAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    /// <summary>Every page of the requests section (all statuses, every year).</summary>
    public static async Task<List<JsonElement>> AllRequestsAsync(HttpClient client)
    {
        var all = new List<JsonElement>();
        for (var page = 1; page <= 100; page++)
        {
            var result = await AreaHttp.Json(client, $"/api/meetings/requests?fy=all&pageSize=20&page={page}");
            var items = result.GetProperty("items").EnumerateArray().ToList();
            all.AddRange(items);
            if (items.Count == 0 || all.Count >= result.GetProperty("total").GetInt32())
            {
                break;
            }
        }

        return all;
    }

    public static JsonElement RequestIn(List<JsonElement> requests, int id) => requests.Single(r => r.GetProperty("id").GetInt32() == id);

    // ---------- bodies ----------

    public static string Tag() => Guid.NewGuid().ToString("N")[..12];

    /// <summary>A form date-time ("yyyy-MM-ddTHH:mm"), <paramref name="days"/> from today.</summary>
    public static string Date(int days, int hour = 21) =>
        DateTime.Today.AddDays(days).AddHours(hour).ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);

    public static object NewMeeting(string type, string? title = null, int days = 30, string? statement = "Ordem de trabalhos",
        string? location = "Sede da RTUB", string? representativeId = null) => new
        {
            type,
            title = title ?? $"Reunião {Tag()}",
            date = Date(days),
            location,
            statement,
            tunoRepresentativeId = representativeId,
        };

    public static object NewRequest(string type, string? title = null, string? description = "Assuntos a tratar") => new
    {
        type,
        title = title ?? $"Pedido {Tag()}",
        proposedDate = Date(15),
        location = "Sede da RTUB",
        description,
    };

    public static object NewAta(bool assembly, string? firstSecretaryId = null, string? secondSecretaryId = null) => new
    {
        ataNumber = "1/2026",
        actualStartTime = Date(-1, 21),
        actualEndTime = Date(-1, 23),
        location = "Sede da RTUB",
        quorumBasis = assembly ? "HoraAgendada" : null,
        firstSecretaryId,
        secondSecretaryId,
        agendaPoints = new[]
        {
            new { title = "Aprovação das contas", discussion = "Contas do ano.", decision = "Aprovadas.", votesFor = 12, votesAgainst = 0, votesAbstain = 1, result = "Aprovado" },
        },
        closingText = "Nada mais havendo a tratar, encerrou-se a sessão.",
    };

    // ---------- every call ----------

    /// <summary>Every GET, with the status it answered; each must say Cache-Control: no-store.</summary>
    public static async Task<Dictionary<string, HttpStatusCode>> ReadsAsync(HttpClient client, int id)
    {
        var statuses = new Dictionary<string, HttpStatusCode>();
        foreach (var path in new[]
                 {
                     "/api/meetings", "/api/meetings?fy=all", $"/api/meetings/{id}", "/api/meetings/form", $"/api/meetings/{id}/cancel",
                     $"/api/meetings/{id}/email", $"/api/meetings/{id}/push", $"/api/meetings/{id}/participants",
                     $"/api/meetings/{id}/participants/candidates?q=a", $"/api/meetings/{id}/ata", $"/api/meetings/{id}/ata/edit",
                     $"/api/meetings/{id}/ata/pdf", "/api/meetings/requests",
                 })
        {
            var response = await client.GetAsync(path);
            response.Headers.CacheControl!.NoStore.Should().BeTrue("{0} is never cached", path);
            statuses[path] = response.StatusCode;
        }

        return statuses;
    }

    /// <summary>Every write on one meeting, each with a well-formed body.</summary>
    public static Task<Dictionary<string, HttpStatusCode>> MeetingWritesAsync(HttpClient client, int id) =>
        SendAllAsync(new (string, Func<Task<HttpResponseMessage>>)[]
        {
            ($"PUT /{id}", () => client.PutAsJsonAsync($"/api/meetings/{id}", NewMeeting(Ago))),
            ($"DELETE /{id}", () => client.DeleteAsync($"/api/meetings/{id}")),
            ($"POST /{id}/cancel", () => client.PostAsJsonAsync($"/api/meetings/{id}/cancel", new { reason = "Motivo", notifyByEmail = false })),
            ($"POST /{id}/uncancel", () => client.PostAsync($"/api/meetings/{id}/uncancel", null)),
            ($"POST /{id}/email/preview", () => client.PostAsJsonAsync($"/api/meetings/{id}/email/preview", new { body = "Corpo" })),
            ($"POST /{id}/email", () => client.PostAsJsonAsync($"/api/meetings/{id}/email", new { subject = "Assunto", body = "Corpo" })),
            ($"POST /{id}/push", () => client.PostAsJsonAsync($"/api/meetings/{id}/push", new { message = "Mensagem" })),
            ($"PUT /{id}/participation", () => client.PutAsJsonAsync($"/api/meetings/{id}/participation", new { willAttend = true })),
            ($"POST /{id}/participants", () => client.PostAsJsonAsync($"/api/meetings/{id}/participants", new { userId = "ninguem" })),
            ($"DELETE /{id}/participants/1", () => client.DeleteAsync($"/api/meetings/{id}/participants/1")),
            ($"PUT /{id}/ata", () => client.PutAsJsonAsync($"/api/meetings/{id}/ata", NewAta(assembly: true))),
            ($"POST /{id}/ata/publish", () => client.PostAsync($"/api/meetings/{id}/ata/publish", null)),
            ($"POST /{id}/ata/confirmation", () => client.PostAsJsonAsync($"/api/meetings/{id}/ata/confirmation", new { confirm = true })),
        });

    /// <summary>Creating a meeting, proposing one, and every write on one request.</summary>
    public static Task<Dictionary<string, HttpStatusCode>> OtherWritesAsync(HttpClient client, int requestId) =>
        SendAllAsync(new (string, Func<Task<HttpResponseMessage>>)[]
        {
            ("POST /", () => client.PostAsJsonAsync("/api/meetings", NewMeeting(Ago))),
            ("POST /requests", () => client.PostAsJsonAsync("/api/meetings/requests", NewRequest(Cv))),
            ($"POST /requests/{requestId}/accept", () => client.PostAsync($"/api/meetings/requests/{requestId}/accept", null)),
            ($"POST /requests/{requestId}/reject", () => client.PostAsync($"/api/meetings/requests/{requestId}/reject", null)),
            ($"DELETE /requests/{requestId}", () => client.DeleteAsync($"/api/meetings/requests/{requestId}")),
            ($"POST /requests/{requestId}/reminder", () => client.PostAsync($"/api/meetings/requests/{requestId}/reminder", null)),
        });

    /// <summary>Every call answered <paramref name="expected"/>; the message names the ones that did not.</summary>
    public static void AllAnswer(Dictionary<string, HttpStatusCode> statuses, HttpStatusCode expected)
    {
        statuses.Should().NotBeEmpty();
        statuses.Where(s => s.Value != expected).Select(s => $"{s.Key} answered {(int)s.Value}")
            .Should().BeEmpty("every call answers {0}", (int)expected);
    }

    private static async Task<Dictionary<string, HttpStatusCode>> SendAllAsync((string Name, Func<Task<HttpResponseMessage>> Send)[] calls)
    {
        var statuses = new Dictionary<string, HttpStatusCode>();
        foreach (var (name, send) in calls)
        {
            statuses[name] = (await send()).StatusCode;
        }

        return statuses;
    }
}
