// EventDiscussionApiTests: Contacts tests.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;

public partial class EventDiscussionApiTests
{
    // ---------- contacts ----------

    [Fact]
    public async Task Contacts_AreModAndAbove_ReadAndWrite()
    {
        var eventId = await AddEventAsync("Contactos");
        var (member, _) = await SignInAsync();
        var (mod, _) = await SignInAsync("Mod");
        var (_, called) = await SignInAsync();
        await SetPhoneAsync(called.Id, "912 345 678");
        await AreaHttp.WithTokenAsync(member);

        (await Anonymous().GetAsync($"/api/events/{eventId}/contacts")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var refused = await member.GetAsync($"/api/events/{eventId}/contacts");
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await refused.Content.ReadAsStringAsync()).Should().NotContain("912 345 678");
        (await member.PutAsJsonAsync($"/api/events/{eventId}/contacts/{called.Id}", new { willAttend = true, notes = "" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await mod.GetAsync("/api/events/987654/contacts")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var list = await mod.GetFromJsonAsync<JsonElement>($"/api/events/{eventId}/contacts");
        list.GetProperty("notContacted").EnumerateArray().Should().Contain(r => r.GetProperty("phone").GetString() == "912 345 678");
    }

    [Fact]
    public async Task Contacts_AreOpenToAnyLeitao_ReadAndWrite()
    {
        var eventId = await AddEventAsync("Contactos leitão");
        var (leitao, leitaoUser) = await SignInAsync();
        var (_, called) = await SignInAsync();
        await SetCategoriesAsync(leitaoUser.Id, MemberCategory.Leitao);
        await AreaHttp.WithTokenAsync(leitao);

        (await leitao.GetAsync($"/api/events/{eventId}/contacts")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await leitao.PutAsJsonAsync($"/api/events/{eventId}/contacts/{called.Id}", new { willAttend = true, notes = "" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await EnrollmentAsync(eventId, called.Id))!.WillAttend.Should().BeTrue();
    }

    [Theory]
    [InlineData("Mod")]
    [InlineData("Admin")]
    [InlineData("Owner")]
    public async Task ModAndAbove_RecordACall_ThatSyncsTheEnrollment_AndResetUndoesBoth(string role)
    {
        var eventId = await AddEventAsync($"Chamadas {role}");
        var (caller, _) = await SignInAsync(role);
        var (_, called) = await SignInAsync();
        await AreaHttp.WithTokenAsync(caller);
        var url = $"/api/events/{eventId}/contacts/{called.Id}";

        (await caller.PutAsJsonAsync(url, new { willAttend = true, notes = new string('n', 501) })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await caller.PutAsJsonAsync($"/api/events/{eventId}/contacts/no-such-user", new { willAttend = true, notes = "" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var saved = await caller.PutAsJsonAsync(url, new { willAttend = true, notes = "Vai de carro" });
        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        var row = (await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("contacted").EnumerateArray()
            .Single(r => r.GetProperty("userId").GetString() == called.Id);
        row.GetProperty("willAttend").GetBoolean().Should().BeTrue();
        (await EnrollmentAsync(eventId, called.Id))!.WillAttend.Should().BeTrue("an answer records the inscrição, as before");

        (await caller.PutAsJsonAsync(url, new { willAttend = false, notes = "" })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await EnrollmentAsync(eventId, called.Id))!.WillAttend.Should().BeFalse();

        (await caller.DeleteAsync(url)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await EnrollmentAsync(eventId, called.Id)).Should().BeNull("repor removes the inscrição, as before");
        (await caller.DeleteAsync(url)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ContactWrites_WithoutTheAntiforgeryHeader_AreRefused()
    {
        var eventId = await AddEventAsync("Contactos sem token");
        var (mod, _) = await SignInAsync("Mod");
        var (_, called) = await SignInAsync();

        (await mod.PutAsJsonAsync($"/api/events/{eventId}/contacts/{called.Id}", new { willAttend = true, notes = "" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await EnrollmentAsync(eventId, called.Id)).Should().BeNull();
    }
}
