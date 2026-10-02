using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// Gallery management on /api/gallery (React track 015) through the real host: real login, antiforgery,
/// SQLite and GalleryMediaService. Storage and push are the recording fakes of <see cref="EventsApiFactory"/>:
/// nothing is uploaded to, or deleted from, R2 and nothing is sent.
/// </summary>
public class GalleryManagementApiTests : IClassFixture<EventsApiFactory>
{
    private readonly EventsApiFactory _factory;

    public GalleryManagementApiTests(EventsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AMember_Uploads_WithTagsAndTheMembersOnlyFlag_AndTheTaggedPeopleArePushedThroughTheFake()
    {
        var (member, me) = await SignInAsync();
        var (_, tagged) = await SignInAsync();
        await WithTokenAsync(member);

        var response = await member.PostAsync("/api/gallery", Upload("Serenata no castelo", "2024-05-17", membersOnly: true, tagged.Id));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var item = await response.Content.ReadFromJsonAsync<JsonElement>();
        item.GetProperty("membersOnly").GetBoolean().Should().BeTrue();
        item.GetProperty("canEdit").GetBoolean().Should().BeTrue();
        item.GetProperty("people").EnumerateArray().Should().ContainSingle(p => p.GetProperty("id").GetString() == tagged.Id);
        var saved = await MediaAsync(item.GetProperty("id").GetInt32());
        saved!.UploaderId.Should().Be(me.Id);
        (saved.Year, saved.Month, saved.Day).Should().Be((2024, (byte?)5, (byte?)17));
        saved.MediaUrl.Should().StartWith("https://pub-test.r2.dev/", "the storage fake answered, nothing reached R2");
        _factory.Push.Verify(p => p.SendToSelectedUsersAsync(It.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { tagged.Id })),
            It.Is<SendPushNotificationDto>(n => n.Url == "/gallery")), Times.Once);
    }

    [Theory]
    [InlineData("2023-01-01", 2023, null, null)]
    [InlineData("2023-06-01", 2023, 6, null)]
    [InlineData("2023-06-09", 2023, 6, 9)]
    public async Task TheDate_KeepsOnlyWhatIsKnown_AsTheOldForm(string date, int year, int? month, int? day)
    {
        var (member, _) = await SignInAsync();
        await WithTokenAsync(member);

        var item = await (await member.PostAsync("/api/gallery", Upload("Ensaio", date, membersOnly: false))).Content.ReadFromJsonAsync<JsonElement>();

        var saved = await MediaAsync(item.GetProperty("id").GetInt32());
        saved!.Year.Should().Be(year);
        ((int?)saved.Month).Should().Be(month);
        ((int?)saved.Day).Should().Be(day);
        saved.IsPrivate.Should().BeFalse();
    }

    [Fact]
    public async Task InvalidUploads_AreFieldErrors_AndStoreNothing()
    {
        var (member, _) = await SignInAsync();
        await WithTokenAsync(member);
        var before = await CountAsync();

        (await member.PostAsync("/api/gallery", Upload("Folha", "2024-01-02", false, contentType: "application/pdf")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await member.PostAsync("/api/gallery", Upload(" ", "2024-01-02", false))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await member.PostAsync("/api/gallery", Upload(new string('t', 201), "2024-01-02", false))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await member.PostAsync("/api/gallery", Upload("Sem data", "", false))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await member.PostAsync("/api/gallery", Upload("Ano estranho", "1850-03-03", false))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await member.PostAsync("/api/gallery", Upload("Pessoa", "2024-01-02", false, "no-such-user"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        _factory.GalleryStorage.Setup(s => s.GetMaxFileSize(MediaType.Image)).Returns(4);
        try
        {
            var tooBig = await member.PostAsync("/api/gallery", Upload("Grande", "2024-01-02", false));
            tooBig.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await tooBig.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("file", out _).Should().BeTrue();
        }
        finally
        {
            _factory.GalleryStorage.Setup(s => s.GetMaxFileSize(MediaType.Image)).Returns(10 * 1024 * 1024);
        }

        (await CountAsync()).Should().Be(before);
    }

    [Fact]
    public async Task Writes_AreForSignedInMembers_WithTheAntiforgeryHeader()
    {
        var (member, _) = await SignInAsync();
        var anonymous = Anonymous();
        await WithTokenAsync(anonymous);
        var before = await CountAsync();

        (await anonymous.PostAsync("/api/gallery", Upload("Visita", "2024-01-02", false))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await member.PostAsync("/api/gallery", Upload("Sem token", "2024-01-02", false))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Anonymous().GetAsync("/api/gallery/people?q=a")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await CountAsync()).Should().Be(before);
    }

    [Fact]
    public async Task OnlyTheUploaderAdminOrOwner_EditAndDelete_AndModsAndOtherMembersCannot()
    {
        var (uploader, uploaderUser) = await SignInAsync();
        var (other, _) = await SignInAsync();
        var (mod, _) = await SignInAsync("Mod");
        var (owner, _) = await SignInAsync("Owner");
        foreach (var c in new[] { uploader, other, mod, owner })
        {
            await WithTokenAsync(c);
        }

        var id = await AddMediaAsync(uploaderUser.Id, isPrivate: true);
        var edit = new { title = "Novo título", date = "2022-11-05", membersOnly = false, personIds = Array.Empty<string>() };

        foreach (var refused in new[] { other, mod })
        {
            (await refused.PutAsJsonAsync($"/api/gallery/items/{id}", edit)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await refused.DeleteAsync($"/api/gallery/items/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await refused.GetAsync($"/api/gallery/items/{id}/edit")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        var listed = await other.GetFromJsonAsync<JsonElement>($"/api/gallery/items/{id}");
        listed.GetProperty("canEdit").GetBoolean().Should().BeFalse();

        (await uploader.PutAsJsonAsync($"/api/gallery/items/{id}", edit)).StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = await MediaAsync(id);
        saved!.Title.Should().Be("Novo título");
        saved.IsPrivate.Should().BeFalse("the members-only flag follows the edit");

        (await owner.PutAsJsonAsync($"/api/gallery/items/{id}", edit)).StatusCode.Should().Be(HttpStatusCode.OK,
            "the Owner inherits Admin (was Admin only)");
        (await owner.DeleteAsync($"/api/gallery/items/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await MediaAsync(id)).Should().BeNull();
        _factory.GalleryStorage.Verify(s => s.DeleteMediaAsync(saved.MediaUrl), Times.Once, "the stored file goes first, through the fake");
        (await owner.DeleteAsync($"/api/gallery/items/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Edit_AddsAndRemovesTags_WithoutNotifying()
    {
        var (uploader, uploaderUser) = await SignInAsync();
        var (_, kept) = await SignInAsync();
        var (_, removed) = await SignInAsync();
        var (_, added) = await SignInAsync();
        await WithTokenAsync(uploader);
        var id = await AddMediaAsync(uploaderUser.Id, isPrivate: true, kept.Id, removed.Id);

        var form = await uploader.GetFromJsonAsync<JsonElement>($"/api/gallery/items/{id}/edit");
        form.GetProperty("people").GetArrayLength().Should().Be(2);

        var found = await uploader.GetFromJsonAsync<JsonElement>($"/api/gallery/people?q={added.UserName}");
        found.EnumerateArray().Should().ContainSingle(p => p.GetProperty("id").GetString() == added.Id);
        found.GetRawText().Should().NotContain("@test.com").And.NotContain("phone");

        var response = await uploader.PutAsJsonAsync($"/api/gallery/items/{id}",
            new { title = "Foto", date = "2021-03-04", membersOnly = true, personIds = new[] { kept.Id, added.Id } });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await TagsAsync(id)).Should().BeEquivalentTo(new[] { kept.Id, added.Id });
        _factory.Push.Verify(p => p.SendToSelectedUsersAsync(It.Is<IEnumerable<string>>(ids => ids.Contains(added.Id)), It.IsAny<SendPushNotificationDto>()),
            Times.Never, "only tags made on upload notify, as before");
    }

    [Fact]
    public async Task Visitors_NeverSeeMembersOnlyItems_OrTheirUrls()
    {
        var (_, uploader) = await SignInAsync();
        var id = await AddMediaAsync(uploader.Id, isPrivate: true);
        var url = (await MediaAsync(id))!.MediaUrl;

        var page = await Anonymous().GetStringAsync("/api/gallery?pageSize=60");
        page.Should().NotContain(url);
        (await Anonymous().GetAsync($"/api/gallery/items/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Anonymous().GetAsync($"/api/gallery/items/{id}/edit")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------- helpers ----------

    private static MultipartFormDataContent Upload(string title, string date, bool membersOnly, string? personId = null, string contentType = "image/jpeg")
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4 });
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", "foto.jpg");
        form.Add(new StringContent(title), "title");
        form.Add(new StringContent(date), "date");
        form.Add(new StringContent(membersOnly ? "true" : "false"), "membersOnly");
        if (personId is not null)
        {
            form.Add(new StringContent(personId), "personIds");
        }

        return form;
    }

    private async Task<int> AddMediaAsync(string uploaderId, bool isPrivate, params string[] tags)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var m = GalleryMedia.Create(uploaderId, "Foto de teste", MediaType.Image, $"https://pub-test.r2.dev/images/test/gallery/{Guid.NewGuid():N}.jpg", 2020, 4, 2, null, null, isPrivate);
        db.GalleryMedia.Add(m);
        await db.SaveChangesAsync();
        foreach (var t in tags)
        {
            db.GalleryMediaPersonTags.Add(GalleryMediaPersonTag.Create(m.Id, t));
        }

        await db.SaveChangesAsync();
        return m.Id;
    }

    private async Task<GalleryMedia?> MediaAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().GalleryMedia.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
    }

    private async Task<int> CountAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().GalleryMedia.CountAsync();
    }

    private async Task<List<string>> TagsAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().GalleryMediaPersonTags
            .Where(t => t.GalleryMediaId == id).Select(t => t.UserId).ToListAsync();
    }

    private static int _ip;

    private Task<(HttpClient Client, ApplicationUser User)> SignInAsync(string? role = null)
    {
        var n = Interlocked.Increment(ref _ip);
        return CookieTestSession.SignInAsync(_factory, $"gal{Guid.NewGuid():N}"[..20], $"10.77.{n / 250}.{n % 250 + 1}", role);
    }

    private HttpClient Anonymous()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, "10.78.0.1");
        return client;
    }

    private static async Task WithTokenAsync(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/public/antiforgery-token");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }
}
