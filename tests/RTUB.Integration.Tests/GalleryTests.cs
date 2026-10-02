using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Integration.Tests;

/// <summary>
/// The React /gallery (React track 009) over HTTP, on the real SQLite test database: what a visitor
/// and a signed-in member actually receive from GET /api/gallery, and where the members' Blazor tools
/// went. Media rows point at fake https URLs; no storage is ever called.
/// </summary>
public class GalleryTests : IntegrationTestBase
{
    private const string PrivateUrl = "https://pub-test.r2.dev/images/Test/gallery/image/members-only-secret.jpg";
    private const string PrivateTitle = "Jantar só de membros";
    private const string TaggedNickname = "Pessoa Marcada";

    private static readonly SemaphoreSlim Seeded = new(1, 1);
    private static (int PublicId, int PrivateId)? _ids;

    public GalleryTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Visitor_GetsPublicItemsOnly_AndNothingOfTheMembersOnlyOnes()
    {
        await SeedAsync();

        var response = await Factory.CreateClient().GetAsync("/api/gallery");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue("the answer depends on the session");

        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain(PrivateUrl).And.NotContain(PrivateTitle).And.NotContain("members-only-secret")
            .And.NotContain(TaggedNickname, "visitors never learn who is in a photo")
            .And.NotContainAny(new[] { "uploader", "userId", "email", "isPrivate", "createdBy", "thumbnailUrl", "takenAt" });

        var body = JsonDocument.Parse(raw).RootElement;
        body.GetProperty("isMember").GetBoolean().Should().BeFalse();
        var items = body.GetProperty("items").EnumerateArray().ToList();
        items.Should().NotBeEmpty().And.OnlyContain(i => !i.GetProperty("membersOnly").GetBoolean());
        items.Should().OnlyContain(i => i.GetProperty("people").GetArrayLength() == 0);
        body.GetProperty("people").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Visitor_CannotOpenAMembersOnlyItemDirectly()
    {
        var (publicId, privateId) = await SeedAsync();
        var client = Factory.CreateClient();

        var hidden = await client.GetAsync($"/api/gallery/items/{privateId}");
        hidden.StatusCode.Should().Be(HttpStatusCode.NotFound, "a members-only item looks exactly like a missing one");
        (await hidden.Content.ReadAsStringAsync()).Should().NotContain(PrivateUrl);

        (await client.GetAsync($"/api/gallery/items/{publicId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/gallery/items/987654")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SignedInMember_GetsMembersOnlyItems_AndWhoIsInThem()
    {
        var (_, privateId) = await SeedAsync();
        var client = await SignedInClientAsync();

        var body = JsonDocument.Parse(await client.GetStringAsync("/api/gallery")).RootElement;
        body.GetProperty("isMember").GetBoolean().Should().BeTrue();

        var item = body.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetInt32() == privateId);
        item.GetProperty("membersOnly").GetBoolean().Should().BeTrue();
        item.GetProperty("url").GetString().Should().Be(PrivateUrl);
        item.GetProperty("people")[0].GetProperty("name").GetString().Should().Be(TaggedNickname);

        (await client.GetAsync($"/api/gallery/items/{privateId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>015: the API takes writes now, but never from a visitor or without the antiforgery token.</summary>
    [Fact]
    public async Task Api_Writes_AreRefusedToVisitorsAndWithoutTheToken()
    {
        var client = Factory.CreateClient();

        (await client.PostAsync("/api/gallery", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest, "no antiforgery token");
        (await client.DeleteAsync("/api/gallery/items/1")).StatusCode.Should().Be(HttpStatusCode.BadRequest, "no antiforgery token");
        (await client.PutAsync("/api/gallery/items/1", null)).StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task Gallery_IsTheReactShell()
    {
        var html = await Factory.CreateClient().GetStringAsync("/gallery?item=1");

        html.Should().Contain("id=\"root\"").And.NotContain("blazor.web.js").And.NotContain(PrivateUrl);
    }

    /// <summary>015: management is React (upload, edit, delete, tags on /gallery); /member/gallery only redirects.</summary>
    [Fact]
    public async Task MembersGallery_IsRetired_AndRedirectsToTheReactGallery()
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/member/gallery");
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Be("/gallery");
        (await client.PostAsync("/member/gallery", null)).StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);

        typeof(RTUB.App).Assembly.GetType("RTUB.Pages.Members.MemberGallery").Should().BeNull("the Blazor management page was retired");
        var src = Path.Combine(RepoRoot(), "src", "RTUB.Web", "portal", "src");
        File.ReadAllText(Path.Combine(src, "Gallery.tsx")).Should().Contain("<UploadDialog").And.Contain("<EditDialog").And.Contain("open.canEdit");
        File.ReadAllText(Path.Combine(src, "GalleryManage.tsx")).Should().NotContainAny(new[] { "migra", "inscri", "presen" });
    }

    [Fact]
    public void PortalLinks_PointAtTheReactGallery()
    {
        var src = Path.Combine(RepoRoot(), "src", "RTUB.Web", "portal", "src");

        File.ReadAllText(Path.Combine(src, "content.ts")).Should().Contain("gallery: '/gallery'")
            .And.NotContain("memberGallery", "nothing links to the retired /member/gallery (015)");
        File.ReadAllText(Path.Combine(src, "Home.tsx")).Should().Contain("<MoreLink href={portal.gallery}>");
        File.ReadAllText(Path.Combine(src, "App.tsx")).Should().Contain("gallery: portal.gallery",
            "the top bar, menu and footer open /gallery, not the home anchor");
        Directory.GetFiles(src).Select(File.ReadAllText).Should().NotContain(t => t.Contains("legacy.gallery"));
    }

    // ---------- helpers ----------

    /// <summary>One public photo and one members-only photo with a tagged member, once per class.</summary>
    private async Task<(int PublicId, int PrivateId)> SeedAsync()
    {
        await Seeded.WaitAsync();
        try
        {
            if (_ids is { } ids && await ExistsAsync(ids.PrivateId))
            {
                return ids;
            }

            using var scope = Factory.Services.CreateScope();
            await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();

            var uploader = new ApplicationUser { UserName = "gal-" + Guid.NewGuid().ToString("N")[..8], Email = "uploader@test.com", Nickname = "Uploader", FirstName = "U", LastName = "P" };
            var tagged = new ApplicationUser { UserName = "gal-" + Guid.NewGuid().ToString("N")[..8], Email = "tagged@test.com", Nickname = TaggedNickname, FirstName = "T", LastName = "G" };
            db.Users.AddRange(uploader, tagged);

            var shown = GalleryMedia.Create(uploader.Id, "Serenata pública", MediaType.Image, "https://pub-test.r2.dev/images/Test/gallery/image/public.jpg", 2024, 5, 1, isPrivate: false);
            var hidden = GalleryMedia.Create(uploader.Id, PrivateTitle, MediaType.Image, PrivateUrl, 2019, 3, null, isPrivate: true);
            db.GalleryMedia.AddRange(shown, hidden);
            await db.SaveChangesAsync();

            db.GalleryMediaPersonTags.Add(GalleryMediaPersonTag.Create(hidden.Id, tagged.Id));
            db.GalleryMediaPersonTags.Add(GalleryMediaPersonTag.Create(shown.Id, tagged.Id));
            await db.SaveChangesAsync();

            _ids = (shown.Id, hidden.Id);
            return _ids.Value;
        }
        finally
        {
            Seeded.Release();
        }
    }

    private async Task<bool> ExistsAsync(int id)
    {
        using var scope = Factory.Services.CreateScope();
        await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();
        return await db.GalleryMedia.AnyAsync(m => m.Id == id);
    }

    /// <summary>A real session: a confirmed member signed in through POST /auth/login.</summary>
    private async Task<HttpClient> SignedInClientAsync()
    {
        var password = TestSecret.NewPassword();
        var userName = "gal-member-" + Guid.NewGuid().ToString("N")[..8];
        using (var scope = Factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = userName, Email = userName + "@test.com", EmailConfirmed = true, Nickname = "Membro", FirstName = "M", LastName = "B" };
            (await users.CreateAsync(user, password)).Succeeded.Should().BeTrue();
        }

        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, "10.90.0.1");
        var token = await AntiforgeryFormToken.FetchAsync(client);
        var login = await client.PostAsync("/auth/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [AntiforgeryFormToken.FieldName] = token,
            ["Username"] = userName,
            ["Password"] = password,
            ["RememberMe"] = "false"
        }));
        login.StatusCode.Should().Be(HttpStatusCode.Redirect);
        return client;
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src", "RTUB.Web")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find the repository root");
    }
}
