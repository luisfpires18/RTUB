using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Integration.Tests.Api;
/// <summary>
/// /api/pending-actions and /pending-actions (tasks 071 / 072) through the real host: real login, antiforgery and SQLite
/// (so the panel queries are proven to translate), and the answers through the real EnrollmentService /
/// RehearsalAttendanceService and the real unique indexes. Other tests share this database, so these assert on their own rows,
/// never on totals that other tests could change.
/// </summary>
public partial class PendingActionsApiTests : IClassFixture<EventsApiFactory>
{
    private readonly EventsApiFactory _factory;

    private static int _ip;

    private static int _day = 4000;

    public PendingActionsApiTests(EventsApiFactory factory)
    {
        _factory = factory;
    }

    // ---------- helpers ----------

    private static int _approvalDay;

    private async Task<(int Board, int Todo, int Done)> SeedBoardAsync(bool withDone, bool isPrivate = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var board = LogisticsBoard.Create($"PA {Guid.NewGuid():N}"[..12]);
        if (isPrivate)
        {
            board.Visibility = BoardVisibility.Private;
        }

        db.LogisticsBoards.Add(board);
        await db.SaveChangesAsync();
        board.LegacyFolder = "";
        var todo = LogisticsList.Create("Por fazer", board.Id, 0);
        todo.State = CardStatus.Todo;
        db.LogisticsLists.Add(todo);
        LogisticsList? done = null;
        if (withDone)
        {
            done = LogisticsList.Create("Feito", board.Id, 1);
            done.State = CardStatus.Done;
            db.LogisticsLists.Add(done);
        }

        await db.SaveChangesAsync();
        return (board.Id, todo.Id, done?.Id ?? 0);
    }

    private async Task<int> AddCardAsync(int listId, string title, string? owner = null, string? collaborator = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var list = await db.LogisticsLists.SingleAsync(l => l.Id == listId);
        var card = LogisticsCard.Create(title, listId, 0);
        card.Status = list.State ?? CardStatus.Todo;
        card.AssignedToUserId = owner;
        db.LogisticsCards.Add(card);
        await db.SaveChangesAsync();
        if (collaborator is not null)
        {
            db.LogisticsCardAssignments.Add(LogisticsCardAssignment.Create(card.Id, collaborator));
            await db.SaveChangesAsync();
        }

        return card.Id;
    }

    private static Task<HttpResponseMessage> Answer(HttpClient client, string kind, int id, string answer, string? instrument = null, string? notes = null) =>
        client.PostAsJsonAsync("/api/pending-actions/responses", new { kind, sourceKey = id.ToString(), answer, instrument, notes });

    private async Task CancelEventAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Events.SingleAsync(e => e.Id == id)).IsCancelled = true;
        await db.SaveChangesAsync();
    }

    private static int NextDay() => Interlocked.Increment(ref _day);

    private static async Task<List<string>> AllKeysAsync(HttpClient client, string panel, string? filter)
    {
        var keys = new List<string>();
        for (var page = 1; ; page++)
        {
            var body = await client.GetFromJsonAsync<JsonElement>($"/api/pending-actions/panels/{panel}?filter={filter ?? ""}&page={page}");
            keys.AddRange(body.GetProperty("rows").EnumerateArray().Select(r => r.GetProperty("sourceKey").GetString()!));
            var total = body.GetProperty("total").GetInt32();
            if (body.GetProperty("page").GetInt32() * 3 >= total)
            {
                return keys;
            }
        }
    }

    private async Task<int> AddEventAsync(int days)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var e = Event.Create($"Ação {days}", PortugalTime.Today(TimeProvider.System).AddDays(days).AddHours(21), "Braga", Enum.GetValues<EventType>()[0]);
        db.Events.Add(e);
        await db.SaveChangesAsync();
        return e.Id;
    }

    private async Task<int> AddRehearsalAsync(int days)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var r = Rehearsal.Create(PortugalTime.Today(TimeProvider.System).AddDays(days), "Centro Académico");
        db.Rehearsals.Add(r);
        await db.SaveChangesAsync();
        return r.Id;
    }

    private async Task SetMembershipAsync(string userId, List<MemberCategory> categories, bool retired)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == userId);
        user.Categories = categories;
        user.IsRetired = retired;
        await db.SaveChangesAsync();
    }

    private async Task<(HttpClient Client, ApplicationUser User)> SignInAsync(List<MemberCategory> categories, bool retired,
        string? role = null, List<Position>? positions = null)
    {
        var n = Interlocked.Increment(ref _ip);
        var (client, user) = await CookieTestSession.SignInAsync(_factory, $"pa{Guid.NewGuid():N}"[..20], $"10.81.{n / 250}.{n % 250 + 1}", role);
        await SetMembershipAsync(user.Id, categories, retired);
        if (positions is not null)
        {
            await SetPositionsAsync(user.Id, positions);
        }

        return (client, user);
    }

    private async Task SetPositionsAsync(string userId, List<Position> positions)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Users.SingleAsync(u => u.Id == userId)).Positions = positions;
        await db.SaveChangesAsync();
    }

    private static int CurrentFiscalYearStart()
    {
        var today = PortugalTime.Today(TimeProvider.System);
        return today.Month >= 9 ? today.Year : today.Year - 1;
    }

    private static Task<HttpResponseMessage> Command(HttpClient client, string kind, string sourceKey, string answer) =>
        client.PostAsJsonAsync("/api/pending-actions/responses", new { kind, sourceKey, answer });

    private static async Task<JsonElement> RowAsync(HttpClient client, string panel, string? filter, string sourceKey)
    {
        for (var page = 1; ; page++)
        {
            var body = await client.GetFromJsonAsync<JsonElement>($"/api/pending-actions/panels/{panel}?filter={filter ?? ""}&page={page}");
            foreach (var row in body.GetProperty("rows").EnumerateArray())
            {
                if (row.GetProperty("sourceKey").GetString() == sourceKey)
                {
                    return row;
                }
            }

            if (body.GetProperty("page").GetInt32() * 3 >= body.GetProperty("total").GetInt32())
            {
                throw new InvalidOperationException($"{sourceKey} not in {panel}");
            }
        }
    }

    private HttpClient Client()
    {
        var n = Interlocked.Increment(ref _ip);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, $"10.82.{n / 250}.{n % 250 + 1}");
        return client;
    }

    private static async Task WithTokenAsync(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/public/antiforgery-token");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }
}
