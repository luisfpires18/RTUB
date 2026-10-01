using System.Net;
using System.Text.Json;
using FluentAssertions;
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
/// React track 010: the home previews read real data - the next events from
/// GET /api/public/events/upcoming and the latest public photos from GET /api/gallery?public=true -
/// and the public-shell copy changes. Read-only; no storage is called.
/// </summary>
public class HomePreviewTests : IntegrationTestBase
{
    private const string MembersOnlyPhoto = "https://pub-test.r2.dev/images/Test/gallery/image/home-preview-members.jpg";

    public HomePreviewTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    // ---------- upcoming events ----------

    [Fact]
    public async Task UpcomingEvents_AreEmptyWithoutEvents_ThenTheNextThreeInDateOrder_WithPublicFieldsOnly()
    {
        var client = Factory.CreateClient();

        // This class's database starts with no events: the home shows its empty state, never a fake date.
        (await GetJsonAsync(client, "/api/public/events/upcoming")).GetArrayLength().Should().Be(0);

        var today = DateTime.Today;
        await AddEventsAsync(
            Event("Já passou", today.AddDays(-10)),
            Event("Quarta", today.AddDays(30).AddHours(21)),
            Event("Segunda", today.AddDays(7).AddHours(21).AddMinutes(30)),
            Event("Primeira, de vários dias", today.AddDays(-1), endDate: today.AddDays(1)),
            Event("Terceira, cancelada", today.AddDays(14), cancelled: true));

        var response = await client.GetAsync("/api/public/events/upcoming");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain("descrição interna").And.NotContain("motivo interno")
            .And.NotContainAny(new[] { "\"id\"", "description", "cancellationReason", "imageUrl", "enrollments" });

        var events = JsonDocument.Parse(raw).RootElement.EnumerateArray().ToList();
        events.Select(e => e.GetProperty("name").GetString()).Should().Equal(
            "Primeira, de vários dias", "Segunda", "Terceira, cancelada");

        events[0].GetProperty("endDate").GetString().Should().Be(today.AddDays(1).ToString("yyyy-MM-dd"));
        events[0].GetProperty("time").ValueKind.Should().Be(JsonValueKind.Null, "a midnight start means no time was set");
        events[1].GetProperty("date").GetString().Should().Be(today.AddDays(7).ToString("yyyy-MM-dd"));
        events[1].GetProperty("time").GetString().Should().Be("21:30");
        events[1].GetProperty("type").GetString().Should().Be("Atuação");
        events[2].GetProperty("cancelled").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task UpcomingEvents_AreReadOnly()
    {
        (await Factory.CreateClient().PostAsync("/api/public/events/upcoming", null))
            .StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    // ---------- gallery preview ----------

    [Fact]
    public async Task GalleryPreview_IsPublicOnly_EvenForASignedInMember()
    {
        await AddMembersOnlyPhotoAsync();
        var member = await SignedInClientAsync();

        var full = await member.GetStringAsync("/api/gallery");
        full.Should().Contain(MembersOnlyPhoto, "a member's own gallery view still includes members-only photos");

        var preview = await member.GetStringAsync("/api/gallery?public=true&pageSize=5");
        preview.Should().NotContain(MembersOnlyPhoto).And.NotContain("\"membersOnly\":true");

        (await Factory.CreateClient().GetStringAsync("/api/gallery?public=true&pageSize=5"))
            .Should().NotContain(MembersOnlyPhoto);
    }

    // ---------- the home and shell sources ----------

    [Fact]
    public void Home_ReadsRealPreviews_AndNoLongerShowsTheIllustrationsOrTheStaticAgenda()
    {
        var home = Source("Home.tsx");

        home.Should().Contain("getUpcomingEvents()").And.Contain("events.slice(0, 3)", "at most three event cards");
        home.Should().Contain("getGalleryPreview(PREVIEW_PHOTOS)").And.Contain("!i.membersOnly");
        home.Should().NotContain("galleryTiles").And.NotContain("Ilustrações")
            .And.NotContain("As datas confirmadas estão na agenda completa.");
        Source("content.ts").Should().NotContain("galleryTiles").And.NotContain("instruments");
    }

    [Fact]
    public void Home_Copy_Follows010()
    {
        var home = Source("Home.tsx");

        home.Should().NotContain("Quatro discos, uma só voz").And.Contain("álbuns para ouvir e recordar");
        home.Should().NotContain("aria-label=\"Instrumentos\"", "the guitarra/bandolim/cavaquinho/voz tags are gone");
        home.Should().Contain("Ninguém nasce a tocar instrumentos. Aprende-se aqui.")
            .And.NotContain("Ninguém nasce a tocar bandolim");
        home.Should().NotContain("Quem conduz a tuna");
        home.Should().Contain("<MoreLink href={portal.roles}>").And.Contain("<MoreLink href={portal.gallery}>")
            .And.Contain("<MoreLink href={portal.music}>").And.Contain("<MoreLink href={legacy.events}>");
    }

    [Fact]
    public void Gallery_ShowsVisitorsNoMembersOnlyTeaser()
    {
        var gallery = Source("Gallery.tsx");

        gallery.Should().NotContain("reservadas a membros").And.NotContain("Entrar como membro");
    }

    [Fact]
    public void RequestForm_Copy_Follows010()
    {
        var request = Source("Request.tsx");

        request.Should().Contain("Definir intervalo de datas").And.NotContain("Ainda sem data fechada");
        request.Should().NotContain("Localidade e, se já souber, o sítio.");
        request.Should().Contain("label=\"Local *\"");
    }

    [Fact]
    public void SearchAndFilterFields_ShareOneControl_AndBackLinksOneStyle()
    {
        Source("Gallery.tsx").Should().Contain("className=\"control\"").And.Contain("className=\"control control--select\"");
        Source("Governance.tsx").Should().Contain("className=\"control control--select\"");
        Source("MusicAlbum.tsx").Should().Contain("className=\"control album-tools__search\"").And.Contain("className=\"back-link\"");
        Source("MusicUi.tsx").Should().Contain("control control--select control--sm");
        Source("Privacy.tsx").Should().Contain("className=\"back-link\"");

        var css = Source("styles.css");
        css.Should().Contain(".control {").And.Contain(".control:focus-within").And.Contain(".back-link {");
        css.Should().NotContain(".doc__back").And.NotContain(".gallery-tools__field").And.NotContain(".search {");
    }

    // ---------- helpers ----------

    private static string Source(string file) =>
        File.ReadAllText(Path.Combine(RepoRoot(), "src", "RTUB.Web", "portal", "src", file));

    private static Event Event(string name, DateTime date, DateTime? endDate = null, bool cancelled = false)
    {
        var e = RTUB.Core.Entities.Event.Create(name, date, "Bragança", EventType.Atuacao, "descrição interna");
        e.EndDate = endDate;
        e.IsCancelled = cancelled;
        e.CancellationReason = cancelled ? "motivo interno" : null;
        return e;
    }

    private async Task AddEventsAsync(params Event[] events)
    {
        using var scope = Factory.Services.CreateScope();
        await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();
        db.Events.AddRange(events);
        await db.SaveChangesAsync();
    }

    private async Task AddMembersOnlyPhotoAsync()
    {
        using var scope = Factory.Services.CreateScope();
        await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();
        if (await db.GalleryMedia.AnyAsync(m => m.MediaUrl == MembersOnlyPhoto))
        {
            return;
        }

        var uploader = new ApplicationUser { UserName = "home-" + Guid.NewGuid().ToString("N")[..8], Email = "u@test.com", Nickname = "U", FirstName = "U", LastName = "P" };
        db.Users.Add(uploader);
        db.GalleryMedia.Add(GalleryMedia.Create(uploader.Id, "Só membros", MediaType.Image, MembersOnlyPhoto, 2026, 1, 1, isPrivate: true));
        db.GalleryMedia.Add(GalleryMedia.Create(uploader.Id, "Pública", MediaType.Image, "https://pub-test.r2.dev/images/Test/gallery/image/home-preview-public.jpg", 2025, 6, 1, isPrivate: false));
        await db.SaveChangesAsync();
    }

    private async Task<HttpClient> SignedInClientAsync()
    {
        var password = TestSecret.NewPassword();
        var userName = "home-member-" + Guid.NewGuid().ToString("N")[..8];
        using (var scope = Factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = userName, Email = userName + "@test.com", EmailConfirmed = true, Nickname = "Membro", FirstName = "M", LastName = "B" };
            (await users.CreateAsync(user, password)).Succeeded.Should().BeTrue();
        }

        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, "10.91.0.1");
        var token = await AntiforgeryFormToken.FetchAsync(client);
        (await client.PostAsync("/auth/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [AntiforgeryFormToken.FieldName] = token,
            ["Username"] = userName,
            ["Password"] = password,
            ["RememberMe"] = "false"
        }))).StatusCode.Should().Be(HttpStatusCode.Redirect);
        return client;
    }

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
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
