using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// /api/events/{id}/discussion and /api/events/{id}/contacts (React track 013) through the real host: real
/// login, antiforgery and SQLite, the real Post/Comment/Transportation/EventContact/Enrollment services.
/// Push, email and storage are the recording fakes of <see cref="EventsApiFactory"/>: nothing is sent or stored.
/// </summary>
public partial class EventDiscussionApiTests : IClassFixture<EventsApiFactory>
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

    private async Task SetCategoriesAsync(string userId, params MemberCategory[] categories)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Users.SingleAsync(u => u.Id == userId)).Categories = categories.ToList();
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

    private HttpClient Anonymous() => AreaHttp.Anonymous(_factory, "10.74.0.1");
}
