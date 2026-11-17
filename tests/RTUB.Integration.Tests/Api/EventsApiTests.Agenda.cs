// EventsApiTests tests: public agenda, event view and visibility.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using Xunit;

namespace RTUB.Integration.Tests.Api;
public partial class EventsApiTests
{
    // ---------- reading ----------

    [Fact]
    public async Task AVisitor_GetsThePublicAgenda_AndNothingPrivate()
    {
        var seed = await SeedAsync();

        var response = await Anonymous().GetAsync("/api/events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue("the answer depends on the session");
        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain(Description).And.NotContain(Reason).And.NotContain(MemberNote)
            .And.NotContain(seed.MemberId, "no user id ever reaches a visitor")
            .And.NotContainAny(new[] { "userId", "email", "notes", "enrollments", "createdBy", "cancellationReason", "\"description\"" });

        var body = JsonDocument.Parse(raw).RootElement;
        body.GetProperty("isMember").GetBoolean().Should().BeFalse();
        var all = body.GetProperty("upcoming").EnumerateArray().Concat(body.GetProperty("past").EnumerateArray()).ToList();
        all.Should().OnlyContain(e => e.GetProperty("member").ValueKind == JsonValueKind.Null);

        var festival = all.Single(e => e.GetProperty("id").GetInt32() == seed.Festival);
        festival.GetProperty("trophies").EnumerateArray().Select(t => t.GetString()).Should().Equal("1.º Prémio", "Melhor Pandeireta");
        festival.GetProperty("videoCount").GetInt32().Should().Be(1);
        all.Single(e => e.GetProperty("id").GetInt32() == seed.Cancelled).GetProperty("cancelled").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task TheAgenda_SplitsUpcomingAndPast_InTheirOrders()
    {
        var seed = await SeedAsync();

        var body = await Anonymous().GetFromJsonAsync<JsonElement>("/api/events");

        var upcoming = Ids(body.GetProperty("upcoming"));
        upcoming.Should().ContainInOrder(seed.MultiDay, seed.Open, seed.Cancelled);
        upcoming.Should().NotContain(new[] { seed.Festival, seed.Past });
        Ids(body.GetProperty("past")).Should().ContainInOrder(seed.Past, seed.Festival);
    }

    [Fact]
    public async Task OneEvent_ForAVisitor_HasItsVideosButNoMemberSection()
    {
        var seed = await SeedAsync();

        var response = await Anonymous().GetAsync($"/api/events/{seed.Festival}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        body.GetProperty("member").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("videos").EnumerateArray().Single().GetProperty("url").GetString().Should().StartWith("https://pub-test.r2.dev/");
    }

    [Fact]
    public async Task AnUnknownEvent_Is404_ForEveryone()
    {
        await SeedAsync();
        var (member, _) = await SignInAsync();

        (await Anonymous().GetAsync("/api/events/987654")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await member.GetAsync("/api/events/987654")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await member.GetAsync("/api/events/987654/enrollment")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ASignedInMember_GetsTheMemberView_WithTheReasonTheirOwnAnswerAndWhoIsGoing()
    {
        var seed = await SeedAsync();
        var (member, _) = await SignInAsync();

        var cancelled = await member.GetFromJsonAsync<JsonElement>($"/api/events/{seed.Cancelled}");
        var open = await member.GetFromJsonAsync<JsonElement>($"/api/events/{seed.Open}");

        cancelled.GetProperty("isMember").GetBoolean().Should().BeTrue();
        cancelled.GetProperty("member").GetProperty("cancellationReason").GetString().Should().Be(Reason);
        open.GetProperty("event").GetProperty("member").GetProperty("description").GetString().Should().Be(Description);
        open.GetProperty("event").GetProperty("member").GetProperty("myStatus").ValueKind.Should().Be(JsonValueKind.Null,
            "someone else's answer is never this member's");
        open.GetProperty("event").GetProperty("member").GetProperty("goingCount").GetInt32().Should().Be(1);
        var going = open.GetProperty("member").GetProperty("participants").GetProperty("going").EnumerateArray().ToList();
        going.Should().ContainSingle(p => p.GetProperty("notes").GetString() == MemberNote,
            "members see who is going and their notes on the event page, as the members' list always showed");
        open.ToString().Should().NotContain(seed.MemberId, "no user id leaves the server").And.NotContain("@test.com", "nor an email");
    }

    // ---------- members-only events (#227) ----------

    [Fact]
    public async Task AMembersOnlyEvent_DoesNotExist_ForAVisitor_InTheAgendaArchiveHomeDetailOrVideoPlays()
    {
        var publicId = await AddEventAsync("Arraial público #227", DateTime.Today.AddDays(2).AddHours(21));
        var surpriseId = await AddEventAsync("Serenata surpresa #227", DateTime.Today.AddDays(1).AddHours(21), isPublic: false);
        var pastSurpriseId = await AddEventAsync("Surpresa antiga #227", DateTime.Today.AddDays(-5), isPublic: false);
        var surpriseVideo = await AddEventVideoAsync(pastSurpriseId, "Vídeo da surpresa", 1);
        var visitor = Anonymous();
        await WithTokenAsync(visitor);

        var agenda = await visitor.GetAsync("/api/events");
        var raw = await agenda.Content.ReadAsStringAsync();
        raw.Should().Contain("Arraial público #227").And.NotContain("Serenata surpresa #227", "the upcoming list never names it")
            .And.NotContain("Surpresa antiga #227", "nor does the archive").And.NotContain("\"membersOnly\":true");
        var body = JsonDocument.Parse(raw).RootElement;
        Ids(body.GetProperty("upcoming")).Should().Contain(publicId).And.NotContain(surpriseId);
        Ids(body.GetProperty("past")).Should().NotContain(pastSurpriseId);

        (await visitor.GetAsync($"/api/events/{publicId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await visitor.GetAsync($"/api/events/{surpriseId}");
        detail.StatusCode.Should().Be(HttpStatusCode.NotFound, "a members-only event answers exactly like one that does not exist");
        (await detail.Content.ReadAsStringAsync()).Should().NotContain("Serenata surpresa #227");
        (await visitor.GetAsync($"/api/events/{pastSurpriseId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await visitor.PostAsync($"/api/events/videos/{surpriseVideo}/plays", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await visitor.GetStringAsync("/api/public/events/upcoming")).Should().NotContain("Serenata surpresa #227");
        var (member, _) = await SignInAsync();
        (await member.GetStringAsync("/api/public/events/upcoming")).Should().NotContain("Serenata surpresa #227",
            "the home preview is the public preview whoever asks");
    }

    [Fact]
    public async Task ASignedInMember_SeesAMembersOnlyEvent_MarkedAsSuch_AndCanAnswerIt()
    {
        var surpriseId = await AddEventAsync("Serenata secreta #227", DateTime.Today.AddDays(3).AddHours(21), isPublic: false);
        var publicId = await AddEventAsync("Arraial aberto #227", DateTime.Today.AddDays(4).AddHours(21));
        var (member, user) = await SignInAsync();
        await WithTokenAsync(member);

        var agenda = await member.GetFromJsonAsync<JsonElement>("/api/events");
        var upcoming = agenda.GetProperty("upcoming").EnumerateArray().ToList();
        upcoming.Single(e => e.GetProperty("id").GetInt32() == surpriseId).GetProperty("membersOnly").GetBoolean().Should().BeTrue();
        upcoming.Single(e => e.GetProperty("id").GetInt32() == publicId).GetProperty("membersOnly").GetBoolean().Should().BeFalse();

        var detail = await member.GetFromJsonAsync<JsonElement>($"/api/events/{surpriseId}");
        detail.GetProperty("event").GetProperty("membersOnly").GetBoolean().Should().BeTrue();
        detail.GetProperty("member").ValueKind.Should().Be(JsonValueKind.Object);

        (await member.PutAsJsonAsync($"/api/events/{surpriseId}/enrollment", Answer(true))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await EnrollmentAsync(surpriseId, user.Id))!.WillAttend.Should().BeTrue();
    }

    [Fact]
    public async Task AnAdmin_CreatesAMembersOnlyEvent_AndCanMakeItPublicLater()
    {
        var (admin, _) = await SignInAsync("Admin");
        await WithTokenAsync(admin);
        var visitor = Anonymous();

        var created = await admin.PostAsJsonAsync("/api/events", NewEvent("Surpresa do admin #227", membersOnly: true));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var summary = await created.Content.ReadFromJsonAsync<JsonElement>();
        summary.GetProperty("membersOnly").GetBoolean().Should().BeTrue();
        var id = summary.GetProperty("id").GetInt32();
        (await EventByIdAsync(id)).IsPublic.Should().BeFalse();
        (await admin.GetFromJsonAsync<JsonElement>($"/api/events/{id}/edit")).GetProperty("membersOnly").GetBoolean().Should().BeTrue();
        (await visitor.GetAsync($"/api/events/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await admin.PutAsJsonAsync($"/api/events/{id}", NewEvent("Surpresa do admin #227", membersOnly: false))).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await EventByIdAsync(id)).IsPublic.Should().BeTrue();
        (await visitor.GetAsync($"/api/events/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await admin.PutAsJsonAsync($"/api/events/{id}", NewEvent("Surpresa do admin #227", membersOnly: true))).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await visitor.GetAsync($"/api/events/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound, "editing can hide it again");
    }

    [Fact]
    public async Task ANewEvent_IsPublic_WhenPrivacyIsNotChosen()
    {
        var (admin, _) = await SignInAsync("Admin");
        await WithTokenAsync(admin);

        var created = await admin.PostAsJsonAsync("/api/events", NewEvent("Sem escolha de privacidade #227"));

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var summary = await created.Content.ReadFromJsonAsync<JsonElement>();
        summary.GetProperty("membersOnly").GetBoolean().Should().BeFalse();
        (await EventByIdAsync(summary.GetProperty("id").GetInt32())).IsPublic.Should().BeTrue();
        (await Anonymous().GetAsync($"/api/events/{summary.GetProperty("id").GetInt32()}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // Regression (#227 follow-up): an update that omits membersOnly must never declassify a private event.
    [Fact]
    public async Task Visibility_OnCreateDefaultsToPublic_AndOnUpdateChangesOnlyWhenSentExplicitly()
    {
        var (admin, _) = await SignInAsync("Admin");
        await WithTokenAsync(admin);
        var visitor = Anonymous();

        async Task<int> CreateAsync(object body)
        {
            var response = await admin.PostAsJsonAsync("/api/events", body);
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        }

        async Task UpdateAsync(int id, object body) =>
            (await admin.PutAsJsonAsync($"/api/events/{id}", body)).StatusCode.Should().Be(HttpStatusCode.OK);

        // 1. Create without membersOnly -> public.
        var omitted = await CreateAsync(NewEvent("Criada sem campo #227-fu"));
        (await EventByIdAsync(omitted)).IsPublic.Should().BeTrue();

        // 2. Private event updated without membersOnly -> stays private.
        var secret = await CreateAsync(NewEvent("Privada a editar #227-fu", membersOnly: true));
        await UpdateAsync(secret, NewEvent("Privada a editar #227-fu", location: "Castelo"));
        (await EventByIdAsync(secret)).Should().Match<Event>(e => !e.IsPublic && e.Location == "Castelo",
            "the edit applies, the visibility is kept");
        (await visitor.GetAsync($"/api/events/{secret}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 3. Private event updated with membersOnly=false -> public.
        await UpdateAsync(secret, NewEvent("Privada a editar #227-fu", membersOnly: false));
        (await EventByIdAsync(secret)).IsPublic.Should().BeTrue();
        (await visitor.GetAsync($"/api/events/{secret}")).StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Public event updated with membersOnly=true -> private.
        await UpdateAsync(omitted, NewEvent("Criada sem campo #227-fu", membersOnly: true));
        (await EventByIdAsync(omitted)).IsPublic.Should().BeFalse();
        (await visitor.GetAsync($"/api/events/{omitted}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // And a public event updated without membersOnly stays public.
        var open = await CreateAsync(NewEvent("Pública a editar #227-fu", membersOnly: false));
        await UpdateAsync(open, NewEvent("Pública a editar #227-fu"));
        (await EventByIdAsync(open)).IsPublic.Should().BeTrue();
    }

    private static List<int> Ids(JsonElement list) => list.EnumerateArray().Select(e => e.GetProperty("id").GetInt32()).ToList();
}
