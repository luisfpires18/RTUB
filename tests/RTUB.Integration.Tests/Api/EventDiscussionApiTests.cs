using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// /api/events/{id}/discussion and /api/events/{id}/contacts (React track 013) through the real host: real
/// login, antiforgery and SQLite, the real Post/Comment/Transportation/EventContact/Enrollment services.
/// Push, email and storage are the recording fakes of <see cref="EventsApiFactory"/>: nothing is sent or stored.
/// </summary>
public class EventDiscussionApiTests : IClassFixture<EventsApiFactory>
{
    private readonly EventsApiFactory _factory;

    public EventDiscussionApiTests(EventsApiFactory factory)
    {
        _factory = factory;
    }

    // ---------- discussion ----------

    [Fact]
    public async Task Discussion_IsForSignedInMembers_AndUnknownEventsAre404()
    {
        var eventId = await AddEventAsync("Conversa fechada");
        var (member, _) = await SignInAsync();

        (await Anonymous().GetAsync($"/api/events/{eventId}/discussion")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await member.GetAsync("/api/events/987654/discussion")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var empty = await member.GetFromJsonAsync<JsonElement>($"/api/events/{eventId}/discussion");
        empty.GetProperty("posts").GetArrayLength().Should().Be(0);
        empty.GetProperty("canModerate").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Writes_WithoutTheAntiforgeryHeader_AreRefused_AndWriteNothing()
    {
        var eventId = await AddEventAsync("Sem token");
        var (member, _) = await SignInAsync();

        var response = await member.PostAsJsonAsync($"/api/events/{eventId}/discussion/posts", new { title = "", body = "Olá a todos" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PostCountAsync(eventId)).Should().Be(0);
    }

    [Fact]
    public async Task AMember_PostsComments_AndEditsOwnPosts_WithoutLeakingIds()
    {
        var eventId = await AddEventAsync("Conversa aberta");
        var (author, authorUser) = await SignInAsync();
        await WithTokenAsync(author);

        var posted = await author.PostAsJsonAsync($"/api/events/{eventId}/discussion/posts",
            new { title = "", body = "Levem capas pretas.\nSaída às 18h." });
        posted.StatusCode.Should().Be(HttpStatusCode.OK);
        var raw = await posted.Content.ReadAsStringAsync();
        raw.Should().NotContain(authorUser.Id).And.NotContain("@test.com").And.NotContainAny(new[] { "authorId", "email", "phone", "mentionsJson" });

        var post = JsonDocument.Parse(raw).RootElement.GetProperty("posts").EnumerateArray().Single();
        post.GetProperty("title").GetString().Should().Be("Levem capas pretas. Saída às 18h.", "a blank title comes from the text");
        post.GetProperty("canEdit").GetBoolean().Should().BeTrue();
        var postId = post.GetProperty("id").GetInt32();

        var commented = await author.PostAsJsonAsync($"/api/events/{eventId}/discussion/posts/{postId}/comments", new { body = "E as pandeiretas." });
        commented.StatusCode.Should().Be(HttpStatusCode.OK);
        (await commented.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("posts")[0].GetProperty("comments").GetArrayLength().Should().Be(1);

        var edited = await author.PutAsJsonAsync($"/api/events/{eventId}/discussion/posts/{postId}", new { title = "Farda", body = "Capas pretas." });
        edited.StatusCode.Should().Be(HttpStatusCode.OK);
        var after = (await edited.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("posts")[0];
        after.GetProperty("title").GetString().Should().Be("Farda");
        after.GetProperty("edited").GetBoolean().Should().BeTrue();
    }

    [Theory]
    [InlineData("", "", "body")]
    [InlineData("", "ok", "body")]
    [InlineData("ab", "Texto válido", "title")]
    public async Task InvalidPosts_AreFieldErrors(string title, string body, string field)
    {
        var eventId = await AddEventAsync("Validação");
        var (member, _) = await SignInAsync();
        await WithTokenAsync(member);

        var response = await member.PostAsJsonAsync($"/api/events/{eventId}/discussion/posts", new { title, body });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty(field, out _).Should().BeTrue();
        (await PostCountAsync(eventId)).Should().Be(0);
    }

    [Fact]
    public async Task TooLongTextsAndEmptyComments_AreRefused()
    {
        var eventId = await AddEventAsync("Limites");
        var (member, _) = await SignInAsync();
        await WithTokenAsync(member);

        (await member.PostAsJsonAsync($"/api/events/{eventId}/discussion/posts", new { title = "", body = new string('a', 5001) }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await member.PostAsJsonAsync($"/api/events/{eventId}/discussion/posts", new { title = new string('t', 121), body = "Texto" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var postId = await PostAsync(member, eventId, "Uma nota");
        (await member.PostAsJsonAsync($"/api/events/{eventId}/discussion/posts/{postId}/comments", new { body = "   " }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await member.PostAsJsonAsync($"/api/events/{eventId}/discussion/posts/{postId}/comments", new { body = new string('c', 2001) }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task OthersPosts_OnlyTheOwnerDeletes_AdminPinsAndLocks_AndALockedPostTakesNoComment()
    {
        var eventId = await AddEventAsync("Moderação");
        var (author, _) = await SignInAsync();
        var (other, _) = await SignInAsync();
        var (admin, _) = await SignInAsync("Admin");
        var (owner, _) = await SignInAsync("Owner");
        foreach (var c in new[] { author, other, admin, owner })
        {
            await WithTokenAsync(c);
        }

        var postId = await PostAsync(author, eventId, "Nota do autor");
        var post = $"/api/events/{eventId}/discussion/posts/{postId}";

        (await other.PutAsJsonAsync(post, new { title = "", body = "Mudado" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await other.DeleteAsync(post)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.DeleteAsync(post)).StatusCode.Should().Be(HttpStatusCode.Forbidden, "only the Owner deletes others' posts, as before");
        (await other.PutAsJsonAsync($"{post}/flags", new { pinned = true, locked = false })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var locked = await admin.PutAsJsonAsync($"{post}/flags", new { pinned = true, locked = true });
        locked.StatusCode.Should().Be(HttpStatusCode.OK);
        var flags = (await locked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("posts")[0];
        flags.GetProperty("pinned").GetBoolean().Should().BeTrue();
        flags.GetProperty("canComment").GetBoolean().Should().BeFalse();
        (await other.PostAsJsonAsync($"{post}/comments", new { body = "Posso?" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var deleted = await owner.DeleteAsync(post);
        deleted.StatusCode.Should().Be(HttpStatusCode.OK);
        (await deleted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("posts").GetArrayLength().Should().Be(0);
        (await PostCountAsync(eventId)).Should().Be(1, "a delete is soft, as before");
    }

    [Fact]
    public async Task Comments_AuthorEditsAndDeletes_OthersCannot_AndIdsOnlyCountUnderTheirEvent()
    {
        var eventId = await AddEventAsync("Comentários");
        var otherEvent = await AddEventAsync("Outra atuação");
        var (author, _) = await SignInAsync();
        var (other, _) = await SignInAsync();
        await WithTokenAsync(author);
        await WithTokenAsync(other);

        var postId = await PostAsync(author, eventId, "Nota");
        var withComment = await other.PostAsJsonAsync($"/api/events/{eventId}/discussion/posts/{postId}/comments", new { body = "Comentário" });
        var commentId = (await withComment.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("posts")[0].GetProperty("comments")[0].GetProperty("id").GetInt32();

        (await author.PutAsJsonAsync($"/api/events/{eventId}/discussion/comments/{commentId}", new { body = "Não é meu" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await author.DeleteAsync($"/api/events/{eventId}/discussion/comments/{commentId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await other.DeleteAsync($"/api/events/{otherEvent}/discussion/comments/{commentId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.DeleteAsync($"/api/events/{otherEvent}/discussion/posts/{postId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await other.PutAsJsonAsync($"/api/events/{eventId}/discussion/comments/{commentId}", new { body = "Corrigido" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var gone = await other.DeleteAsync($"/api/events/{eventId}/discussion/comments/{commentId}");
        gone.StatusCode.Should().Be(HttpStatusCode.OK);
        (await gone.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("posts")[0].GetProperty("comments").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task ALift_TheDriverSeatsMembers_OthersCannot_AndAPassengerMayLeave()
    {
        var eventId = await AddEventAsync("Boleias");
        var (driver, _) = await SignInAsync();
        var (rider, riderUser) = await SignInAsync();
        await WithTokenAsync(driver);
        await WithTokenAsync(rider);

        (await driver.PostAsJsonAsync($"/api/events/{eventId}/discussion/transport", new { vehicle = "", seats = 1, notes = "" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var offered = await driver.PostAsJsonAsync($"/api/events/{eventId}/discussion/transport", new { vehicle = "Clio cinzento", seats = 2, notes = "Saída às 18h" });
        offered.StatusCode.Should().Be(HttpStatusCode.OK);
        var post = (await offered.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("posts")[0];
        post.GetProperty("transport").GetProperty("canManage").GetBoolean().Should().BeTrue();
        var postId = post.GetProperty("id").GetInt32();
        var lift = $"/api/events/{eventId}/discussion/posts/{postId}";

        (await rider.GetAsync($"{lift}/passengers/members?q=x")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await rider.PostAsJsonAsync($"{lift}/passengers", new { userId = riderUser.Id })).StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "only the driver (or Admin/Owner) seats people, as before");

        var found = await driver.GetFromJsonAsync<JsonElement>($"{lift}/passengers/members?q={riderUser.UserName}");
        found.EnumerateArray().Should().Contain(m => m.GetProperty("id").GetString() == riderUser.Id);
        var seated = await driver.PostAsJsonAsync($"{lift}/passengers", new { userId = riderUser.Id });
        seated.StatusCode.Should().Be(HttpStatusCode.OK);
        var passenger = (await seated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("posts")[0].GetProperty("transport").GetProperty("passengers")[0];
        (await driver.PostAsJsonAsync($"{lift}/passengers", new { userId = riderUser.Id })).StatusCode.Should().Be(HttpStatusCode.BadRequest, "already in the car");

        (await driver.PutAsJsonAsync($"{lift}/transport", new { vehicle = "Clio", seats = 2, notes = "" })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await rider.PutAsJsonAsync($"{lift}/transport", new { vehicle = "Meu", seats = 3, notes = "" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var left = await rider.DeleteAsync($"{lift}/passengers/{passenger.GetProperty("id").GetInt32()}");
        left.StatusCode.Should().Be(HttpStatusCode.OK);
        (await left.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("posts")[0].GetProperty("transport").GetProperty("passengers")
            .GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task APost_OnAnUpcomingEvent_NotifiesGoingMembers_ThroughTheFake()
    {
        var eventId = await AddEventAsync("Aviso de nota", DateTime.Today.AddDays(30));
        var (author, _) = await SignInAsync();
        var (_, going) = await SignInAsync();
        await AddEnrollmentAsync(eventId, going.Id);
        await WithTokenAsync(author);

        await PostAsync(author, eventId, "Ensaio extra na quinta");

        _factory.Push.Verify(p => p.SendToUserAsync(going.Id, It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()), Times.Once);
    }

    // ---------- contacts ----------

    [Fact]
    public async Task Contacts_AreModAndAbove_ReadAndWrite()
    {
        var eventId = await AddEventAsync("Contactos");
        var (member, _) = await SignInAsync();
        var (mod, _) = await SignInAsync("Mod");
        var (_, called) = await SignInAsync();
        await SetPhoneAsync(called.Id, "912 345 678");
        await WithTokenAsync(member);

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

    [Theory]
    [InlineData("Mod")]
    [InlineData("Admin")]
    [InlineData("Owner")]
    public async Task ModAndAbove_RecordACall_ThatSyncsTheEnrollment_AndResetUndoesBoth(string role)
    {
        var eventId = await AddEventAsync($"Chamadas {role}");
        var (caller, _) = await SignInAsync(role);
        var (_, called) = await SignInAsync();
        await WithTokenAsync(caller);
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

    // ---------- the pages ----------

    [Theory]
    [InlineData("discussion")]
    [InlineData("contacts")]
    public async Task ASignedInMember_GetsTheReactShell_NotABlazorPage(string page)
    {
        var eventId = await AddEventAsync($"Página {page}");
        var (member, _) = await SignInAsync();

        var response = await member.GetAsync($"/events/{eventId}/{page}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoCache.Should().BeTrue();
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("id=\"root\"").And.NotContain("blazor.web.js");
    }

    // ---------- helpers ----------

    private async Task<int> PostAsync(HttpClient client, int eventId, string body)
    {
        var response = await client.PostAsJsonAsync($"/api/events/{eventId}/discussion/posts", new { title = "", body });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("posts").EnumerateArray()
            .Single(p => p.GetProperty("body").GetString() == body).GetProperty("id").GetInt32();
    }

    private async Task<int> AddEventAsync(string name, DateTime? date = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var e = Event.Create(name, date ?? DateTime.Today.AddDays(-5), "Bragança", EventType.Atuacao, "");
        db.Events.Add(e);
        await db.SaveChangesAsync();
        return e.Id;
    }

    private async Task<int> PostCountAsync(int eventId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Posts.CountAsync(p => p.Discussion.EventId == eventId);
    }

    private async Task SetPhoneAsync(string userId, string phone)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Users.SingleAsync(u => u.Id == userId)).PhoneNumber = phone;
        await db.SaveChangesAsync();
    }

    private async Task AddEnrollmentAsync(int eventId, string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Enrollments.Add(Enrollment.Create(userId, eventId));
        await db.SaveChangesAsync();
    }

    private async Task<Enrollment?> EnrollmentAsync(int eventId, string userId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Enrollments.AsNoTracking()
            .FirstOrDefaultAsync(e => e.EventId == eventId && e.UserId == userId);
    }

    private static int _ip;

    private Task<(HttpClient Client, ApplicationUser User)> SignInAsync(string? role = null)
    {
        var n = Interlocked.Increment(ref _ip);
        return CookieTestSession.SignInAsync(_factory, $"talk{Guid.NewGuid():N}"[..20], $"10.73.{n / 250}.{n % 250 + 1}", role);
    }

    private HttpClient Anonymous()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, "10.74.0.1");
        return client;
    }

    private static async Task WithTokenAsync(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/public/antiforgery-token");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }
}
