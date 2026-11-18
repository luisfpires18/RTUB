using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;
/// <summary>
/// /api/events (React track 011) through the real host: the real login, real antiforgery, the real
/// SQLite test database and the real EnrollmentService. Push is a recording fake, so nothing is
/// ever sent; video and image links are fake https URLs and storage is never called.
/// </summary>
public partial class EventsApiTests : IClassFixture<EventsApiFactory>
{
    private const string Description = "Descrição só para membros";

    private const string Reason = "Motivo interno do cancelamento";

    private const string MemberNote = "nota-privada-de-um-membro";

    private readonly EventsApiFactory _factory;

    public EventsApiTests(EventsApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<int> AddEventVideoAsync(int eventId, string title, int order)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var uploader = await db.Users.Select(u => u.Id).FirstAsync();
        var v = EventVideo.CreateVideo(eventId, $"https://pub-test.r2.dev/events/test/videos/{eventId}_{order}.mp4", "video/mp4", 8, uploader, title, order);
        db.EventVideos.Add(v);
        await db.SaveChangesAsync();
        return v.Id;
    }

    private const string FakeImageUrl ="https://pub-test.r2.dev/images/test/events/new.webp";

    private static readonly byte[] WebpBytes = "RIFF\0\0\0\0WEBPVP8 "u8.ToArray();

    private async Task<int> AddEventAsync(string name, DateTime date, string? imageUrl = null, EventType type = EventType.Arraial,
        bool isPublic = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var e = Event.Create(name, date, "Bragança", type, "");
        e.ImageUrl = imageUrl;
        e.IsPublic = isPublic;
        db.Events.Add(e);
        await db.SaveChangesAsync();
        return e.Id;
    }

    private async Task<Event> EventByIdAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Events.AsNoTracking().SingleAsync(e => e.Id == id);
    }

    // ---------- helpers ----------

    private static object NewEvent(string name, string location = "Bragança") => new
    {
        name,
        date = DateTime.Today.AddDays(400).ToString("yyyy-MM-dd"),
        time = "21:00",
        endDate = (string?)null,
        location,
        type = "Arraial",
        description = "Só para membros",
    };

    private static object NewEvent(string name, bool membersOnly) => new
    {
        membersOnly,
        name,
        date = DateTime.Today.AddDays(400).ToString("yyyy-MM-dd"),
        time = "21:00",
        endDate = (string?)null,
        location = "Bragança",
        type = "Arraial",
        description = "Só para membros",
    };

    private sealed record Seed(int Open, int MultiDay, int Cancelled, int Past, int Festival, int Video, string MemberId);

    private static readonly SemaphoreSlim Lock = new(1, 1);

    private static Seed? _seed;

    /// <summary>One set of events per test class; every test reads it and writes only its own member's answers.</summary>
    private async Task<Seed> SeedAsync()
    {
        await Lock.WaitAsync();
        try
        {
            if (_seed is not null)
            {
                return _seed;
            }

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var today = DateTime.Today;

            var other = await db.Users.Select(u => u.Id).FirstAsync();
            Event Add(string name, DateTime date, DateTime? end = null, EventType type = EventType.Atuacao, string description = "")
            {
                var e = Event.Create(name, date, "Bragança", type, description);
                e.EndDate = end;
                db.Events.Add(e);
                return e;
            }

            var open = Add("Serenata aberta", today.AddDays(1000).AddHours(21), description: Description);
            var multiDay = Add("Festival de três dias", today.AddDays(999), end: today.AddDays(1001));
            var cancelled = Add("Arraial cancelado", today.AddDays(1002));
            cancelled.Cancel(Reason);
            var past = Add("Atuação passada", today.AddDays(-1000));
            var festival = Add("Festival antigo", today.AddDays(-1001), type: EventType.Festival);
            await db.SaveChangesAsync();

            db.Trophies.Add(Trophy.Create("Melhor Pandeireta", festival.Id));
            db.Trophies.Add(Trophy.Create("1.º Prémio", festival.Id));
            var video = EventVideo.CreateVideo(festival.Id, "https://pub-test.r2.dev/videos/events/festival.mp4", "video/mp4", 10, other, "Atuação");
            db.EventVideos.Add(video);
            var note = Enrollment.Create(other, open.Id);
            note.Notes = MemberNote;
            db.Enrollments.Add(note);
            await db.SaveChangesAsync();

            return _seed = new Seed(open.Id, multiDay.Id, cancelled.Id, past.Id, festival.Id, video.Id, other);
        }
        finally
        {
            Lock.Release();
        }
    }

    private static object Answer(bool willAttend, string? instrument = null, string? notes = null) => new { willAttend, instrument, notes };

    private static int _ip;

    private Task<(HttpClient Client, ApplicationUser User)> SignInAsync(string? role = null)
    {
        var n = Interlocked.Increment(ref _ip);
        return CookieTestSession.SignInAsync(_factory, $"events-{Guid.NewGuid():N}"[..24], $"10.71.{n / 250}.{n % 250 + 1}", role);
    }

    private HttpClient Anonymous() => AreaHttp.Anonymous(_factory, "10.72.0.1");

    private async Task<Enrollment?> EnrollmentAsync(int eventId, string userId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Enrollments.AsNoTracking()
            .FirstOrDefaultAsync(e => e.EventId == eventId && e.UserId == userId);
    }

    private async Task AddEnrollmentAsync(int eventId, string userId, bool willAttend)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var e = Enrollment.Create(userId, eventId);
        e.WillAttend = willAttend;
        db.Enrollments.Add(e);
        await db.SaveChangesAsync();
    }
}
