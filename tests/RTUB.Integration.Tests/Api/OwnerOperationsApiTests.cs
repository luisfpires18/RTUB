using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// /api/owner/* and the React /emails, /notifications, /users, /owner/tracing and /owner/db (task 035,
/// docs/react-owner-operations.md) through the real host: real login, antiforgery, SQLite and the old services. Owner
/// only, every call: visitors 401 (pages: sign in), every other member 403 (Leitão, Caloiro, Tuno, Veterano, Mod and
/// Admin, which had /emails and /notifications before), the Owner through. Push and email are recording fakes. Tests
/// seed their own rows and assert by id or tag: the database is shared by the class.
/// </summary>
public class OwnerOperationsApiTests : IClassFixture<OwnerApiFactory>
{
    private static readonly string[] Pages = ["/emails", "/notifications", "/users", "/owner/tracing", "/owner/db"];

    /// <summary>The title of every refused send; no test sends it for real.</summary>
    private const string Refused = "Nunca enviado";
    private static int _ip;
    private readonly OwnerApiFactory _factory;

    public OwnerOperationsApiTests(OwnerApiFactory factory)
    {
        _factory = factory;
    }

    // ---------- who gets in ----------

    [Fact]
    public async Task Visitors_GetNothing_AndThePagesSendThemToSignIn()
    {
        var target = await SeedUserAsync(subscribed: true);
        var log = await SeedLogAsync("visitor");
        var anonymous = AreaHttp.Anonymous(_factory, "10.71.0.1");
        await AreaHttp.WithTokenAsync(anonymous);

        AllAnswer(await ReadsAsync(anonymous), HttpStatusCode.Unauthorized);
        AllAnswer(await WritesAsync(anonymous, target.Id, log), HttpStatusCode.Unauthorized);

        foreach (var page in Pages)
        {
            var response = await anonymous.GetAsync(page);
            response.StatusCode.Should().Be(HttpStatusCode.Redirect, page);
            response.Headers.Location!.ToString().Should().Be("/login?returnUrl=" + Uri.EscapeDataString(page));
        }

        await NothingChangedAsync(target.Id, log);
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

    [Theory]
    [MemberData(nameof(NotOwners))]
    public async Task EveryoneButTheOwner_IsRefusedEveryCall_AndSeesTheRefusal(string who, string? role)
    {
        var member = await SignInAsync(role, who switch
        {
            "Leitão" => Who.Leitao,
            "Caloiro" => Who.Caloiro,
            "Tuno" => Who.Tuno,
            "Veterano" or "Admin Veterano" => Who.Veterano,
            _ => _ => { },
        });
        var target = await SeedUserAsync(subscribed: true);
        var log = await SeedLogAsync(who);

        var reads = await ReadsAsync(member);
        AllAnswer(reads, HttpStatusCode.Forbidden);
        AllAnswer(await WritesAsync(member, target.Id, log), HttpStatusCode.Forbidden);

        foreach (var page in Pages)
        {
            var response = await member.GetAsync(page);
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "{0} is Owner only", page);
            (await response.Content.ReadAsStringAsync()).Should().Contain("id=\"root\"", "the React page shows the refusal");
        }

        // No operational data in the refusals.
        (await member.GetStringAsyncAllowingErrors("/api/owner/emails")).Should().NotContain(target.Email!);
        (await member.GetStringAsyncAllowingErrors("/api/owner/users?q=" + target.UserName)).Should().NotContain(target.Email!);
        await NothingChangedAsync(target.Id, log);
        _factory.Email.Verify(e => e.SendAnnouncementEmailAsync(Refused, It.IsAny<string>(), It.IsAny<List<string>>(),
            It.IsAny<Dictionary<string, (string, string)>>(), It.IsAny<IProgress<EmailSendProgress>?>()), Times.Never);
        _factory.Push.Verify(p => p.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.Is<SendPushNotificationDto>(n => n.Title == Refused)),
            Times.Never);
    }

    [Fact]
    public async Task TheOwner_GetsTheReactPages_AndEveryRead_NeverCached()
    {
        var owner = await SignInAsync("Owner");

        foreach (var page in Pages)
        {
            var response = await owner.GetAsync(page);
            response.StatusCode.Should().Be(HttpStatusCode.OK, page);
            response.Headers.CacheControl!.NoCache.Should().BeTrue();
            var html = await response.Content.ReadAsStringAsync();
            html.Should().Contain("id=\"root\"").And.NotContain("blazor.web.js", "{0} is React", page);
        }

        AllAnswer(await ReadsAsync(owner), HttpStatusCode.OK);
    }

    [Fact]
    public async Task EveryWrite_NeedsTheAntiforgeryToken_EvenForTheOwner()
    {
        var owner = await SignInAsync("Owner");
        var target = await SeedUserAsync(subscribed: true);
        var log = await SeedLogAsync("csrf");
        owner.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");

        AllAnswer(await WritesAsync(owner, target.Id, log), HttpStatusCode.BadRequest);
        await NothingChangedAsync(target.Id, log);
    }

    // ---------- /emails ----------

    [Fact]
    public async Task Emails_ListsConfirmedAddresses_BySubscription_AndTheOwnerTogglesThem()
    {
        var owner = await SignInAsync("Owner");
        var subscribed = await SeedUserAsync(subscribed: true);
        var unsubscribed = await SeedUserAsync(subscribed: false);
        var unconfirmed = await SeedUserAsync(subscribed: true, confirmed: false);

        var list = await AreaHttp.Json(owner, "/api/owner/emails");
        Ids(list.GetProperty("subscribed")).Should().Contain(subscribed.Id).And.NotContain(unsubscribed.Id).And.NotContain(unconfirmed.Id);
        Ids(list.GetProperty("notSubscribed")).Should().Contain(unsubscribed.Id).And.NotContain(unconfirmed.Id);

        var removed = await AreaHttp.Json(await owner.PutAsJsonAsync("/api/owner/emails/recipients", new { userIds = new[] { subscribed.Id }, subscribed = false }));
        removed.GetProperty("changed").GetInt32().Should().Be(1);
        Ids(removed.GetProperty("recipients").GetProperty("notSubscribed")).Should().Contain(subscribed.Id);
        (await StoredUserAsync(subscribed.Id)).Subscribed.Should().BeFalse();

        var added = await AreaHttp.Json(await owner.PutAsJsonAsync("/api/owner/emails/recipients",
            new { userIds = new[] { subscribed.Id, unsubscribed.Id }, subscribed = true }));
        added.GetProperty("changed").GetInt32().Should().Be(2);
        (await StoredUserAsync(subscribed.Id)).Subscribed.Should().BeTrue();
        (await StoredUserAsync(unsubscribed.Id)).Subscribed.Should().BeTrue();

        (await AreaHttp.Errors(await owner.PutAsJsonAsync("/api/owner/emails/recipients", new { userIds = Array.Empty<string>(), subscribed = true })))
            .Keys.Should().Equal("userIds");
    }

    [Fact]
    public async Task Emails_ValidatesTheForm_AndSendsToTheSubscribedConfirmedAddressesOnly()
    {
        var owner = await SignInAsync("Owner");
        var subscribed = await SeedUserAsync(subscribed: true);
        var unsubscribed = await SeedUserAsync(subscribed: false);
        var tag = Tag();

        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/owner/emails", new { title = " ", content = "" }))).Keys.Should().BeEquivalentTo("title", "content");
        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/owner/emails", new { title = new string('t', 201), content = "x" }))).Keys.Should().Equal("title");
        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/owner/emails", new { title = "x", content = new string('c', 5001) }))).Keys.Should().Equal("content");

        var sent = await AreaHttp.Json(await owner.PostAsJsonAsync("/api/owner/emails", new { title = $"Anúncio {tag}", content = "Ensaio geral amanhã." }));
        sent.GetProperty("success").GetBoolean().Should().BeTrue();

        _factory.Email.Verify(e => e.SendAnnouncementEmailAsync($"Anúncio {tag}", "Ensaio geral amanhã.",
            It.Is<List<string>>(to => to.Contains(subscribed.Email!) && !to.Contains(unsubscribed.Email!) && to.Count == to.Distinct().Count()),
            It.Is<Dictionary<string, (string, string)>>(d => d.ContainsKey(subscribed.Email!)),
            It.IsAny<IProgress<EmailSendProgress>?>()), Times.Once);
    }

    [Fact]
    public async Task Emails_ReportsTheSendersRefusal()
    {
        var owner = await SignInAsync("Owner");
        await SeedUserAsync(subscribed: true);
        var tag = Tag();
        _factory.Email.Setup(e => e.SendAnnouncementEmailAsync($"Recusado {tag}", It.IsAny<string>(), It.IsAny<List<string>>(),
                It.IsAny<Dictionary<string, (string, string)>>(), It.IsAny<IProgress<EmailSendProgress>?>()))
            .ReturnsAsync((false, 0, "Email de anúncio já enviado recentemente."));

        var result = await AreaHttp.Json(await owner.PostAsJsonAsync("/api/owner/emails", new { title = $"Recusado {tag}", content = "x" }));
        result.GetProperty("success").GetBoolean().Should().BeFalse();
        result.GetProperty("message").GetString().Should().Be("Email de anúncio já enviado recentemente.");
    }

    // ---------- /notifications ----------

    [Fact]
    public async Task Notifications_ListsThePushSubscribers_AndSendsToTheChosenOnes_Audited()
    {
        var owner = await SignInAsync("Owner");
        var withPush = await SeedUserAsync(subscribed: true);
        var withoutPush = await SeedUserAsync(subscribed: true);
        _factory.PushSubscribed.Add(withPush.Id);
        var tag = Tag();

        var list = await AreaHttp.Json(owner, "/api/owner/notifications");
        Ids(list.GetProperty("recipients")).Should().Contain(withPush.Id).And.NotContain(withoutPush.Id);

        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/owner/notifications", new { title = "", body = " ", userIds = new[] { withPush.Id } })))
            .Keys.Should().BeEquivalentTo("title", "body");
        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/owner/notifications", new { title = new string('t', 101), body = "x", userIds = new[] { withPush.Id } })))
            .Keys.Should().Equal("title");
        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/owner/notifications", new { title = "x", body = new string('b', 501), userIds = new[] { withPush.Id } })))
            .Keys.Should().Equal("body");
        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/owner/notifications", new { title = "x", body = "y", userIds = new[] { withoutPush.Id } })))
            .Keys.Should().Equal("userIds");

        var sent = await AreaHttp.Json(await owner.PostAsJsonAsync("/api/owner/notifications",
            new { title = $"Aviso {tag}", body = "Ensaio às 21h.", userIds = new[] { withPush.Id, withoutPush.Id } }));
        sent.GetProperty("recipients").GetInt32().Should().Be(1, "only accounts with a push subscription are sent to");

        _factory.Push.Verify(p => p.SendToSelectedUsersAsync(
            It.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { withPush.Id })),
            It.Is<SendPushNotificationDto>(n => n.Title == $"Aviso {tag}" && n.Body == "Ensaio às 21h." && n.Icon == "/icons/rtub-logo-192.png"
                && n.Tag!.StartsWith("announcement-"))), Times.Once);

        var audit = await WithDbAsync(db => db.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityDisplayName == $"Aviso {tag}"));
        audit.Action.Should().Be("PushNotificationSent");
        audit.EntityType.Should().Be("Notification");
        audit.Changes.Should().Contain("\"RecipientCount\":1").And.Contain("Ensaio");
    }

    // ---------- /users ----------

    [Fact]
    public async Task Users_SearchesPagesAndShowsAnAccount()
    {
        var owner = await SignInAsync("Owner");
        var target = await SeedUserAsync(subscribed: true);

        var page = await AreaHttp.Json(owner, $"/api/owner/users?q={target.UserName}");
        var card = page.GetProperty("items").EnumerateArray().Single();
        card.GetProperty("id").GetString().Should().Be(target.Id);
        card.GetProperty("email").GetString().Should().Be(target.Email);
        card.GetProperty("role").GetString().Should().Be("Sem Função");
        page.GetProperty("pageSize").GetInt32().Should().Be(20);
        (await AreaHttp.Json(owner, "/api/owner/users?pageSize=7")).GetProperty("pageSize").GetInt32().Should().Be(20, "only the offered sizes");

        var detail = await AreaHttp.Json(owner, $"/api/owner/users/{target.Id}");
        detail.GetProperty("subscribed").GetBoolean().Should().BeTrue();
        detail.GetProperty("locked").GetBoolean().Should().BeFalse();
        detail.TryGetProperty("passwordHash", out _).Should().BeFalse("no secrets leave the server");
        (await owner.GetAsync("/api/owner/users/nao-existe")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Users_TheOwnerChangesARole_OwnerAlsoGettingAdmin()
    {
        var owner = await SignInAsync("Owner");
        var target = await SeedUserAsync(subscribed: true, role: "Member");

        (await AreaHttp.Errors(await owner.PutAsJsonAsync($"/api/owner/users/{target.Id}/role", new { role = "Rei" }))).Keys.Should().Equal("role");
        (await owner.PutAsJsonAsync("/api/owner/users/nao-existe/role", new { role = "Mod" })).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await AreaHttp.Json(await owner.PutAsJsonAsync($"/api/owner/users/{target.Id}/role", new { role = "Mod" }))).GetProperty("role").GetString().Should().Be("Mod");
        (await RolesAsync(target.Id)).Should().BeEquivalentTo("Mod");

        (await AreaHttp.Json(await owner.PutAsJsonAsync($"/api/owner/users/{target.Id}/role", new { role = "Owner" }))).GetProperty("role").GetString().Should().Be("Owner");
        (await RolesAsync(target.Id)).Should().BeEquivalentTo("Owner", "Admin");

        (await AreaHttp.Json(await owner.PutAsJsonAsync($"/api/owner/users/{target.Id}/role", new { role = "Member" }))).GetProperty("role").GetString().Should().Be("Member");
        (await RolesAsync(target.Id)).Should().BeEquivalentTo("Member");
    }

    [Fact]
    public async Task Users_TheOwnerUnlocksAnAccount_AndSyncsTheStatuses()
    {
        var owner = await SignInAsync("Owner");
        var target = await SeedUserAsync(subscribed: true);
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var stored = (await users.FindByIdAsync(target.Id))!;
            await users.SetLockoutEnabledAsync(stored, true);
            await users.SetLockoutEndDateAsync(stored, DateTimeOffset.UtcNow.AddHours(1));
            await users.AccessFailedAsync(stored);
        }

        (await AreaHttp.Json(owner, $"/api/owner/users/{target.Id}")).GetProperty("locked").GetBoolean().Should().BeTrue();
        var unlocked = await AreaHttp.Json(await owner.PostAsync($"/api/owner/users/{target.Id}/unlock", null));
        unlocked.GetProperty("locked").GetBoolean().Should().BeFalse();
        var stored2 = await StoredUserAsync(target.Id);
        stored2.LockoutEnd.Should().BeNull();
        stored2.AccessFailedCount.Should().Be(0);

        (await AreaHttp.Json(await owner.PostAsync("/api/owner/users/sync-status", null))).GetProperty("updated").GetInt32().Should().BeGreaterThanOrEqualTo(0);
    }

    // ---------- /owner/tracing ----------

    [Fact]
    public async Task Tracing_FiltersSearchesDeletesAndExports()
    {
        var owner = await SignInAsync("Owner");
        var tag = Tag();
        var first = await SeedLogAsync(tag, user: $"u{tag}", critical: true, changes: $"{{\"Name\":\"{tag}-one\"}}");
        var second = await SeedLogAsync(tag, user: $"u{tag}", changes: "{\"Name\":\"two\"}");

        var filters = await AreaHttp.Json(owner, "/api/owner/tracing/filters");
        filters.GetProperty("users").EnumerateArray().Select(u => u.GetString()).Should().Contain($"u{tag}");

        var byUser = await AreaHttp.Json(owner, $"/api/owner/tracing?user=u{tag}");
        byUser.GetProperty("total").GetInt32().Should().Be(2);
        var critical = await AreaHttp.Json(owner, $"/api/owner/tracing?user=u{tag}&critical=true");
        LogIds(critical).Should().Equal(first);
        critical.GetProperty("criticalOnPage").GetInt32().Should().Be(1);
        LogIds(await AreaHttp.Json(owner, $"/api/owner/tracing?q={tag}-one")).Should().Equal(first);

        var export = await owner.GetAsync($"/api/owner/tracing/export?user=u{tag}");
        export.StatusCode.Should().Be(HttpStatusCode.OK);
        export.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        export.Content.Headers.ContentDisposition!.FileName.Should().StartWith("tracing-export-");
        export.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await export.Content.ReadAsStringAsync()).Should().Contain($"{tag}-one");

        (await owner.DeleteAsync($"/api/owner/tracing/{second}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await owner.DeleteAsync($"/api/owner/tracing/{second}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        LogIds(await AreaHttp.Json(owner, $"/api/owner/tracing?user=u{tag}")).Should().Equal(first);
    }

    [Fact]
    public async Task Tracing_TruncateNeedsTheTypedConfirmation_AndOneUsersTruncateKeepsTheRest()
    {
        var owner = await SignInAsync("Owner");
        var tag = Tag();
        var mine = await SeedLogAsync(tag, user: $"a{tag}");
        var theirs = await SeedLogAsync(tag, user: $"b{tag}");

        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/owner/tracing/truncate", new { user = $"a{tag}", confirmation = "delete all" })))
            .Keys.Should().Equal("confirmation");
        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/owner/tracing/truncate", new { user = "", confirmation = "" })))
            .Keys.Should().Equal("confirmation");
        (await LogExistsAsync(mine)).Should().BeTrue();

        (await owner.PostAsJsonAsync("/api/owner/tracing/truncate", new { user = $"a{tag}", confirmation = "DELETE ALL" })).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        (await LogExistsAsync(mine)).Should().BeFalse();
        (await LogExistsAsync(theirs)).Should().BeTrue();
    }

    [Fact]
    public async Task Tracing_LoginsAndTheAnnouncement()
    {
        var owner = await SignInAsync("Owner");
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        (await AreaHttp.Json(owner, $"/api/owner/tracing/logins?from={today}&to={today}")).ValueKind.Should().Be(JsonValueKind.Array);
        (await AreaHttp.Errors(await owner.GetAsync("/api/owner/tracing/logins?from=ontem"))).Keys.Should().Equal("from");

        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/owner/tracing/announcement", new { message = " " }))).Keys.Should().Equal("message");
        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/owner/tracing/announcement", new { message = new string('m', 501) }))).Keys.Should().Equal("message");
        (await owner.PostAsJsonAsync("/api/owner/tracing/announcement", new { message = "O servidor vai reiniciar." })).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
    }

    // ---------- /owner/db ----------

    [Fact]
    public async Task Database_ListsTablesAndPagesThroughOne()
    {
        var owner = await SignInAsync("Owner");
        await SeedLogAsync("db");

        var tables = (await AreaHttp.Json(owner, "/api/owner/db/tables")).EnumerateArray().Select(t => t.GetString()).ToList();
        tables.Should().Contain("AuditLogs");

        var grid = await AreaHttp.Json(owner, "/api/owner/db/tables/AuditLogs?page=1&pageSize=10");
        grid.GetProperty("columns").EnumerateArray().Select(c => c.GetString()).Should().Contain("EntityType");
        grid.GetProperty("rows").GetArrayLength().Should().BeInRange(1, 10);
        grid.GetProperty("total").GetInt32().Should().BeGreaterThan(0);

        (await owner.GetAsync("/api/owner/db/tables/NaoExiste")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.GetAsync("/api/owner/db/tables/Audit%3BLogs")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Database_SelectIsReadOnly_AndAChangeNeedsTheConfirmationAndTheSqlRules()
    {
        var owner = await SignInAsync("Owner");
        var tag = Tag();
        var log = await SeedLogAsync(tag);

        var result = await AreaHttp.Json(await owner.PostAsJsonAsync("/api/owner/db/select", new { query = $"SELECT Id, EntityType, Changes FROM AuditLogs WHERE Id = {log}" }));
        result.GetProperty("columns").EnumerateArray().Select(c => c.GetString()).Should().Equal("Id", "EntityType", "Changes");
        result.GetProperty("rows")[0][0].GetString().Should().Be(log.ToString());
        result.GetProperty("rows")[0][2].GetString().Should().Be("[NULL]");

        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/owner/db/select", new { query = $"UPDATE AuditLogs SET Action = 'x' WHERE Id = {log}" })))
            .Keys.Should().Equal("query");
        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/owner/db/select", new { query = "SELECT * FROM NaoExiste" }))).Keys.Should().Equal("query");

        var update = $"UPDATE AuditLogs SET Action = 'Mudado' WHERE Id = {log}";
        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/owner/db/modify", new { query = update, confirmed = false }))).Keys.Should().Equal("confirmed");
        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/owner/db/modify", new { query = "DROP TABLE AuditLogs", confirmed = true }))).Keys.Should().Equal("query");
        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/owner/db/modify", new { query = $"SELECT * FROM AuditLogs", confirmed = true }))).Keys.Should().Equal("query");
        (await WithDbAsync(db => db.AuditLogs.AsNoTracking().SingleAsync(a => a.Id == log))).Action.Should().Be("Created");

        (await AreaHttp.Json(await owner.PostAsJsonAsync("/api/owner/db/modify", new { query = update, confirmed = true }))).GetProperty("rowsAffected").GetInt32().Should().Be(1);
        (await WithDbAsync(db => db.AuditLogs.AsNoTracking().SingleAsync(a => a.Id == log))).Action.Should().Be("Mudado");
    }

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

    private Task<int> SeedLogAsync(string entity, string? user = "someone", bool critical = false, string? changes = null) => WithDbAsync(async db =>
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
                     "/api/owner/tracing/logins", "/api/owner/db/tables", "/api/owner/db/tables/AuditLogs",
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

/// <summary>The host with push and email replaced by recording fakes: nothing reaches a device or a mailbox.</summary>
public sealed class OwnerApiFactory : TestWebApplicationFactory
{
    public Mock<IPushNotificationService> Push { get; } = new();
    public Mock<IEmailNotificationService> Email { get; } = new();

    /// <summary>The accounts the fake push service reports as subscribed; tests add their own.</summary>
    public System.Collections.Concurrent.ConcurrentBag<string> PushSubscribed { get; } = new();

    public OwnerApiFactory()
    {
        Push.Setup(p => p.GetSubscribedUserIdsAsync()).ReturnsAsync(() => PushSubscribed.ToArray().AsEnumerable());
        Push.Setup(p => p.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<SendPushNotificationDto>()))
            .ReturnsAsync((IEnumerable<string> ids, SendPushNotificationDto _) => (ids.Count(), 0));
        Email.Setup(e => e.SendAnnouncementEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(),
                It.IsAny<Dictionary<string, (string, string)>>(), It.IsAny<IProgress<EmailSendProgress>?>()))
            .ReturnsAsync((string _, string _, List<string> to, Dictionary<string, (string, string)> _, IProgress<EmailSendProgress>? _) => (true, to.Count, (string?)null));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPushNotificationService>();
            services.AddSingleton(Push.Object);
            services.RemoveAll<IEmailNotificationService>();
            services.AddSingleton(Email.Object);
        });
    }
}

internal static class OwnerHttpExtensions
{
    /// <summary>The body whatever the status (a refusal's problem JSON included).</summary>
    public static async Task<string> GetStringAsyncAllowingErrors(this HttpClient client, string path) =>
        await (await client.GetAsync(path)).Content.ReadAsStringAsync();
}
