using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;
/// <summary>
/// Logística on /api/logistics (React track 023, were the Blazor /logistics and /logistics/{id}) through the real host:
/// real login, roles, antiforgery and SQLite. Board files go to <see cref="FakeDocumentStorage"/> (in memory, nothing
/// reaches R2). Pushes (069) go through the real push service: without VAPID keys only the inbox copy is kept. Reminder
/// delivery is driven by the tests (LogisticsApiTests.Reminders.cs). Every test makes its own boards, since the
/// class shares one database.
/// </summary>
public partial class LogisticsApiTests : IClassFixture<DocumentationApiFactory>
{
    private readonly DocumentationApiFactory _factory;

    public LogisticsApiTests(DocumentationApiFactory factory)
    {
        _factory = factory;
        _factory.Storage.Objects.Clear();
    }

    private async Task<string?> BoardFolderAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LogisticsBoards.Where(b => b.Id == id)
            .Select(b => b.LegacyFolder).SingleAsync();
    }

    private async Task UpdateBoardAsync(int id, Action<LogisticsBoard> change)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        change(await db.LogisticsBoards.SingleAsync(b => b.Id == id));
        await db.SaveChangesAsync();
    }

    // ---------- helpers ----------

    private static string Tag() => "lgx" + Guid.NewGuid().ToString("N")[..8];

    private static JsonElement Card(JsonElement board, int id) =>
        board.GetProperty("lists").EnumerateArray().SelectMany(l => l.GetProperty("cards").EnumerateArray()).Single(c => c.GetProperty("id").GetInt32() == id);

    private static MultipartFormDataContent File(string name, string type = "application/pdf")
    {
        var content = new ByteArrayContent(new byte[] { 1, 2, 3 });
        content.Headers.ContentType = new MediaTypeHeaderValue(type);
        return new MultipartFormDataContent { { content, "file", name } };
    }

    private async Task<(int Board, int List, int Card)> SeedBoardAsync(int cards = 1, string? name = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var board = LogisticsBoard.Create(name ?? $"{Tag()} Quadro");
        db.LogisticsBoards.Add(board);
        await db.SaveChangesAsync();
        board.LegacyFolder = "";
        var list = LogisticsList.Create("Material", board.Id, 0);
        list.State = CardStatus.Todo;
        db.LogisticsLists.Add(list);
        await db.SaveChangesAsync();
        var first = 0;
        for (var i = 0; i < cards; i++)
        {
            var card = LogisticsCard.Create($"Cartão {i}", list.Id, i);
            db.LogisticsCards.Add(card);
            await db.SaveChangesAsync();
            if (i == 0)
            {
                first = card.Id;
            }
        }

        return (board.Id, list.Id, first);
    }

    private async Task<int> AddListAsync(int boardId, string name, int position, CardStatus state = CardStatus.Todo)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var list = LogisticsList.Create(name, boardId, position);
        list.State = state;
        db.LogisticsLists.Add(list);
        await db.SaveChangesAsync();
        return list.Id;
    }

    private async Task<List<int>> CardIdsAsync(int listId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LogisticsCards.Where(c => c.ListId == listId)
            .OrderBy(c => c.Position).ThenBy(c => c.Id).Select(c => c.Id).ToListAsync();
    }

    private async Task<LogisticsCard?> CardAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LogisticsCards.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id);
    }

    private async Task UpdateCardAsync(int id, Action<LogisticsCard> change)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        change(await db.LogisticsCards.SingleAsync(c => c.Id == id));
        await db.SaveChangesAsync();
    }

    private async Task UpdateUserAsync(string id, Action<ApplicationUser> change)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        change(await db.Users.SingleAsync(u => u.Id == id));
        await db.SaveChangesAsync();
    }

    private static async Task<JsonElement> Json(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.StatusCode.Should().Be(HttpStatusCode.OK, path);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<Dictionary<string, string[]>> Errors(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("errors").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.EnumerateArray().Select(v => v.GetString()!).ToArray());
    }

    private static int _ip;

    private Task<(HttpClient Client, ApplicationUser User)> SignInAsync(string? role = null)
    {
        var n = Interlocked.Increment(ref _ip);
        return CookieTestSession.SignInAsync(_factory, $"lgx{Guid.NewGuid():N}"[..20], $"10.93.{n / 250}.{n % 250 + 1}", role);
    }

    private static async Task WithTokenAsync(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/public/antiforgery-token");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }
}
