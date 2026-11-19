// NewsApiTests: MembersOnly tests.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using RTUB.Core.Entities;

namespace RTUB.Integration.Tests.Api;

public partial class NewsApiTests
{
    [Fact]
    public async Task Feed_IsPaged_WithAClampedPageSize()
    {
        var (admin, _) = await SignInAsync("Admin");
        await AreaHttp.WithTokenAsync(admin);
        for (var i = 0; i < 3; i++)
        {
            await AddAsync($"{Tag()} página", "Texto", DateTime.UtcNow.AddDays(-30 - i), null);
        }

        var total = await PublishedCountAsync();
        var first = await AreaHttp.Json(Anonymous(), "/api/news?page=1&pageSize=2");
        first.GetProperty("posts").GetArrayLength().Should().Be(2);
        first.GetProperty("hasMore").GetBoolean().Should().Be(total > 2);

        var ids = new List<int>();
        for (var page = 1; ; page++)
        {
            var feed = await AreaHttp.Json(Anonymous(), $"/api/news?page={page}&pageSize=2");
            ids.AddRange(feed.GetProperty("posts").EnumerateArray().Select(p => p.GetProperty("id").GetInt32()));
            if (!feed.GetProperty("hasMore").GetBoolean())
            {
                break;
            }
        }

        ids.Should().OnlyHaveUniqueItems().And.HaveCount(total, "the pages walk the whole feed once");
        (await AreaHttp.Json(Anonymous(), "/api/news?pageSize=500")).GetProperty("posts").GetArrayLength().Should().BeLessThanOrEqualTo(20);
        (await AreaHttp.Json(admin, "/api/news?page=2&pageSize=1")).GetProperty("drafts").GetArrayLength().Should().Be(0, "drafts come with the first page only");
    }

    [Fact]
    public async Task DeletingTheAuthorsAccount_KeepsThePost_WithNoAuthor()
    {
        var (_, author) = await SignInAsync("Admin");
        var id = await AddAsync($"{Tag()} órfã", "Fica.", DateTime.UtcNow, author.Id);

        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            (await users.DeleteAsync((await users.FindByIdAsync(author.Id))!)).Succeeded.Should().BeTrue();
        }

        var post = await PostAsync(id);
        post.Should().NotBeNull("the post survives its author");
        post!.AuthorId.Should().BeNull();

        var (owner, _) = await SignInAsync("Owner");
        var feed = await AreaHttp.Json(owner, "/api/news?pageSize=20");
        feed.GetProperty("posts").EnumerateArray().Single(p => p.GetProperty("id").GetInt32() == id)
            .GetProperty("authorName").ValueKind.Should().Be(JsonValueKind.Null);
    }

    // ---------- #222: public / members only ----------

    [Fact]
    public async Task ANewPost_IsPublic_UnlessCreatedMembersOnly_AndOmittingTheFieldNeverMakesAPrivatePostPublic()
    {
        var tag = Tag();
        var (admin, _) = await SignInAsync("Admin");
        await AreaHttp.WithTokenAsync(admin);

        var plain = await (await admin.PostAsJsonAsync("/api/news", Input($"{tag} omitida", "Texto", publish: true))).Content.ReadFromJsonAsync<JsonElement>();
        plain.GetProperty("membersOnly").GetBoolean().Should().BeFalse();
        (await PostAsync(plain.GetProperty("id").GetInt32()))!.IsPublic.Should().BeTrue("existing behaviour: no choice = public");

        var explicitPublic = await (await admin.PostAsJsonAsync("/api/news", new { title = $"{tag} pública", body = "Texto", publish = true, membersOnly = false })).Content.ReadFromJsonAsync<JsonElement>();
        explicitPublic.GetProperty("membersOnly").GetBoolean().Should().BeFalse();

        var created = await admin.PostAsJsonAsync("/api/news", new { title = $"{tag} privada", body = "Texto", publish = true, membersOnly = true });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var privatePost = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = privatePost.GetProperty("id").GetInt32();
        privatePost.GetProperty("membersOnly").GetBoolean().Should().BeTrue();
        (await PostAsync(id))!.IsPublic.Should().BeFalse();

        // An update that does not mention the field (an older client) keeps it private.
        var edited = await Json(await admin.PutAsJsonAsync($"/api/news/{id}", new { title = $"{tag} privada", body = "Texto corrigido", publish = false }));
        edited.GetProperty("membersOnly").GetBoolean().Should().BeTrue();
        (await PostAsync(id))!.IsPublic.Should().BeFalse("omission must never declassify");
        (await PostAsync(id))!.Body.Should().Be("Texto corrigido");

        // Private -> public and back, only on an explicit choice.
        (await Json(await admin.PutAsJsonAsync($"/api/news/{id}", new { title = $"{tag} privada", body = "Texto", membersOnly = false })))
            .GetProperty("membersOnly").GetBoolean().Should().BeFalse();
        (await PostAsync(id))!.IsPublic.Should().BeTrue();
        (await Json(await admin.PutAsJsonAsync($"/api/news/{id}", new { title = $"{tag} privada", body = "Texto", membersOnly = true })))
            .GetProperty("membersOnly").GetBoolean().Should().BeTrue();
        (await PostAsync(id))!.IsPublic.Should().BeFalse();

        // Publish / unpublish leave the visibility alone.
        await admin.PostAsync($"/api/news/{id}/unpublish", null);
        await admin.PostAsync($"/api/news/{id}/publish", null);
        (await PostAsync(id))!.IsPublic.Should().BeFalse();
    }

    [Fact]
    public async Task Visitors_NeverReceiveAMembersOnlyPost_FromAnyFeedQuery_ButSignedInReadersDo()
    {
        var tag = Tag();
        var publicId = await AddAsync($"{tag} pública", "Texto público", DateTime.UtcNow.AddHours(-2), null);
        var privateId = await AddAsync($"{tag} privada", "Segredo só para membros", DateTime.UtcNow.AddHours(-1), null, isPublic: false);
        await AddAsync($"{tag} rascunho privado", "Rascunho secreto de membros", null, null, isPublic: false);

        // Visitors: whatever they ask for (list, page size, filters, the home preview's pageSize=3), the private post is not there.
        var anonymous = Anonymous();
        foreach (var path in new[]
                 {
                     "/api/news", "/api/news?pageSize=3", "/api/news?pageSize=20", "/api/news?visibility=private", "/api/news?visibility=PRIVATE",
                     "/api/news?visibility=public", "/api/news?visibility=all", "/api/news?visibility=%27%20OR%201%3D1--", "/api/news?q=Segredo&title=Segredo",
                 })
        {
            var response = await anonymous.GetAsync(path);
            response.StatusCode.Should().Be(HttpStatusCode.OK, path);
            var raw = await response.Content.ReadAsStringAsync();
            raw.Should().NotContain("Segredo só para membros").And.NotContain("Rascunho secreto").And.NotContain($"{tag} privada", path);
            var feed = JsonDocument.Parse(raw).RootElement;
            feed.GetProperty("signedIn").GetBoolean().Should().BeFalse("the page shows no tabs to visitors");
            feed.GetProperty("posts").EnumerateArray().Should().OnlyContain(p => !p.GetProperty("membersOnly").GetBoolean());
        }

        (await AreaHttp.Json(anonymous, "/api/news?pageSize=20")).GetProperty("posts").EnumerateArray()
            .Select(p => p.GetProperty("id").GetInt32()).Should().Contain(publicId, "public posts are unchanged");

        // A visitor asking for the private tab gets the public feed, so nothing reveals that hidden posts exist.
        (await AreaHttp.Json(anonymous, "/api/news?visibility=private&pageSize=20")).GetProperty("posts").EnumerateArray()
            .Select(p => p.GetProperty("id").GetInt32()).Should().Contain(publicId);

        // A signed-in member sees both, with the private badge data; no management data.
        var (member, _) = await SignInAsync("Member");
        var all = await AreaHttp.Json(member, "/api/news?pageSize=20");
        all.GetProperty("signedIn").GetBoolean().Should().BeTrue();
        all.GetProperty("canManage").GetBoolean().Should().BeFalse();
        var mine = Tagged(all.GetProperty("posts"), tag);
        mine.Select(p => p.GetProperty("id").GetInt32()).Should().Contain(new[] { publicId, privateId });
        mine.Single(p => p.GetProperty("id").GetInt32() == privateId).GetProperty("membersOnly").GetBoolean().Should().BeTrue();
        mine.Single(p => p.GetProperty("id").GetInt32() == publicId).GetProperty("membersOnly").GetBoolean().Should().BeFalse();
        mine.Should().OnlyContain(p => p.GetProperty("authorName").ValueKind == JsonValueKind.Null);
        all.GetProperty("drafts").GetArrayLength().Should().Be(0, "members never see drafts, private or not");

        // The tabs: Públicas / Privadas narrow it on the server.
        Tagged((await AreaHttp.Json(member, "/api/news?visibility=public&pageSize=20")).GetProperty("posts"), tag)
            .Select(p => p.GetProperty("id").GetInt32()).Should().Equal(publicId);
        Tagged((await AreaHttp.Json(member, "/api/news?visibility=private&pageSize=20")).GetProperty("posts"), tag)
            .Select(p => p.GetProperty("id").GetInt32()).Should().Equal(privateId);

        // Admin: the private draft is a draft for managers only.
        var (admin, _) = await SignInAsync("Admin");
        Tagged((await AreaHttp.Json(admin, "/api/news?visibility=private")).GetProperty("drafts"), tag)
            .Should().ContainSingle().Which.GetProperty("membersOnly").GetBoolean().Should().BeTrue();
        Tagged((await AreaHttp.Json(admin, "/api/news?visibility=public")).GetProperty("drafts"), tag).Should().BeEmpty();

        _factory.Push.Invocations.Should().BeEmpty("news sends no push, so no private text can reach a notification");
        _factory.Email.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task HomePreview_ThreeNewest_IsPublicOnlyForVisitors_AndFlagsPrivatePostsForMembers()
    {
        // The home "Últimas novidades" asks /api/news?pageSize=3 with the reader's session; a private post is the newest.
        var publicId = await AddAsync($"{Tag()} pública", "Texto", DateTime.UtcNow.AddYears(1).AddMinutes(-1), null);
        var privateId = await AddAsync($"{Tag()} privada", "Segredo da home", DateTime.UtcNow.AddYears(1), null, isPublic: false);

        var visitor = await (await Anonymous().GetAsync("/api/news?pageSize=3")).Content.ReadAsStringAsync();
        visitor.Should().NotContain("Segredo da home");
        JsonDocument.Parse(visitor).RootElement.GetProperty("posts").EnumerateArray().First().GetProperty("id").GetInt32().Should().Be(publicId);

        var (member, _) = await SignInAsync("Member");
        var posts = (await AreaHttp.Json(member, "/api/news?pageSize=3")).GetProperty("posts").EnumerateArray().ToList();
        posts[0].GetProperty("id").GetInt32().Should().Be(privateId);
        posts[0].GetProperty("membersOnly").GetBoolean().Should().BeTrue("the preview card needs the flag for its badge");
        posts[1].GetProperty("membersOnly").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task APrivatePost_HasNoDetailRoute_ForVisitors()
    {
        var id = await AddAsync($"{Tag()} só membros", "Segredo único de membros", DateTime.UtcNow, null, isPublic: false);
        var anonymous = Anonymous();

        // There is no per-post read endpoint: a guessed id answers like an unknown one and carries no data.
        var guessed = await anonymous.GetAsync($"/api/news/{id}");
        guessed.StatusCode.Should().NotBe(HttpStatusCode.OK);
        guessed.StatusCode.Should().Be((await anonymous.GetAsync("/api/news/987654321")).StatusCode);
        (await guessed.Content.ReadAsStringAsync()).Should().NotContain("Segredo");

        var shell = await anonymous.GetAsync($"/news#post-{id}");
        shell.StatusCode.Should().Be(HttpStatusCode.OK);
        (await shell.Content.ReadAsStringAsync()).Should().NotContain("Segredo");
    }
}
