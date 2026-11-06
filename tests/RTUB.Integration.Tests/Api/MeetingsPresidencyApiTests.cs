using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Core.Enums;
using Xunit;
using static RTUB.Integration.Tests.Api.MeetingsKit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// Task 075 (#256, the owner's matrix): each body's CURRENT president creates and manages that body's meetings and writes
/// its atas - Presidente da Mesa: AG / AGE; Magister (the Direção's president): Direção; Presidente do CV: CV - and Owner
/// every type; Admin alone none; several presidencies = the union; an ended mandate = nothing. A meeting registered after
/// it happened is not pushed.
/// </summary>
public class MeetingsPresidencyApiTests : IClassFixture<MeetingsApiFactory>
{
    private readonly MeetingsApiFactory _factory;
    private readonly MeetingsKit _kit;

    public MeetingsPresidencyApiTests(MeetingsApiFactory factory)
    {
        _factory = factory;
        _kit = new MeetingsKit(factory);
    }

    public static TheoryData<string> Presidents => new() { "mesa", "magister", "cv" };

    private static Position PositionOf(string who) => who switch
    {
        "mesa" => Position.PresidenteMesaAssembleia,
        "magister" => Position.Magister,
        _ => Position.PresidenteConselhoVeteranos,
    };

    private static MeetingType[] OwnBody(string who) => who switch
    {
        "mesa" => [MeetingType.AssembleiaGeralOrdinaria, MeetingType.AssembleiaGeralExtraordinaria],
        "magister" => [MeetingType.ReuniaoDirecao],
        _ => [MeetingType.ConselhoVeteranos],
    };

    [Theory]
    [MemberData(nameof(Presidents))]
    public async Task EachPresident_WritesOnlyTheirBodysAtas_AndManagesOnlyTheirBodysMeetings(string who)
    {
        // A Veterano, so CV meetings are visible to all three: the refusals below are the ata rule, not visibility.
        var president = await _kit.MemberAsync(Who.Veterano, Who.Holding(PositionOf(who)));

        foreach (var type in Enum.GetValues<MeetingType>())
        {
            var past = await _kit.MeetingAsync(type, -5);
            var own = OwnBody(who).Contains(type);
            var assembly = type is MeetingType.AssembleiaGeralOrdinaria or MeetingType.AssembleiaGeralExtraordinaria;

            var editor = await president.Client.GetAsync($"/api/meetings/{past}/ata/edit");
            var save = await president.Client.PutAsJsonAsync($"/api/meetings/{past}/ata", NewAta(assembly));
            var create = await president.Client.PostAsJsonAsync("/api/meetings", NewMeeting(type.ToString()));
            if (own)
            {
                editor.StatusCode.Should().Be(HttpStatusCode.OK, "{0} writes {1} atas", who, type);
                save.StatusCode.Should().Be(HttpStatusCode.OK, "{0} saves a {1} ata draft", who, type);
                create.StatusCode.Should().Be(HttpStatusCode.Created, "{0} registers {1} meetings", who, type);
                (await president.Client.GetAsync($"/api/meetings/{past}/ata")).StatusCode.Should().Be(HttpStatusCode.OK, "the draft reads back");
            }
            else
            {
                editor.StatusCode.Should().NotBe(HttpStatusCode.OK, "{0} does not write {1} atas", who, type);
                save.StatusCode.Should().NotBe(HttpStatusCode.OK, "{0} does not save a {1} ata (API tampering)", who, type);
                create.StatusCode.Should().NotBe(HttpStatusCode.Created, "{0} does not register {1} meetings", who, type);
            }
        }
    }

    [Fact]
    public async Task TheOwner_WritesEveryBodysAtas_AndAdminAloneNone()
    {
        var owner = await _kit.MemberAsync("Owner", Who.Tuno);
        var admin = await _kit.MemberAsync("Admin", Who.Veterano);

        foreach (var type in Enum.GetValues<MeetingType>())
        {
            var past = await _kit.MeetingAsync(type, -4);
            var assembly = type is MeetingType.AssembleiaGeralOrdinaria or MeetingType.AssembleiaGeralExtraordinaria;
            (await owner.Client.PutAsJsonAsync($"/api/meetings/{past}/ata", NewAta(assembly))).StatusCode.Should().Be(HttpStatusCode.OK,
                "the Owner writes {0} atas (even a CV / Direção the Owner would not see before 075)", type);
            (await admin.Client.PutAsJsonAsync($"/api/meetings/{past}/ata", NewAta(assembly))).StatusCode.Should().NotBe(HttpStatusCode.OK,
                "Admin alone writes no {0} ata", type);
        }
    }

    [Fact]
    public async Task SeveralPresidencies_AreTheUnion_AndAnEndedMandateLosesThemAll()
    {
        var both = await _kit.MemberAsync(Who.Veterano, Who.Holding(Position.PresidenteMesaAssembleia, Position.PresidenteConselhoVeteranos));
        var ag = await _kit.MeetingAsync(MeetingType.AssembleiaGeralOrdinaria, -3);
        var cv = await _kit.MeetingAsync(MeetingType.ConselhoVeteranos, -3);
        var direcao = await _kit.MeetingAsync(MeetingType.ReuniaoDirecao, -3);

        (await both.Client.PutAsJsonAsync($"/api/meetings/{ag}/ata", NewAta(true))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await both.Client.PutAsJsonAsync($"/api/meetings/{cv}/ata", NewAta(false))).StatusCode.Should().Be(HttpStatusCode.OK,
            "no first-match: the Mesa does not hide the CV");
        (await both.Client.PutAsJsonAsync($"/api/meetings/{direcao}/ata", NewAta(false))).StatusCode.Should().NotBe(HttpStatusCode.OK);
        var form = await AreaHttp.Json(both.Client, "/api/meetings/form");
        form.GetProperty("types").EnumerateArray().Select(t => t.GetProperty("value").GetString()).Should().Equal(Ago, Age, Cv);

        await SetPositionsAsync(both.Id, []);
        (await both.Client.PutAsJsonAsync($"/api/meetings/{ag}/ata", NewAta(true))).StatusCode.Should().NotBe(HttpStatusCode.OK, "the mandate ended");
        (await both.Client.PostAsJsonAsync("/api/meetings", NewMeeting(Ago))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AMeetingRegisteredAfterItHappened_IsNotAnnounced()
    {
        var magister = await _kit.MemberAsync(Who.Tuno, Who.Holding(Position.Magister));
        var title = $"Direção esquecida {Guid.NewGuid():N}"[..30];
        var upcomingTitle = $"Direção futura {Guid.NewGuid():N}"[..30];

        (await magister.Client.PostAsJsonAsync("/api/meetings", NewMeeting(Direcao, title: title, days: -10))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await magister.Client.PostAsJsonAsync("/api/meetings", NewMeeting(Direcao, title: upcomingTitle, days: 10))).StatusCode.Should().Be(HttpStatusCode.Created);

        _factory.Push.Verify(p => p.SendToUserAsync(It.IsAny<string>(), It.Is<SendPushNotificationDto>(n => n.Body.Contains(title))), Times.Never);
        _factory.Push.Verify(p => p.SendToUserAsync(It.IsAny<string>(), It.Is<SendPushNotificationDto>(n => n.Body.Contains(upcomingTitle))), Times.AtLeastOnce,
            "an upcoming meeting is still announced");
    }

    private async Task SetPositionsAsync(string userId, List<Position> positions)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Users.SingleAsync(u => u.Id == userId)).Positions = positions;
        await db.SaveChangesAsync();
    }
}
