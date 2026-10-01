using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
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
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// /api/events (React track 011) through the real host: the real login, real antiforgery, the real
/// SQLite test database and the real EnrollmentService. Push is a recording fake, so nothing is
/// ever sent; video and image links are fake https URLs and storage is never called.
/// </summary>
public class EventsApiTests : IClassFixture<EventsApiFactory>
{
    private const string Description = "Descrição só para membros";
    private const string Reason = "Motivo interno do cancelamento";
    private const string MemberNote = "nota-privada-de-um-membro";

    private readonly EventsApiFactory _factory;

    public EventsApiTests(EventsApiFactory factory)
    {
        _factory = factory;
    }

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

    // ---------- enrollment ----------

    [Fact]
    public async Task Enrollment_IsForSignedInMembersOnly()
    {
        var seed = await SeedAsync();
        var visitor = Anonymous();
        await WithTokenAsync(visitor);

        (await visitor.GetAsync($"/api/events/{seed.Open}/enrollment")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await visitor.PutAsJsonAsync($"/api/events/{seed.Open}/enrollment", Answer(true))).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
        (await visitor.DeleteAsync($"/api/events/{seed.Past}/enrollment")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Writes_WithoutTheAntiforgeryHeader_AreRefused_AndChangeNothing()
    {
        var seed = await SeedAsync();
        var (member, user) = await SignInAsync();

        var put = await member.PutAsJsonAsync($"/api/events/{seed.Open}/enrollment", Answer(true));
        var delete = await member.DeleteAsync($"/api/events/{seed.Past}/enrollment");
        var play = await member.PostAsync($"/api/events/videos/{seed.Video}/plays", null);

        put.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        delete.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        play.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await EnrollmentAsync(seed.Open, user.Id)).Should().BeNull();
    }

    [Fact]
    public async Task AMember_AnswersGoing_ThenNotGoing_OnTheSameEnrollment_WithNotificationsFaked()
    {
        var seed = await SeedAsync();
        var (member, user) = await SignInAsync();
        await WithTokenAsync(member);

        var going = await member.PutAsJsonAsync($"/api/events/{seed.Open}/enrollment", Answer(true, notes: "levo capa"));
        going.StatusCode.Should().Be(HttpStatusCode.OK);
        (await going.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString().Should().Be("going");

        var notGoing = await member.PutAsJsonAsync($"/api/events/{seed.Open}/enrollment", Answer(false, notes: "doente"));
        notGoing.StatusCode.Should().Be(HttpStatusCode.OK);

        var stored = await EnrollmentAsync(seed.Open, user.Id);
        stored!.WillAttend.Should().BeFalse();
        stored.Notes.Should().Be("doente");
        (await CountEnrollmentsAsync(seed.Open, user.Id)).Should().Be(1, "a second answer updates, never duplicates");
    }

    [Fact]
    public async Task AnInvalidInstrument_IsAFieldError()
    {
        var seed = await SeedAsync();
        var (member, user) = await SignInAsync();
        await WithTokenAsync(member);

        var response = await member.PutAsJsonAsync($"/api/events/{seed.Open}/enrollment", Answer(true, instrument: "Violino"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("instrument", out _).Should().BeTrue();
        (await EnrollmentAsync(seed.Open, user.Id)).Should().BeNull();
    }

    [Fact]
    public async Task CancelledAndPastEvents_TakeNoNewAnswer()
    {
        var seed = await SeedAsync();
        var (member, user) = await SignInAsync();
        await WithTokenAsync(member);

        (await member.PutAsJsonAsync($"/api/events/{seed.Cancelled}/enrollment", Answer(true))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await member.PutAsJsonAsync($"/api/events/{seed.Past}/enrollment", Answer(true))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await EnrollmentAsync(seed.Cancelled, user.Id)).Should().BeNull();
    }

    [Fact]
    public async Task APastEvent_LetsTheMemberWhoWentWithdraw()
    {
        var seed = await SeedAsync();
        var (member, user) = await SignInAsync();
        await AddEnrollmentAsync(seed.Past, user.Id, willAttend: true);
        await WithTokenAsync(member);

        var response = await member.DeleteAsync($"/api/events/{seed.Past}/enrollment");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await EnrollmentAsync(seed.Past, user.Id)).Should().BeNull();
    }

    [Fact]
    public async Task AVideoPlay_IsRecorded_ForVisitorsToo()
    {
        var seed = await SeedAsync();
        var visitor = Anonymous();
        await WithTokenAsync(visitor);

        (await visitor.PostAsync($"/api/events/videos/{seed.Video}/plays", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await visitor.PostAsync("/api/events/videos/987654/plays", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TheHomePreview_StillGivesAtMostThreeUpcomingEvents_WithoutIds()
    {
        await SeedAsync();

        var raw = await Anonymous().GetStringAsync("/api/public/events/upcoming");

        JsonDocument.Parse(raw).RootElement.GetArrayLength().Should().BeLessThanOrEqualTo(3);
        raw.Should().NotContain("\"id\"").And.NotContain(Description).And.NotContain(Reason);
    }

    // ---------- management (Admin/Owner, 011.5) ----------

    [Fact]
    public async Task EventWrites_AreRefused_ForVisitorsAndMembers_AndWithoutTheAntiforgeryHeader()
    {
        var seed = await SeedAsync();
        var visitor = Anonymous();
        await WithTokenAsync(visitor);
        var (member, _) = await SignInAsync();
        await WithTokenAsync(member);
        var (adminWithoutToken, _) = await SignInAsync("Admin");

        (await visitor.PostAsJsonAsync("/api/events", NewEvent("Do visitante"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await member.PostAsJsonAsync("/api/events", NewEvent("Do membro"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.PutAsJsonAsync($"/api/events/{seed.Open}", NewEvent("Mudado"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.DeleteAsync($"/api/events/{seed.Open}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.GetAsync($"/api/events/{seed.Open}/edit")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await adminWithoutToken.PostAsJsonAsync("/api/events", NewEvent("Sem token"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await adminWithoutToken.DeleteAsync($"/api/events/{seed.Open}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await EventNamedAsync("Do visitante")).Should().BeNull();
        (await EventNamedAsync("Do membro")).Should().BeNull();
        (await EventNamedAsync("Sem token")).Should().BeNull();
        (await member.GetFromJsonAsync<JsonElement>("/api/events")).GetProperty("canManage").GetBoolean().Should().BeFalse();
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Owner")]
    public async Task AdminOrOwner_CreatesEditsAndDeletes_AnEvent(string role)
    {
        var (client, _) = await SignInAsync(role);
        await WithTokenAsync(client);
        var name = $"Arraial de {role}";

        var agenda = await client.GetFromJsonAsync<JsonElement>("/api/events");
        agenda.GetProperty("canManage").GetBoolean().Should().BeTrue();
        agenda.GetProperty("types").GetArrayLength().Should().BeGreaterThan(5);

        var created = await client.PostAsJsonAsync("/api/events", NewEvent(name));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        _factory.Push.Verify(p => p.BroadcastAsync(It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()),
            Times.AtLeastOnce, "a new event is announced, as before (to a fake)");

        var edit = await client.GetFromJsonAsync<JsonElement>($"/api/events/{id}/edit");
        edit.GetProperty("name").GetString().Should().Be(name);
        edit.GetProperty("time").GetString().Should().Be("21:00");

        var updated = await client.PutAsJsonAsync($"/api/events/{id}", NewEvent(name + " (adiado)", location: "Castelo"));
        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        (await EventNamedAsync(name + " (adiado)"))!.Location.Should().Be("Castelo");

        var invalid = await client.PutAsJsonAsync($"/api/events/{id}", NewEvent(""));
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await invalid.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("name", out _).Should().BeTrue();

        (await client.DeleteAsync($"/api/events/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/events/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.DeleteAsync($"/api/events/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AMod_CannotManageEvents()
    {
        var (mod, _) = await SignInAsync("Mod");
        await WithTokenAsync(mod);

        (await mod.PostAsJsonAsync("/api/events", NewEvent("Do Mod"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------- image, cancel / reactivate, notices (Admin/Owner, 012A) ----------

    [Fact]
    public async Task AdvancedWrites_AreRefused_ForVisitorsMembersAndMods_AndWithoutTheAntiforgeryHeader()
    {
        var id = await AddEventAsync("Arraial protegido", DateTime.Today.AddDays(300));
        var visitor = Anonymous();
        await WithTokenAsync(visitor);
        var (member, _) = await SignInAsync();
        await WithTokenAsync(member);
        var (mod, _) = await SignInAsync("Mod");
        await WithTokenAsync(mod);
        var (adminWithoutToken, _) = await SignInAsync("Admin");
        _factory.Email.Invocations.Clear();
        _factory.Storage.Invocations.Clear();

        foreach (var (client, expected) in new[] { (visitor, HttpStatusCode.Unauthorized), (member, HttpStatusCode.Forbidden), (mod, HttpStatusCode.Forbidden) })
        {
            (await client.PostAsync($"/api/events/{id}/image", ImageForm())).StatusCode.Should().Be(expected);
            (await client.DeleteAsync($"/api/events/{id}/image")).StatusCode.Should().Be(expected);
            (await client.PostAsJsonAsync($"/api/events/{id}/cancel", new { reason = "x", notifyByEmail = true })).StatusCode.Should().Be(expected);
            (await client.PostAsync($"/api/events/{id}/reactivate", null)).StatusCode.Should().Be(expected);
            (await client.GetAsync($"/api/events/{id}/notices")).StatusCode.Should().Be(expected);
            (await client.PostAsJsonAsync($"/api/events/{id}/notices", EmailNotice())).StatusCode.Should().Be(expected);
        }

        (await adminWithoutToken.PostAsync($"/api/events/{id}/image", ImageForm())).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await adminWithoutToken.PostAsJsonAsync($"/api/events/{id}/cancel", new { reason = "x", notifyByEmail = false })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await adminWithoutToken.PostAsync($"/api/events/{id}/reactivate", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await adminWithoutToken.PostAsJsonAsync($"/api/events/{id}/notices", EmailNotice())).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var stored = await EventByIdAsync(id);
        stored.IsCancelled.Should().BeFalse();
        stored.ImageUrl.Should().BeNull();
        _factory.Email.Invocations.Should().BeEmpty("nothing is sent for them");
        _factory.Storage.Invocations.Should().BeEmpty("nothing is uploaded or deleted for them");
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Owner")]
    public async Task AdminOrOwner_ReplacesAndRemovesTheImage_ThroughTheStorageFake(string role)
    {
        var id = await AddEventAsync($"Com imagem ({role})", DateTime.Today.AddDays(301), imageUrl: $"https://pub-test.r2.dev/images/test/events/old-{role}.webp");
        var (client, _) = await SignInAsync(role);
        await WithTokenAsync(client);

        (await client.PostAsync($"/api/events/{id}/image", ImageForm())).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await EventByIdAsync(id)).ImageUrl.Should().Be(FakeImageUrl);
        _factory.Storage.Verify(s => s.DeleteImageAsync($"https://pub-test.r2.dev/images/test/events/old-{role}.webp"), Times.Once, "a replaced image is deleted, as before");
        (await client.GetFromJsonAsync<JsonElement>($"/api/events/{id}/edit")).GetProperty("imageUrl").GetString().Should().Be(FakeImageUrl);

        var refused = await client.PostAsync($"/api/events/{id}/image", ImageForm("text/html", "<script>x</script>"u8.ToArray()));
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("image", out _).Should().BeTrue();
        (await client.PostAsync($"/api/events/{id}/image", new MultipartFormDataContent())).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await client.DeleteAsync($"/api/events/{id}/image")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await EventByIdAsync(id)).ImageUrl.Should().BeNull();
        _factory.Storage.Verify(s => s.DeleteImageAsync(FakeImageUrl), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Cancel_DeletesTheEnrollments_ClosesAnswers_AndReactivateReopens()
    {
        var id = await AddEventAsync("Arraial a cancelar", DateTime.Today.AddDays(302));
        var (member, user) = await SignInAsync();
        await WithTokenAsync(member);
        await AddEnrollmentAsync(id, user.Id, willAttend: true);
        var (admin, _) = await SignInAsync("Admin");
        await WithTokenAsync(admin);

        (await admin.PostAsJsonAsync($"/api/events/{id}/cancel", new { reason = " ", notifyByEmail = false })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var cancelled = await admin.PostAsJsonAsync($"/api/events/{id}/cancel", new { reason = "Chuva forte", notifyByEmail = false });
        cancelled.StatusCode.Should().Be(HttpStatusCode.OK);

        var stored = await EventByIdAsync(id);
        stored.IsCancelled.Should().BeTrue();
        stored.CancellationReason.Should().Be("Chuva forte");
        (await EnrollmentAsync(id, user.Id)).Should().BeNull("cancelling deletes the enrollments, as before");
        (await member.PutAsJsonAsync($"/api/events/{id}/enrollment", Answer(true))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await member.GetFromJsonAsync<JsonElement>($"/api/events/{id}")).GetProperty("member").GetProperty("cancellationReason").GetString()
            .Should().Be("Chuva forte");
        (await admin.PostAsJsonAsync($"/api/events/{id}/cancel", new { reason = "outra vez", notifyByEmail = false })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.PostAsJsonAsync($"/api/events/{id}/notices", EmailNotice())).StatusCode.Should().Be(HttpStatusCode.Conflict, "no notices for a cancelled event");

        (await admin.PostAsync($"/api/events/{id}/reactivate", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await EventByIdAsync(id)).IsCancelled.Should().BeFalse();
        (await EnrollmentAsync(id, user.Id)).Should().BeNull("reactivating does not bring the deleted enrollments back");
        (await member.PutAsJsonAsync($"/api/events/{id}/enrollment", Answer(true))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.PostAsync($"/api/events/{id}/reactivate", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PastEvents_CannotBeCancelledOrNotified()
    {
        var id = await AddEventAsync("Arraial antigo", DateTime.Today.AddDays(-300));
        var (admin, _) = await SignInAsync("Owner");
        await WithTokenAsync(admin);

        (await admin.PostAsJsonAsync($"/api/events/{id}/cancel", new { reason = "x", notifyByEmail = false })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.PostAsJsonAsync($"/api/events/{id}/notices", EmailNotice())).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Notices_ShowTheAudienceAsCounts_AndSendThroughTheFakes()
    {
        var id = await AddEventAsync("Arraial com aviso", DateTime.Today.AddDays(303));
        var (admin, adminUser) = await SignInAsync("Admin");
        await WithTokenAsync(admin);
        _factory.Push.Setup(p => p.GetSubscribedUserIdsAsync()).ReturnsAsync(new[] { adminUser.Id });
        _factory.Push.Setup(p => p.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()))
            .ReturnsAsync((1, 0));

        var audience = await admin.GetAsync($"/api/events/{id}/notices");
        audience.StatusCode.Should().Be(HttpStatusCode.OK);
        var raw = await audience.Content.ReadAsStringAsync();
        raw.Should().NotContain("@").And.NotContain(adminUser.Id, "only counts, never addresses or ids");
        JsonDocument.Parse(raw).RootElement.GetProperty("pushSubscribed").GetInt32().Should().Be(1);

        var email = await admin.PostAsJsonAsync($"/api/events/{id}/notices", EmailNotice("reminder"));
        email.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.Email.Verify(m => m.SendEventReminderNotificationAsync(id, "Arraial com aviso", It.IsAny<DateTime>(), "Bragança",
            It.Is<string>(l => l.EndsWith("/events")), It.Is<List<string>>(l => l.Contains(adminUser.Email!)),
            It.IsAny<Dictionary<string, (string, string)>>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<IProgress<EmailSendProgress>?>()), Times.Once);

        var push = await admin.PostAsJsonAsync($"/api/events/{id}/notices", new { channel = "push", kind = (string?)null, message = "Concentração às 20h", onlyLeitoesAndCaloiros = false });
        push.StatusCode.Should().Be(HttpStatusCode.OK);
        (await push.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sent").GetInt32().Should().Be(1);
        _factory.Push.Verify(p => p.SendToSelectedUsersAsync(It.Is<IEnumerable<string>>(ids => ids.Single() == adminUser.Id),
            It.Is<RTUB.Application.DTOs.SendPushNotificationDto>(n => n.Title == "Arraial com aviso" && n.Body == "Concentração às 20h")), Times.Once);

        var empty = await admin.PostAsJsonAsync($"/api/events/{id}/notices", new { channel = "push", kind = (string?)null, message = "", onlyLeitoesAndCaloiros = false });
        empty.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------- prizes (Admin/Owner, 012B) ----------

    [Fact]
    public async Task PrizeWrites_AreRefused_ForVisitorsMembersAndMods_AndWithoutTheAntiforgeryHeader()
    {
        var id = await AddEventAsync("Festival protegido", DateTime.Today.AddDays(-200), type: EventType.Festival);
        var prize = await AddTrophyAsync(id, "Intocável");
        var visitor = Anonymous();
        await WithTokenAsync(visitor);
        var (member, _) = await SignInAsync();
        await WithTokenAsync(member);
        var (mod, _) = await SignInAsync("Mod");
        await WithTokenAsync(mod);
        var (adminWithoutToken, _) = await SignInAsync("Admin");

        foreach (var (client, expected) in new[] { (visitor, HttpStatusCode.Unauthorized), (member, HttpStatusCode.Forbidden), (mod, HttpStatusCode.Forbidden) })
        {
            (await client.GetAsync($"/api/events/{id}/prizes")).StatusCode.Should().Be(expected);
            (await client.PostAsJsonAsync($"/api/events/{id}/prizes", new { name = "Novo" })).StatusCode.Should().Be(expected);
            (await client.PutAsJsonAsync($"/api/events/{id}/prizes/{prize}", new { name = "Mudado" })).StatusCode.Should().Be(expected);
            (await client.DeleteAsync($"/api/events/{id}/prizes/{prize}")).StatusCode.Should().Be(expected);
        }

        (await adminWithoutToken.PostAsJsonAsync($"/api/events/{id}/prizes", new { name = "Sem token" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await adminWithoutToken.PutAsJsonAsync($"/api/events/{id}/prizes/{prize}", new { name = "Sem token" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await adminWithoutToken.DeleteAsync($"/api/events/{id}/prizes/{prize}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await TrophyNamesAsync(id)).Should().Equal("Intocável");
        var page = await member.GetFromJsonAsync<JsonElement>($"/api/events/{id}");
        page.GetProperty("canManagePrizes").GetBoolean().Should().BeFalse();
        page.GetProperty("event").GetProperty("trophies").EnumerateArray().Select(t => t.GetString()).Should().Equal("Intocável");
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Owner")]
    public async Task AdminOrOwner_ManagePrizes_AndThePublicViewFollows(string role)
    {
        var id = await AddEventAsync($"Festival com prémios ({role})", DateTime.Today.AddDays(-201), type: EventType.Festival);
        var (client, _) = await SignInAsync(role);
        await WithTokenAsync(client);
        var visitor = Anonymous();

        (await client.GetFromJsonAsync<JsonElement>($"/api/events/{id}")).GetProperty("canManagePrizes").GetBoolean().Should().BeTrue();
        (await client.GetFromJsonAsync<JsonElement>($"/api/events/{id}/prizes")).GetArrayLength().Should().Be(0);

        var empty = await client.PostAsJsonAsync($"/api/events/{id}/prizes", new { name = " " });
        empty.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await empty.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("name", out _).Should().BeTrue();

        var added = await client.PostAsJsonAsync($"/api/events/{id}/prizes", new { name = "Melhor Tuna" });
        added.StatusCode.Should().Be(HttpStatusCode.OK);
        var prizeId = (await added.Content.ReadFromJsonAsync<JsonElement>())[0].GetProperty("id").GetInt32();
        (await visitor.GetFromJsonAsync<JsonElement>($"/api/events/{id}")).GetProperty("event").GetProperty("trophies")[0].GetString()
            .Should().Be("Melhor Tuna", "visitors see a festival's prizes, as before");

        var renamed = await client.PutAsJsonAsync($"/api/events/{id}/prizes/{prizeId}", new { name = "Melhor Tuna 2026" });
        renamed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await TrophyNamesAsync(id)).Should().Equal("Melhor Tuna 2026");

        var deleted = await client.DeleteAsync($"/api/events/{id}/prizes/{prizeId}");
        deleted.StatusCode.Should().Be(HttpStatusCode.OK);
        (await deleted.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength().Should().Be(0);
        (await visitor.GetFromJsonAsync<JsonElement>($"/api/events/{id}")).GetProperty("event").GetProperty("trophies").GetArrayLength()
            .Should().Be(0, "the last prize gone, nothing is left to show");
        (await client.DeleteAsync($"/api/events/{id}/prizes/{prizeId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NewPrizes_AreRefused_ForAnUpcomingEvent()
    {
        var id = await AddEventAsync("Festival por vir", DateTime.Today.AddDays(304), type: EventType.Festival);
        var (owner, _) = await SignInAsync("Owner");
        await WithTokenAsync(owner);

        (await owner.PostAsJsonAsync($"/api/events/{id}/prizes", new { name = "Cedo demais" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await owner.GetFromJsonAsync<JsonElement>($"/api/events/{id}")).GetProperty("canManagePrizes").GetBoolean().Should().BeFalse();
    }

    // ---------- videos (Admin/Owner, 012C) ----------

    [Fact]
    public async Task VideoWrites_AreRefused_ForVisitorsMembersAndMods_AndWithoutTheAntiforgeryHeader()
    {
        var id = await AddEventAsync("Arraial com vídeo protegido", DateTime.Today.AddDays(-210));
        var video = await AddEventVideoAsync(id, "Intocável", 0);
        var visitor = Anonymous();
        await WithTokenAsync(visitor);
        var (member, _) = await SignInAsync();
        await WithTokenAsync(member);
        var (mod, _) = await SignInAsync("Mod");
        await WithTokenAsync(mod);
        var (adminWithoutToken, _) = await SignInAsync("Admin");
        _factory.VideoStorage.Invocations.Clear();

        foreach (var (client, expected) in new[] { (visitor, HttpStatusCode.Unauthorized), (member, HttpStatusCode.Forbidden), (mod, HttpStatusCode.Forbidden) })
        {
            (await client.GetAsync($"/api/events/{id}/videos")).StatusCode.Should().Be(expected);
            (await client.PostAsync($"/api/events/{id}/videos", VideoForm())).StatusCode.Should().Be(expected);
            (await client.PutAsJsonAsync($"/api/events/{id}/videos/{video}", new { title = "Mudado" })).StatusCode.Should().Be(expected);
            (await client.PostAsJsonAsync($"/api/events/{id}/videos/reorder", new { videoIds = new[] { video } })).StatusCode.Should().Be(expected);
            (await client.DeleteAsync($"/api/events/{id}/videos/{video}")).StatusCode.Should().Be(expected);
        }

        (await adminWithoutToken.PostAsync($"/api/events/{id}/videos", VideoForm())).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await adminWithoutToken.PutAsJsonAsync($"/api/events/{id}/videos/{video}", new { title = "x" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await adminWithoutToken.PostAsJsonAsync($"/api/events/{id}/videos/reorder", new { videoIds = new[] { video } })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await adminWithoutToken.DeleteAsync($"/api/events/{id}/videos/{video}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        _factory.VideoStorage.Invocations.Should().BeEmpty("nothing is uploaded or deleted for them");
        (await member.GetFromJsonAsync<JsonElement>($"/api/events/{id}")).GetProperty("videos")[0].GetProperty("title").GetString()
            .Should().Be("Intocável", "members still watch it");
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Owner")]
    public async Task AdminOrOwner_UploadRenameReorderAndDelete_ThroughTheStorageFake(string role)
    {
        var id = await AddEventAsync($"Arraial filmado ({role})", DateTime.Today.AddDays(-211));
        var first = await AddEventVideoAsync(id, "Primeiro", 0);
        var (client, _) = await SignInAsync(role);
        await WithTokenAsync(client);
        var stored = $"https://pub-test.r2.dev/events/test/videos/{id}_new.mp4";
        _factory.VideoStorage.Setup(s => s.UploadVideoAsync(It.IsAny<Stream>(), "festa.mp4", "video/mp4", id)).ReturnsAsync(stored);

        var refused = await client.PostAsync($"/api/events/{id}/videos", VideoForm(contentType: "text/plain", name: "notas.txt"));
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("file", out _).Should().BeTrue();
        (await client.PostAsync($"/api/events/{id}/videos", VideoForm(title: ""))).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var uploaded = await client.PostAsync($"/api/events/{id}/videos", VideoForm(title: "A atuação toda"));
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await uploaded.Content.ReadFromJsonAsync<JsonElement>();
        list.EnumerateArray().Select(v => v.GetProperty("title").GetString()).Should().Equal("Primeiro", "A atuação toda");
        var second = list[1].GetProperty("id").GetInt32();
        list.GetRawText().Should().NotContain("pub-test", "the management list carries no URL");

        var page = await Anonymous().GetFromJsonAsync<JsonElement>($"/api/events/{id}");
        page.GetProperty("videos")[1].GetProperty("url").GetString().Should().Be(stored, "visitors watch it straight away");

        (await client.PutAsJsonAsync($"/api/events/{id}/videos/{second}", new { title = "Atuação completa" })).StatusCode.Should().Be(HttpStatusCode.OK);
        var reordered = await client.PostAsJsonAsync($"/api/events/{id}/videos/reorder", new { videoIds = new[] { second, first } });
        reordered.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Anonymous().GetFromJsonAsync<JsonElement>($"/api/events/{id}")).GetProperty("videos").EnumerateArray()
            .Select(v => v.GetProperty("title").GetString()).Should().Equal("Atuação completa", "Primeiro");

        (await client.PostAsync($"/api/events/videos/{second}/plays", null)).StatusCode.Should().Be(HttpStatusCode.NoContent, "plays are still audited");

        (await client.DeleteAsync($"/api/events/{id}/videos/{second}")).StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.VideoStorage.Verify(s => s.DeleteVideoAsync(stored), Times.Once);
        var last = await client.DeleteAsync($"/api/events/{id}/videos/{first}");
        (await last.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength().Should().Be(0);
        (await Anonymous().GetFromJsonAsync<JsonElement>($"/api/events/{id}")).GetProperty("videos").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task AVideoUpload_ToAnUpcomingEvent_OrFailingInStorage_LeavesNoRow()
    {
        var upcoming = await AddEventAsync("Arraial por filmar", DateTime.Today.AddDays(305));
        var past = await AddEventAsync("Arraial sem sorte", DateTime.Today.AddDays(-212));
        var (owner, _) = await SignInAsync("Owner");
        await WithTokenAsync(owner);
        _factory.VideoStorage.Setup(s => s.UploadVideoAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), past))
            .ThrowsAsync(new InvalidOperationException("R2 down"));

        (await owner.PostAsync($"/api/events/{upcoming}/videos", VideoForm())).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var failed = await owner.PostAsync($"/api/events/{past}/videos", VideoForm());
        failed.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        (await failed.Content.ReadAsStringAsync()).Should().NotContain("R2 down", "no internal detail reaches the browser");

        (await owner.GetFromJsonAsync<JsonElement>($"/api/events/{past}/videos")).GetArrayLength().Should().Be(0);
    }

    private static MultipartFormDataContent VideoForm(string contentType = "video/mp4", string name = "festa.mp4", string title = "Festa")
    {
        var file = new ByteArrayContent(new byte[] { 0, 0, 0, 24, (byte)'f', (byte)'t', (byte)'y', (byte)'p' });
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", name }, { new StringContent(title), "title" } };
    }

    private async Task<int> AddEventVideoAsync(int eventId, string title, int order)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var uploader = await db.Users.Select(u => u.Id).FirstAsync();
        var v = EventVideo.CreateVideo(eventId, $"https://pub-test.r2.dev/events/test/videos/{eventId}_{order}.mp4", "video/mp4", 8, uploader, title, order);
        db.EventVideos.Add(v);
        await db.SaveChangesAsync();
        return v.Id;
    }

    private async Task<int> AddTrophyAsync(int eventId, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var t = Trophy.Create(name, eventId);
        db.Trophies.Add(t);
        await db.SaveChangesAsync();
        return t.Id;
    }

    private async Task<List<string>> TrophyNamesAsync(int eventId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Trophies.AsNoTracking()
            .Where(t => t.EventId == eventId).OrderBy(t => t.Name).Select(t => t.Name).ToListAsync();
    }

    private const string FakeImageUrl ="https://pub-test.r2.dev/images/test/events/new.webp";

    private static readonly byte[] WebpBytes = "RIFF\0\0\0\0WEBPVP8 "u8.ToArray();

    private static MultipartFormDataContent ImageForm(string contentType = "image/webp", byte[]? bytes = null)
    {
        var file = new ByteArrayContent(bytes ?? WebpBytes);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "image", "event-image.webp" } };
    }

    private static object EmailNotice(string kind = "new") => new { channel = "email", kind, message = (string?)null, onlyLeitoesAndCaloiros = false };

    private async Task<int> AddEventAsync(string name, DateTime date, string? imageUrl = null, EventType type = EventType.Arraial)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var e = Event.Create(name, date, "Bragança", type, "");
        e.ImageUrl = imageUrl;
        db.Events.Add(e);
        await db.SaveChangesAsync();
        return e.Id;
    }

    private async Task<Event> EventByIdAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Events.AsNoTracking().SingleAsync(e => e.Id == id);
    }

    // ---------- helpers ----------

    private static object NewEvent(string name, string location = "Bragança") => new
    {
        name,
        date = DateTime.Today.AddDays(400).ToString("yyyy-MM-dd"),
        time = "21:00",
        endDate = (string?)null,
        location,
        type = "Arraial",
        description = "Só para membros",
    };

    private async Task<Event?> EventNamedAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Events.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Name == name);
    }

    private sealed record Seed(int Open, int MultiDay, int Cancelled, int Past, int Festival, int Video, string MemberId);

    private static readonly SemaphoreSlim Lock = new(1, 1);
    private static Seed? _seed;

    /// <summary>One set of events per test class; every test reads it and writes only its own member's answers.</summary>
    private async Task<Seed> SeedAsync()
    {
        await Lock.WaitAsync();
        try
        {
            if (_seed is not null)
            {
                return _seed;
            }

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var today = DateTime.Today;

            var other = await db.Users.Select(u => u.Id).FirstAsync();
            Event Add(string name, DateTime date, DateTime? end = null, EventType type = EventType.Atuacao, string description = "")
            {
                var e = Event.Create(name, date, "Bragança", type, description);
                e.EndDate = end;
                db.Events.Add(e);
                return e;
            }

            var open = Add("Serenata aberta", today.AddDays(1000).AddHours(21), description: Description);
            var multiDay = Add("Festival de três dias", today.AddDays(999), end: today.AddDays(1001));
            var cancelled = Add("Arraial cancelado", today.AddDays(1002));
            cancelled.Cancel(Reason);
            var past = Add("Atuação passada", today.AddDays(-1000));
            var festival = Add("Festival antigo", today.AddDays(-1001), type: EventType.Festival);
            await db.SaveChangesAsync();

            db.Trophies.Add(Trophy.Create("Melhor Pandeireta", festival.Id));
            db.Trophies.Add(Trophy.Create("1.º Prémio", festival.Id));
            var video = EventVideo.CreateVideo(festival.Id, "https://pub-test.r2.dev/videos/events/festival.mp4", "video/mp4", 10, other, "Atuação");
            db.EventVideos.Add(video);
            var note = Enrollment.Create(other, open.Id);
            note.Notes = MemberNote;
            db.Enrollments.Add(note);
            await db.SaveChangesAsync();

            return _seed = new Seed(open.Id, multiDay.Id, cancelled.Id, past.Id, festival.Id, video.Id, other);
        }
        finally
        {
            Lock.Release();
        }
    }

    private static object Answer(bool willAttend, string? instrument = null, string? notes = null) => new { willAttend, instrument, notes };

    private static List<int> Ids(JsonElement list) => list.EnumerateArray().Select(e => e.GetProperty("id").GetInt32()).ToList();

    private static int _ip;

    private Task<(HttpClient Client, ApplicationUser User)> SignInAsync(string? role = null)
    {
        var n = Interlocked.Increment(ref _ip);
        return CookieTestSession.SignInAsync(_factory, $"events-{Guid.NewGuid():N}"[..24], $"10.71.{n / 250}.{n % 250 + 1}", role);
    }

    private HttpClient Anonymous()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, "10.72.0.1");
        return client;
    }

    private static async Task WithTokenAsync(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/public/antiforgery-token");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }

    private async Task<Enrollment?> EnrollmentAsync(int eventId, string userId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Enrollments.AsNoTracking()
            .FirstOrDefaultAsync(e => e.EventId == eventId && e.UserId == userId);
    }

    private async Task<int> CountEnrollmentsAsync(int eventId, string userId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Enrollments
            .CountAsync(e => e.EventId == eventId && e.UserId == userId);
    }

    private async Task AddEnrollmentAsync(int eventId, string userId, bool willAttend)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var e = Enrollment.Create(userId, eventId);
        e.WillAttend = willAttend;
        db.Enrollments.Add(e);
        await db.SaveChangesAsync();
    }
}

/// <summary>
/// The test host with push, email and image storage replaced by recording fakes: nothing is ever
/// sent to anyone or written to R2.
/// </summary>
public sealed class EventsApiFactory : TestWebApplicationFactory
{
    public Mock<IPushNotificationService> Push { get; } = new();
    public Mock<IEmailNotificationService> Email { get; } = new();
    public Mock<IImageStorageService> Storage { get; } = new();
    public Mock<IEventVideoStorageService> VideoStorage { get; } = new();

    public EventsApiFactory()
    {
        Storage.Setup(s => s.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), "events", It.IsAny<string>()))
            .ReturnsAsync("https://pub-test.r2.dev/images/test/events/new.webp");
        Email.Setup(m => m.SendEventReminderNotificationAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<Dictionary<string, (string, string)>>(), It.IsAny<string>(),
                It.IsAny<DateTime?>(), It.IsAny<IProgress<EmailSendProgress>?>()))
            .ReturnsAsync((true, 1, (string?)null));
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
            services.RemoveAll<IImageStorageService>();
            services.AddSingleton(Storage.Object);
            services.RemoveAll<IEventVideoStorageService>();
            services.AddSingleton(VideoStorage.Object);
        });
    }
}
