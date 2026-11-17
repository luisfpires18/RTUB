// MeetingsPositionsApiTests: test class moved here from MeetingsApiTests.cs.
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
        access.GetProperty("canCreate").GetBoolean().Should().BeTrue("075: the Magister presides over the Direção");
        (await magister.Client.GetAsync("/api/meetings/requests")).StatusCode.Should().Be(HttpStatusCode.OK);

        foreach (var type in new[] { Cv, Age, Direcao })
        {
            (await magister.Client.PostAsJsonAsync("/api/meetings/requests", NewRequest(type))).StatusCode.Should().Be(HttpStatusCode.Created, type);
        }

        (await magister.Client.PostAsJsonAsync("/api/meetings/requests", NewRequest(Ago))).StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "nothing proposes an Assembleia Geral Ordinária");
        (await magister.Client.PostAsJsonAsync("/api/meetings", NewMeeting(Direcao))).StatusCode.Should().Be(HttpStatusCode.Created,
            "075: the Direção's president registers its meetings");
        (await magister.Client.PostAsJsonAsync("/api/meetings", NewMeeting(Cv))).StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "another body's type is not offered");

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
