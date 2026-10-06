using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Application.Data;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// The requests admin on /api/requests (task 031, was the Blazor /requests) through the real host: real login,
/// antiforgery, SQLite and the old RequestService / RequestToEventService. Every test scopes its own requests with a
/// unique tag, since the class shares one database. No notification is sent on approve, reject or delete (none was).
/// </summary>
public class RequestsApiTests : IClassFixture<EventsApiFactory>
{
    private readonly EventsApiFactory _factory;

    public RequestsApiTests(EventsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Visitors_GetNothing_AndBothPagesAreReact_WithNoBlazorPageLeft()
    {
        var id = await AddAsync($"{Tag()} Visita");
        var anonymous = Anonymous();
        await WithTokenAsync(anonymous);
        (await anonymous.GetAsync("/api/requests")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsync($"/api/requests/{id}/approve", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsync($"/api/requests/{id}/reject", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.DeleteAsync($"/api/requests/{id}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        foreach (var path in new[] { "/requests", "/questions" })
        {
            var page = await anonymous.GetAsync(path);
            page.StatusCode.Should().Be(HttpStatusCode.Redirect, path);
            page.Headers.Location!.ToString().Should().Be("/login?returnUrl=" + Uri.EscapeDataString(path));
        }

        var (member, _) = await SignInAsync();
        foreach (var path in new[] { "/requests", "/questions" })
        {
            (await member.GetStringAsync(path)).Should().Contain("id=\"root\"", path).And.NotContain("blazor.web.js");
        }

        var web = typeof(RTUB.App).Assembly;
        web.GetType("RTUB.Pages.Management.Requests").Should().BeNull("the Blazor requests page was retired in 031");
        web.GetType("RTUB.Pages.Management.Questions").Should().BeNull("the Blazor questions page was retired in 031");
        web.GetTypes()
            .SelectMany(t => t.GetCustomAttributes(typeof(RouteAttribute), false).Cast<RouteAttribute>())
            .Select(r => r.Template)
            .Should().NotContain(new[] { "/requests", "/questions" }, "React owns both routes");
        foreach (var gone in new[] { "RTUB.Web/Pages/Management/Requests.razor", "RTUB.Web/Pages/Management/Questions.razor",
                     "RTUB.Shared/Components/Cards/RequestCard.razor", "RTUB.Shared/Components/Cards/QuestionCard.razor" })
        {
            File.Exists(Path.Combine(RepoRoot(), "src", gone)).Should().BeFalse("{0} was retired in 031", gone);
        }
    }

    [Fact]
    public async Task Leitoes_AreRefused_UnlessAdminOrOwner()
    {
        var (leitao, user) = await SignInAsync();
        await SetCategoryAsync(user.Id, MemberCategory.Leitao);
        (await leitao.GetAsync("/api/requests")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "the old page sent Leitões away");

        var (admin, adminUser) = await SignInAsync("Admin");
        await SetCategoryAsync(adminUser.Id, MemberCategory.Leitao);
        (await Json(admin, "/api/requests")).GetProperty("canManage").GetBoolean().Should().BeTrue("Admin and Owner are not sent away");
    }

    [Fact]
    public async Task Members_SeeEveryRequest_ThisYearByDefault_SplitSearchedAndFiltered()
    {
        var tag = Tag();
        var pending = await AddAsync($"{tag} Ana", email: $"{tag}ana@example.test", location: "Bragança");
        var confirmed = await AddAsync($"{tag} Bruno", status: RequestStatus.Confirmed);
        var rejected = await AddAsync($"{tag} Carla", status: RequestStatus.Rejected);
        var analysing = await AddAsync($"{tag} Duarte", status: RequestStatus.Analysing);
        var old = await AddAsync($"{tag} Antigo");
        await SetCreatedAsync(old, DateTime.UtcNow.AddYears(-2));
        var (member, _) = await SignInAsync();

        var body = await Json(member, $"/api/requests?q={tag}");
        body.GetProperty("fiscalYear").GetString().Should().Be(FiscalYearHelper.GetCurrentFiscalYearString(), "the old page opened on the current year");
        Ids(body, "pending").Should().BeEquivalentTo(new[] { pending, analysing }, "in analysis is still to answer");
        Ids(body, "answered").Should().BeEquivalentTo(new[] { confirmed, rejected });
        body.GetProperty("canManage").GetBoolean().Should().BeFalse();

        var first = body.GetProperty("pending").EnumerateArray().Single(r => r.GetProperty("id").GetInt32() == pending);
        first.GetProperty("email").GetString().Should().Be($"{tag}ana@example.test", "members see the contact details, as before");
        first.GetProperty("phone").GetString().Should().Be("912345678");
        first.GetProperty("status").GetString().Should().Be("pending");

        Ids(await Json(member, $"/api/requests?fiscalYear=&q={tag}"), "pending").Should().Contain(old, "\"\" is every year");
        var onlyConfirmed = await Json(member, $"/api/requests?status=confirmed&q={tag}");
        Ids(onlyConfirmed, "pending").Should().BeEmpty();
        Ids(onlyConfirmed, "answered").Should().Equal(confirmed);
        Ids(await Json(member, $"/api/requests?q={Uri.EscapeDataString($"{tag.ToUpperInvariant()}ANA@")}"), "pending").Should().Equal(pending);
        Ids(await Json(member, $"/api/requests?fiscalYear=&q={tag}&status=pending"), "pending").Should().BeEquivalentTo(new[] { pending, old });

        (await Errors(await member.GetAsync("/api/requests?status=bogus"))).Keys.Should().Contain("status");
        (await Errors(await member.GetAsync("/api/requests?fiscalYear=1900-1901"))).Keys.Should().Contain("fiscalYear");
    }

    [Fact]
    public async Task Answering_IsForAdminAndOwner_AndHappensOnce()
    {
        var tag = Tag();
        var first = await AddAsync($"{tag} Primeiro", location: "Mirandela");
        var second = await AddAsync($"{tag} Segundo");
        foreach (var role in new[] { "Member", "Mod" })
        {
            var (client, _) = await SignInAsync(role);
            await WithTokenAsync(client);
            (await client.PostAsync($"/api/requests/{first}/approve", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden, role);
            (await client.PostAsync($"/api/requests/{first}/reject", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden, role);
        }

        var (admin, _) = await SignInAsync("Admin");
        await WithTokenAsync(admin);
        var approved = await Json(await admin.PostAsync($"/api/requests/{first}/approve", null));
        approved.GetProperty("request").GetProperty("status").GetString().Should().Be("confirmed");
        var url = approved.GetProperty("createEventUrl").GetString()!;
        url.Should().StartWith("/events?openModal=true").And.Contain("location=Mirandela").And.Contain($"requestId={first}")
            .And.Contain("name=" + Uri.EscapeDataString("Serenata em Mirandela"), "the old \"Criar Evento\" prefill");
        (await StatusAsync(first)).Should().Be(RequestStatus.Confirmed);

        (await Errors(await admin.PostAsync($"/api/requests/{first}/approve", null))).Keys.Should().Contain("status");
        (await Errors(await admin.PostAsync($"/api/requests/{first}/reject", null))).Keys.Should().Contain("status");
        (await admin.PostAsync("/api/requests/999999/approve", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var (owner, _) = await SignInAsync("Owner");
        await WithTokenAsync(owner);
        (await Json(await owner.PostAsync($"/api/requests/{second}/reject", null))).GetProperty("status").GetString()
            .Should().Be("rejected", "Owner inherits Admin");
        (await StatusAsync(second)).Should().Be(RequestStatus.Rejected);
    }

    [Fact]
    public async Task Delete_IsForAdminAndOwner_AndIsHard()
    {
        var id = await AddAsync($"{Tag()} Apagar");
        var (member, _) = await SignInAsync();
        await WithTokenAsync(member);
        (await member.DeleteAsync($"/api/requests/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var (owner, _) = await SignInAsync("Owner");
        await WithTokenAsync(owner);
        (await owner.DeleteAsync($"/api/requests/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await StatusAsync(id)).Should().BeNull();
        (await owner.DeleteAsync($"/api/requests/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Writes_NeedTheAntiforgeryHeader()
    {
        var id = await AddAsync($"{Tag()} Token");
        var (admin, _) = await SignInAsync("Admin");

        (await admin.PostAsync($"/api/requests/{id}/approve", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsync($"/api/requests/{id}/reject", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.DeleteAsync($"/api/requests/{id}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await StatusAsync(id)).Should().Be(RequestStatus.Pending);
    }

    // ---------- helpers ----------

    private static string Tag() => "req" + Guid.NewGuid().ToString("N")[..8];

    private static IEnumerable<int> Ids(JsonElement body, string list) =>
        body.GetProperty(list).EnumerateArray().Select(r => r.GetProperty("id").GetInt32());

    private async Task<int> AddAsync(string name, RequestStatus status = RequestStatus.Pending, string? email = null, string location = "Porto")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var request = Request.Create(name, email ?? "pedido@example.test", "912345678", "Serenata", DateTime.UtcNow.Date.AddDays(30), location, "Olá!");
        request.UpdateStatus(status);
        db.Requests.Add(request);
        await db.SaveChangesAsync();
        return request.Id;
    }

    private async Task SetCreatedAsync(int id, DateTime createdAt)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Requests.Where(r => r.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.CreatedAt, createdAt));
    }

    private async Task<RequestStatus?> StatusAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Requests.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == id))?.Status;
    }

    private async Task SetCategoryAsync(string userId, MemberCategory category)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId);
        user!.Categories = [category];
        (await users.UpdateAsync(user)).Succeeded.Should().BeTrue();
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

    private static int _ip;

    private Task<(HttpClient Client, ApplicationUser User)> SignInAsync(string? role = null)
    {
        var n = Interlocked.Increment(ref _ip);
        return CookieTestSession.SignInAsync(_factory, $"req{Guid.NewGuid():N}"[..20], $"10.41.{n / 250}.{n % 250 + 1}", role);
    }

    private HttpClient Anonymous()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, "10.42.0.1");
        return client;
    }

    private static async Task WithTokenAsync(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/public/antiforgery-token");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
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
