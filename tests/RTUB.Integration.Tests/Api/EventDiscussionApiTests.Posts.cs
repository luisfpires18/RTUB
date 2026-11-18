// EventDiscussionApiTests: Posts tests.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Moq;

namespace RTUB.Integration.Tests.Api;

public partial class EventDiscussionApiTests
{
    [Fact]
    public async Task AMember_PostsComments_AndEditsOwnPosts_WithoutLeakingIds()
    {
        var eventId = await AddEventAsync("Conversa aberta");
        var (author, authorUser) = await SignInAsync();
        await AreaHttp.WithTokenAsync(author);

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
        await AreaHttp.WithTokenAsync(member);

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
        await AreaHttp.WithTokenAsync(member);

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
            await AreaHttp.WithTokenAsync(c);
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
        await AreaHttp.WithTokenAsync(author);
        await AreaHttp.WithTokenAsync(other);

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
        await AreaHttp.WithTokenAsync(driver);
        await AreaHttp.WithTokenAsync(rider);

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
        await AreaHttp.WithTokenAsync(author);

        await PostAsync(author, eventId, "Ensaio extra na quinta");

        _factory.Push.Verify(p => p.SendToUserAsync(going.Id, It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()), Times.Once);
    }
}
