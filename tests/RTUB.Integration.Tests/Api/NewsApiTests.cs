using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Core.Entities;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// The public "Novidades" feed on /api/news (React track 025, docs/react-news.md) through the real host: real login,
/// antiforgery and SQLite. Everyone reads published posts; drafts and every write are Admin/Owner only; the public never
/// learns who wrote a post. Push and e-mail are the recording mocks of <see cref="EventsApiFactory"/>: nothing is sent.
/// The class shares one database, so each test tags its own posts and reads only those.
/// </summary>
public partial class NewsApiTests : IClassFixture<EventsApiFactory>
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

    private async Task<int> AddAsync(string? title, string body, DateTime? publishedAt, string? authorId, bool isPublic = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var post = new NewsPost { Title = title, Body = body, PublishedAt = publishedAt, AuthorId = authorId, IsPublic = isPublic };
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
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().NewsPosts.CountAsync(p => p.PublishedAt != null && p.IsPublic);
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
