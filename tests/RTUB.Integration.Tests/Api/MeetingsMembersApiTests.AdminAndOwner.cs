// MeetingsMembersApiTests tests: admins and the Owner manage the members, answers and CVs.
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
public partial class MeetingsMembersApiTests
{
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

        // An answer of the Direção meeting is not reachable through the assembly. Task 075: the Owner sees every type, so it
        // removes the answer through its own meeting.
        (await owner.Client.DeleteAsync($"/api/meetings/{assembly}/participants/{answer}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await magister.Client.DeleteAsync($"/api/meetings/{assembly}/participants/{answer}")).StatusCode.Should().Be(HttpStatusCode.NotFound,
            "even your own answer goes through its own meeting");
        (await _kit.StoredParticipationAsync(answer)).Should().NotBeNull();
        (await owner.Client.DeleteAsync($"/api/meetings/{direcao}/participants/{answer}")).StatusCode.Should().Be(HttpStatusCode.OK);
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
    public async Task TheOwner_CreatesSeesAndManagesEveryType()
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
            Can(saved.GetProperty("card"), "manage").Should().BeTrue("075 (supersedes A3): the Owner sees and manages {0}", type);
        }

        // Task 075 (supersedes A3): a CV meeting is on the Owner's board and the Owner's to manage.
        var cv = await _kit.MeetingAsync(MeetingType.ConselhoVeteranos, 12);
        BoardIds(await BoardAsync(owner.Client)).Should().Contain(cv);
        (await owner.Client.GetAsync($"/api/meetings/{cv}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await owner.Client.PutAsJsonAsync($"/api/meetings/{cv}", NewMeeting(Cv))).StatusCode.Should().Be(HttpStatusCode.OK);

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
