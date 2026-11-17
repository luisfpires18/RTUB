using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Core.Entities;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// /api/owner/* and the React /emails, /notifications, /users, /owner/tracing and /owner/db (task 035,
/// docs/react-owner-operations.md) through the real host: real login, antiforgery, SQLite and the old services. Owner
/// only, every call: visitors 401 (pages: sign in), every other member 403 (Leitão, Caloiro, Tuno, Veterano, Mod and
/// Admin, which had /emails and /notifications before), the Owner through. Push and email are recording fakes. Tests
/// seed their own rows and assert by id or tag: the database is shared by the class.
/// </summary>
public partial class OwnerOperationsApiTests : IClassFixture<OwnerApiFactory>
{
    private static readonly string[] Pages = ["/emails", "/notifications", "/users", "/owner/tracing", "/owner/dashboard", "/owner/db"];

    /// <summary>The title of every refused send; no test sends it for real.</summary>
    private const string Refused = "Nunca enviado";
    private static int _ip;
    private readonly OwnerApiFactory _factory;

    public OwnerOperationsApiTests(OwnerApiFactory factory)
    {
        _factory = factory;
    }

    public static TheoryData<string, string?> NotOwners => new()
    {
        { "Leitão", null },
        { "Caloiro", null },
        { "Tuno", null },
        { "Veterano", null },
        { "Mod", "Mod" },
        { "Admin", "Admin" },
        { "Admin Veterano", "Admin" },
    };

    // ---------- helpers ----------

    private async Task<HttpClient> SignInAsync(string? role, params Action<ApplicationUser>[] setup)
    {
        var n = Interlocked.Increment(ref _ip);
        var (client, user) = await CookieTestSession.SignInAsync(_factory, $"own{Guid.NewGuid():N}"[..20], $"10.72.{n / 250}.{n % 250 + 1}", role);
        if (setup.Length > 0)
        {
            using var scope = _factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var stored = (await users.FindByIdAsync(user.Id))!;
            foreach (var change in setup)
            {
                change(stored);
            }

            (await users.UpdateAsync(stored)).Succeeded.Should().BeTrue();
        }

        await AreaHttp.WithTokenAsync(client);
        return client;
    }

    private async Task<ApplicationUser> SeedUserAsync(bool subscribed, bool confirmed = true, string? role = null)
    {
        var tag = Tag();
        var user = new ApplicationUser
        {
            UserName = $"tgt{tag}",
            Email = $"tgt{tag}@test.com",
            EmailConfirmed = confirmed,
            FirstName = "Alvo",
            LastName = tag,
            Nickname = $"Alvo{tag}",
            Subscribed = subscribed,
        };
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        (await users.CreateAsync(user)).Succeeded.Should().BeTrue();
        if (role is not null)
        {
            (await users.AddToRoleAsync(user, role)).Succeeded.Should().BeTrue();
        }

        return user;
    }

    private Task<int> SeedLogAsync(string entity, string? user = "someone", bool critical = false, string? changes = null,
        Action<AuditLog>? setup = null) => WithDbAsync(async db =>
    {
        var log = new AuditLog
        {
            EntityType = $"Test{entity}",
            Action = "Created",
            UserName = user,
            Timestamp = DateTime.UtcNow,
            IsCriticalAction = critical,
            Changes = changes,
        };
        setup?.Invoke(log);
        db.AuditLogs.Add(log);
        await db.SaveChangesAsync();
        return log.Id;
    });

    private Task<ApplicationUser> StoredUserAsync(string id) => WithDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == id));

    private Task<bool> LogExistsAsync(int id) => WithDbAsync(db => db.AuditLogs.AnyAsync(a => a.Id == id));

    private Task<List<string>> RolesAsync(string userId) => WithDbAsync(db =>
        (from link in db.UserRoles join role in db.Roles on link.RoleId equals role.Id where link.UserId == userId select role.Name!).ToListAsync());

    private async Task<T> WithDbAsync<T>(Func<ApplicationDbContext, Task<T>> work)
    {
        using var scope = _factory.Services.CreateScope();
        await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();
        return await work(db);
    }

    /// <summary>The account and the log the refused calls aimed at are as they were.</summary>
    private async Task NothingChangedAsync(string userId, int logId)
    {
        var user = await StoredUserAsync(userId);
        user.Subscribed.Should().BeTrue();
        (await RolesAsync(userId)).Should().BeEmpty();
        (await LogExistsAsync(logId)).Should().BeTrue();
    }

    private static string Tag() => Guid.NewGuid().ToString("N")[..10];

    private static List<string?> Ids(JsonElement people) => people.EnumerateArray().Select(p => p.GetProperty("id").GetString()).ToList();

    private static List<int> LogIds(JsonElement page) => page.GetProperty("items").EnumerateArray().Select(l => l.GetProperty("id").GetInt32()).ToList();

    /// <summary>Every GET, with the status it answered; each must say Cache-Control: no-store.</summary>
    private static async Task<Dictionary<string, HttpStatusCode>> ReadsAsync(HttpClient client)
    {
        var statuses = new Dictionary<string, HttpStatusCode>();
        foreach (var path in new[]
                 {
                     "/api/owner/emails", "/api/owner/notifications", "/api/owner/users", "/api/owner/users?q=a&page=2&pageSize=40",
                     "/api/owner/tracing/filters", "/api/owner/tracing", "/api/owner/tracing?critical=true&q=x", "/api/owner/tracing/export",
                     "/api/owner/tracing/logins", "/api/owner/dashboard", "/api/owner/db/tables", "/api/owner/db/tables/AuditLogs",
                 })
        {
            var response = await client.GetAsync(path);
            response.Headers.CacheControl!.NoStore.Should().BeTrue("{0} is never cached", path);
            statuses[path] = response.StatusCode;
        }

        return statuses;
    }

    /// <summary>Every write, each with a well-formed body aimed at a real account and log.</summary>
    private static async Task<Dictionary<string, HttpStatusCode>> WritesAsync(HttpClient client, string userId, int logId)
    {
        var calls = new (string Name, Func<Task<HttpResponseMessage>> Send)[]
        {
            ("PUT emails/recipients", () => client.PutAsJsonAsync("/api/owner/emails/recipients", new { userIds = new[] { userId }, subscribed = false })),
            ("POST emails", () => client.PostAsJsonAsync("/api/owner/emails", new { title = Refused, content = "x" })),
            ("POST notifications", () => client.PostAsJsonAsync("/api/owner/notifications", new { title = Refused, body = "x", userIds = new[] { userId } })),
            ("PUT users/role", () => client.PutAsJsonAsync($"/api/owner/users/{userId}/role", new { role = "Owner" })),
            ("POST users/unlock", () => client.PostAsync($"/api/owner/users/{userId}/unlock", null)),
            ("POST users/sync-status", () => client.PostAsync("/api/owner/users/sync-status", null)),
            ("DELETE tracing", () => client.DeleteAsync($"/api/owner/tracing/{logId}")),
            ("POST tracing/truncate", () => client.PostAsJsonAsync("/api/owner/tracing/truncate", new { user = "", confirmation = "DELETE ALL" })),
            ("POST tracing/announcement", () => client.PostAsJsonAsync("/api/owner/tracing/announcement", new { message = "Olá" })),
            ("POST db/select", () => client.PostAsJsonAsync("/api/owner/db/select", new { query = "SELECT 1" })),
            ("POST db/modify", () => client.PostAsJsonAsync("/api/owner/db/modify", new { query = $"DELETE FROM AuditLogs WHERE Id = {logId}", confirmed = true })),
        };

        var statuses = new Dictionary<string, HttpStatusCode>();
        foreach (var (name, send) in calls)
        {
            statuses[name] = (await send()).StatusCode;
        }

        return statuses;
    }

    private static void AllAnswer(Dictionary<string, HttpStatusCode> statuses, HttpStatusCode expected) =>
        statuses.Where(s => s.Value != expected).Select(s => $"{s.Key} answered {(int)s.Value}")
            .Should().BeEmpty("every call answers {0}", (int)expected);
}
