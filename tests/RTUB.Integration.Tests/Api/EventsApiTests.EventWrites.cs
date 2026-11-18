// EventsApiTests tests: event create, edit, delete, cancel and notices.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Core.Entities;

namespace RTUB.Integration.Tests.Api;
public partial class EventsApiTests
{
    // ---------- management (Admin/Owner, 011.5) ----------

    [Fact]
    public async Task EventWrites_AreRefused_ForVisitorsAndMembers_AndWithoutTheAntiforgeryHeader()
    {
        var seed = await SeedAsync();
        var visitor = Anonymous();
        await AreaHttp.WithTokenAsync(visitor);
        var (member, _) = await SignInAsync();
        await AreaHttp.WithTokenAsync(member);
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
        await AreaHttp.WithTokenAsync(client);
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
    public async Task AMod_CreatesAndEditsEvents_ButCannotDeleteThem()
    {
        var (mod, _) = await SignInAsync("Mod");
        await AreaHttp.WithTokenAsync(mod);

        var agenda = await mod.GetFromJsonAsync<JsonElement>("/api/events");
        (agenda.GetProperty("canEdit").GetBoolean(), agenda.GetProperty("canManage").GetBoolean()).Should().Be((true, false));
        agenda.GetProperty("types").ValueKind.Should().Be(JsonValueKind.Array, "the form needs the type list");

        var created = await mod.PostAsJsonAsync("/api/events", NewEvent("Do Mod"));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        (await mod.GetAsync($"/api/events/{id}/edit")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await mod.PutAsJsonAsync($"/api/events/{id}", NewEvent("Do Mod (editado)"))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await EventNamedAsync("Do Mod (editado)")).Should().NotBeNull();

        (await mod.DeleteAsync($"/api/events/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await EventByIdAsync(id)).Should().NotBeNull();
    }

    // ---------- image, cancel / reactivate, notices (Admin/Owner, 012A) ----------

    [Fact]
    public async Task AdvancedWrites_AreRefused_ForVisitorsMembersAndMods_AndWithoutTheAntiforgeryHeader()
    {
        var id = await AddEventAsync("Arraial protegido", DateTime.Today.AddDays(300));
        var visitor = Anonymous();
        await AreaHttp.WithTokenAsync(visitor);
        var (member, _) = await SignInAsync();
        await AreaHttp.WithTokenAsync(member);
        var (mod, _) = await SignInAsync("Mod");
        await AreaHttp.WithTokenAsync(mod);
        var (adminWithoutToken, _) = await SignInAsync("Admin");
        _factory.Email.Invocations.Clear();
        _factory.Storage.Invocations.Clear();

        // The image belongs to editing the event (Mod and above); a Mod is refused the rest.
        foreach (var (client, expected) in new[] { (visitor, HttpStatusCode.Unauthorized), (member, HttpStatusCode.Forbidden) })
        {
            (await client.PostAsync($"/api/events/{id}/image", ImageForm())).StatusCode.Should().Be(expected);
            (await client.DeleteAsync($"/api/events/{id}/image")).StatusCode.Should().Be(expected);
        }

        foreach (var (client, expected) in new[] { (visitor, HttpStatusCode.Unauthorized), (member, HttpStatusCode.Forbidden), (mod, HttpStatusCode.Forbidden) })
        {
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
        await AreaHttp.WithTokenAsync(client);

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
        await AreaHttp.WithTokenAsync(member);
        await AddEnrollmentAsync(id, user.Id, willAttend: true);
        var (admin, _) = await SignInAsync("Admin");
        await AreaHttp.WithTokenAsync(admin);

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
        await AreaHttp.WithTokenAsync(admin);

        (await admin.PostAsJsonAsync($"/api/events/{id}/cancel", new { reason = "x", notifyByEmail = false })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.PostAsJsonAsync($"/api/events/{id}/notices", EmailNotice())).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Notices_ShowTheAudienceAsCounts_AndSendThroughTheFakes()
    {
        var id = await AddEventAsync("Arraial com aviso", DateTime.Today.AddDays(303));
        var (admin, adminUser) = await SignInAsync("Admin");
        await AreaHttp.WithTokenAsync(admin);
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

    private static MultipartFormDataContent ImageForm(string contentType = "image/webp", byte[]? bytes = null)
    {
        var file = new ByteArrayContent(bytes ?? WebpBytes);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "image", "event-image.webp" } };
    }

    private static object EmailNotice(string kind = "new") => new { channel = "email", kind, message = (string?)null, onlyLeitoesAndCaloiros = false };

    private async Task<Event?> EventNamedAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Events.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Name == name);
    }
}
