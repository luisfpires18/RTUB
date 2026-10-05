using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// The public "Novidades" feed on /api/news (React track 025, docs/react-news.md) through the real host: real login,
/// antiforgery and SQLite. Everyone reads published posts; drafts and every write are Admin/Owner only; the public never
/// learns who wrote a post. Push and e-mail are the recording mocks of <see cref="EventsApiFactory"/>: nothing is sent.
/// The class shares one database, so each test tags its own posts and reads only those.
/// </summary>
public class NewsApiTests : IClassFixture<EventsApiFactory>
{
    private readonly EventsApiFactory _factory;

    public NewsApiTests(EventsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Visitors_ReadPublishedPostsOnly_NewestFirst_SignedRtub_AndTheRouteIsPublicReact()
    {
        var tag = Tag();
        var (_, author) = await SignInAsync("Admin");
        var older = await AddAsync($"{tag} antiga", "Texto antigo", DateTime.UtcNow.AddDays(-2), author.Id);
        var newer = await AddAsync($"{tag} nova", "Texto novo", DateTime.UtcNow.AddDays(-1), author.Id);
        await AddAsync($"{tag} rascunho", "Texto do rascunho secreto", null, author.Id);

        var anonymous = Anonymous();
        var response = await anonymous.GetAsync("/api/news?pageSize=20");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain("rascunho secreto", "drafts never reach the public")
            .And.NotContain(author.Id).And.NotContain(author.UserName!).And.NotContain(author.FirstName)
            .And.NotContainAny("authorId", "createdBy", "updatedBy", "userName", "email");

        var feed = JsonDocument.Parse(raw).RootElement;
        feed.GetProperty("canManage").GetBoolean().Should().BeFalse();
        feed.GetProperty("drafts").GetArrayLength().Should().Be(0);
        var mine = Tagged(feed.GetProperty("posts"), tag);
        mine.Select(p => p.GetProperty("id").GetInt32()).Should().Equal(newer, older);
        mine.Should().OnlyContain(p => p.GetProperty("authorName").ValueKind == JsonValueKind.Null);
        mine[0].GetProperty("body").GetString().Should().Be("Texto novo");
        mine[0].GetProperty("publishedAt").GetString().Should().EndWith("Z", "times are UTC for the reader's clock");

        var page = await anonymous.GetAsync("/news");
        page.StatusCode.Should().Be(HttpStatusCode.OK, "the feed is public");
        (await page.Content.ReadAsStringAsync()).Should().Contain("id=\"root\"").And.NotContain("blazor.web.js");
        (await anonymous.PostAsync("/news", null)).StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Member")]
    [InlineData("Mod")]
    public async Task VisitorsMembersAndMods_CannotManage(string? role)
    {
        var id = await AddAsync($"{Tag()} intocável", "Fica igual.", DateTime.UtcNow, null);
        var client = role is null ? Anonymous() : (await SignInAsync(role)).Client;
        await WithTokenAsync(client);
        var expected = role is null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;

        (await client.PostAsJsonAsync("/api/news", Input("Novo", "Texto", publish: true))).StatusCode.Should().Be(expected);
        (await client.PutAsJsonAsync($"/api/news/{id}", Input("Mudado", "Mudado"))).StatusCode.Should().Be(expected);
        (await client.PostAsync($"/api/news/{id}/unpublish", null)).StatusCode.Should().Be(expected);
        (await client.PostAsync($"/api/news/{id}/publish", null)).StatusCode.Should().Be(expected);
        (await client.DeleteAsync($"/api/news/{id}")).StatusCode.Should().Be(expected);

        var post = await PostAsync(id);
        post!.Body.Should().Be("Fica igual.");
        post.Title.Should().EndWith("intocável");
        post.PublishedAt.Should().NotBeNull();

        var feed = await Json(client, "/api/news");
        feed.GetProperty("canManage").GetBoolean().Should().BeFalse();
        feed.GetProperty("drafts").GetArrayLength().Should().Be(0);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Owner")]
    public async Task AdminAndOwner_WriteDrafts_Publish_Unpublish_Republish_Edit_AndDelete(string role)
    {
        var tag = Tag();
        var (manager, me) = await SignInAsync(role);
        await WithTokenAsync(manager);
        var anonymous = Anonymous();

        // A draft: only managers see it, with the author's display name.
        var created = await manager.PostAsJsonAsync("/api/news", Input($"  {tag} anúncio  ", "  Ensaio aberto na sexta.\nTragam capa.  "));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var draft = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = draft.GetProperty("id").GetInt32();
        created.Headers.Location!.ToString().Should().Be($"/api/news/{id}");
        draft.GetProperty("title").GetString().Should().Be($"{tag} anúncio", "title and text are trimmed");
        draft.GetProperty("body").GetString().Should().Be("Ensaio aberto na sexta.\nTragam capa.");
        draft.GetProperty("publishedAt").ValueKind.Should().Be(JsonValueKind.Null);
        draft.GetProperty("authorName").GetString().Should().Contain(me.FirstName);
        (await PostAsync(id))!.AuthorId.Should().Be(me.Id);

        var mine = await Json(manager, "/api/news");
        mine.GetProperty("canManage").GetBoolean().Should().BeTrue();
        Tagged(mine.GetProperty("drafts"), tag).Should().ContainSingle();
        Tagged(mine.GetProperty("posts"), tag).Should().BeEmpty();
        Tagged((await Json(anonymous, "/api/news")).GetProperty("posts"), tag).Should().BeEmpty();

        // Publish: on top of the public feed. Publishing again leaves the date alone.
        var first = await PublishAsync(manager, id);
        Tagged((await Json(anonymous, "/api/news")).GetProperty("posts"), tag).Should().ContainSingle();
        (await Json(anonymous, "/api/news")).GetProperty("posts")[0].GetProperty("id").GetInt32().Should().Be(id);
        (await PublishAsync(manager, id)).Should().Be(first);

        // Another post goes above it; unpublishing takes it off the feed; re-publishing puts it back on top with a new date.
        await AddAsync($"{tag} outra", "Outra", DateTime.UtcNow, null);
        var unpublished = await Json(await manager.PostAsync($"/api/news/{id}/unpublish", null));
        unpublished.GetProperty("publishedAt").ValueKind.Should().Be(JsonValueKind.Null);
        Tagged((await Json(anonymous, "/api/news")).GetProperty("posts"), tag).Select(p => p.GetProperty("id").GetInt32()).Should().NotContain(id);
        var again = await PublishAsync(manager, id);
        again.Should().BeAfter(first);
        (await Json(anonymous, "/api/news")).GetProperty("posts")[0].GetProperty("id").GetInt32().Should().Be(id);

        // Edit keeps the state; a blank title means none.
        var edited = await Json(await manager.PutAsJsonAsync($"/api/news/{id}", Input("   ", "Texto corrigido", publish: false)));
        edited.GetProperty("title").ValueKind.Should().Be(JsonValueKind.Null);
        edited.GetProperty("body").GetString().Should().Be("Texto corrigido");
        edited.GetProperty("publishedAt").GetDateTime().Should().Be(again, "editing never unpublishes");

        // Delete is permanent.
        (await manager.DeleteAsync($"/api/news/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await PostAsync(id)).Should().BeNull();
        (await manager.DeleteAsync($"/api/news/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await manager.PutAsJsonAsync($"/api/news/{id}", Input("x", "y"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await manager.PostAsync($"/api/news/{id}/publish", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Created as published straight away ("Publicar" in the form).
        var live = await manager.PostAsJsonAsync("/api/news", Input($"{tag} direta", "Já publicada", publish: true));
        live.StatusCode.Should().Be(HttpStatusCode.Created);
        (await live.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("publishedAt").ValueKind.Should().Be(JsonValueKind.String);

        _factory.Push.Invocations.Should().BeEmpty("publishing sends nothing");
        _factory.Email.Invocations.Should().BeEmpty("publishing sends nothing");
    }

    [Fact]
    public async Task EveryWrite_NeedsTheAntiforgeryToken()
    {
        var id = await AddAsync($"{Tag()} protegida", "Sem token não muda.", DateTime.UtcNow, null);
        var (admin, _) = await SignInAsync("Admin");

        (await admin.PostAsJsonAsync("/api/news", Input("x", "y"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PutAsJsonAsync($"/api/news/{id}", Input("x", "y"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsync($"/api/news/{id}/unpublish", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsync($"/api/news/{id}/publish", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.DeleteAsync($"/api/news/{id}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var post = await PostAsync(id);
        (post!.Body, post.PublishedAt.HasValue).Should().Be(("Sem token não muda.", true));
    }

    [Fact]
    public async Task Input_IsValidated_TextRequired_AndBothLengthsCapped()
    {
        var (admin, _) = await SignInAsync("Owner");
        await WithTokenAsync(admin);
        var before = await CountAsync();

        (await Errors(await admin.PostAsJsonAsync("/api/news", Input("Título", "   ")))).Should().ContainKey("body");
        (await Errors(await admin.PostAsJsonAsync("/api/news", new { title = "Só título" }))).Should().ContainKey("body");
        (await Errors(await admin.PostAsJsonAsync("/api/news", Input("Título", new string('a', 5001))))).Should().ContainKey("body");
        (await Errors(await admin.PostAsJsonAsync("/api/news", Input(new string('t', 151), "Texto")))).Should().ContainKey("title");
        (await CountAsync()).Should().Be(before, "nothing invalid is stored");

        var longest = await admin.PostAsJsonAsync("/api/news", Input(new string('t', 150), new string('a', 5000)));
        longest.StatusCode.Should().Be(HttpStatusCode.Created, "150 and 5000 characters are the limits, inclusive");

        (await Anonymous().GetAsync("/api/news?page=0")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Feed_IsPaged_WithAClampedPageSize()
    {
        var (admin, _) = await SignInAsync("Admin");
        await WithTokenAsync(admin);
        for (var i = 0; i < 3; i++)
        {
            await AddAsync($"{Tag()} página", "Texto", DateTime.UtcNow.AddDays(-30 - i), null);
        }

        var total = await PublishedCountAsync();
        var first = await Json(Anonymous(), "/api/news?page=1&pageSize=2");
        first.GetProperty("posts").GetArrayLength().Should().Be(2);
        first.GetProperty("hasMore").GetBoolean().Should().Be(total > 2);

        var ids = new List<int>();
        for (var page = 1; ; page++)
        {
            var feed = await Json(Anonymous(), $"/api/news?page={page}&pageSize=2");
            ids.AddRange(feed.GetProperty("posts").EnumerateArray().Select(p => p.GetProperty("id").GetInt32()));
            if (!feed.GetProperty("hasMore").GetBoolean())
            {
                break;
            }
        }

        ids.Should().OnlyHaveUniqueItems().And.HaveCount(total, "the pages walk the whole feed once");
        (await Json(Anonymous(), "/api/news?pageSize=500")).GetProperty("posts").GetArrayLength().Should().BeLessThanOrEqualTo(20);
        (await Json(admin, "/api/news?page=2&pageSize=1")).GetProperty("drafts").GetArrayLength().Should().Be(0, "drafts come with the first page only");
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
        var feed = await Json(owner, "/api/news?pageSize=20");
        feed.GetProperty("posts").EnumerateArray().Single(p => p.GetProperty("id").GetInt32() == id)
            .GetProperty("authorName").ValueKind.Should().Be(JsonValueKind.Null);
    }

    // ---------- helpers ----------

    private static string Tag() => "news" + Guid.NewGuid().ToString("N")[..8];

    private static object Input(string? title, string? body, bool publish = false) => new { title, body, publish };

    private static List<JsonElement> Tagged(JsonElement posts, string tag) =>
        posts.EnumerateArray().Where(p => p.GetProperty("title").GetString()?.StartsWith(tag, StringComparison.Ordinal) == true).ToList();

    private static async Task<DateTime> PublishAsync(HttpClient client, int id)
    {
        var post = await Json(await client.PostAsync($"/api/news/{id}/publish", null));
        return post.GetProperty("publishedAt").GetDateTime();
    }

    private static async Task<JsonElement> Json(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.StatusCode.Should().Be(HttpStatusCode.OK, path);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<Dictionary<string, string[]>> Errors(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("errors").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.EnumerateArray().Select(v => v.GetString()!).ToArray());
    }

    private async Task<int> AddAsync(string? title, string body, DateTime? publishedAt, string? authorId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var post = new NewsPost { Title = title, Body = body, PublishedAt = publishedAt, AuthorId = authorId };
        db.NewsPosts.Add(post);
        await db.SaveChangesAsync();
        return post.Id;
    }

    private async Task<NewsPost?> PostAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().NewsPosts.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id);
    }

    private async Task<int> CountAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().NewsPosts.CountAsync();
    }

    private async Task<int> PublishedCountAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().NewsPosts.CountAsync(p => p.PublishedAt != null);
    }

    private static int _ip;

    private Task<(HttpClient Client, ApplicationUser User)> SignInAsync(string? role = null)
    {
        var n = Interlocked.Increment(ref _ip);
        return CookieTestSession.SignInAsync(_factory, $"news{Guid.NewGuid():N}"[..20], $"10.91.{n / 250}.{n % 250 + 1}", role);
    }

    private HttpClient Anonymous()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, "10.92.0.1");
        return client;
    }

    private static async Task WithTokenAsync(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/public/antiforgery-token");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }
}
