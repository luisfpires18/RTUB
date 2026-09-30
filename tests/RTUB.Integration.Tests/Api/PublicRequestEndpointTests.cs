using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// The test host with email and push replaced by recording mocks, so nothing is ever sent.
/// </summary>
public sealed class PublicRequestFactory : TestWebApplicationFactory
{
    public Mock<IEmailNotificationService> Email { get; } = new();
    public Mock<IPushNotificationService> Push { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailNotificationService>();
            services.RemoveAll<IPushNotificationService>();
            services.AddScoped(_ => Email.Object);
            services.AddScoped(_ => Push.Object);
        });
    }
}

/// <summary>
/// POST /api/public/requests (React track 003): anonymous, antiforgery-protected, rate limited per
/// IP with its own answer, honeypot, server-side validation through the shared service, and no
/// internal detail on failure. Each test uses its own client IP and its own email address.
/// </summary>
public class PublicRequestEndpointTests : IClassFixture<PublicRequestFactory>
{
    private const string Endpoint = "/api/public/requests";
    private readonly PublicRequestFactory _factory;

    public PublicRequestEndpointTests(PublicRequestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TokenEndpoint_IssuesAnUncachedFormToken()
    {
        var response = await Client("198.51.100.1").GetAsync("/api/public/antiforgery-token");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("token").GetString().Should().NotBeNullOrEmpty();
        json.RootElement.GetProperty("fieldName").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ValidRequest_IsStoredOnce_AndNotifiesThroughTheExistingChannels()
    {
        var client = Client("198.51.100.2");
        var email = "valid@example.com";

        var response = await PostAsync(client, Form(email));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("submitted").GetBoolean().Should().BeTrue();
        var stored = await StoredAsync(email);
        stored.Should().ContainSingle();
        stored[0].Message.Should().Be("Serenata para a Ana");
        stored[0].IsDateRange.Should().BeFalse();
        _factory.Email.Verify(e => e.SendNewRequestNotificationAsync(stored[0].Id, "Ana", email, It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<DateTime>(), null, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Once);
        _factory.Push.Verify(p => p.SendToUserAsync(It.IsAny<string>(), It.IsAny<SendPushNotificationDto>()), Times.AtLeastOnce,
            "the admin/owner push from RequestService still fires");
    }

    [Fact]
    public async Task DateRange_IsStored()
    {
        var email = "range@example.com";
        var form = Form(email);
        form["isDateRange"] = "true";
        form["preferredEndDate"] = Day(14);

        (await PostAsync(Client("198.51.100.3"), form)).StatusCode.Should().Be(HttpStatusCode.OK);

        var stored = (await StoredAsync(email)).Single();
        stored.IsDateRange.Should().BeTrue();
        stored.PreferredEndDate!.Value.Date.Should().Be(DateTime.Today.AddDays(14));
    }

    [Fact]
    public async Task WithoutAntiforgeryToken_IsRefused_AndStoresNothing()
    {
        var email = "no-token@example.com";

        var response = await Client("198.51.100.4").PostAsync(Endpoint, new FormUrlEncodedContent(Form(email)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await StoredAsync(email)).Should().BeEmpty();
    }

    [Fact]
    public async Task InvalidFields_Return400WithFieldErrors_AndStoreNothing()
    {
        var email = "not-an-email";
        var form = Form(email);
        form["preferredDate"] = Day(-2);
        form["name"] = "";

        var response = await PostAsync(Client("198.51.100.5"), form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = json.RootElement.GetProperty("errors");
        errors.TryGetProperty("email", out _).Should().BeTrue();
        errors.TryGetProperty("name", out _).Should().BeTrue();
        errors.GetProperty("preferredDate")[0].GetString().Should().Be("A data não pode ser no passado.");
        (await StoredAsync(email)).Should().BeEmpty();
    }

    [Fact]
    public async Task Honeypot_LooksLikeSuccess_ButStoresAndSendsNothing()
    {
        var email = "bot@example.com";
        var form = Form(email);
        form["website"] = "http://spam.example";

        var response = await PostAsync(Client("198.51.100.6"), form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await StoredAsync(email)).Should().BeEmpty();
        _factory.Email.Verify(e => e.SendNewRequestNotificationAsync(It.IsAny<int>(), It.IsAny<string>(), email,
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime?>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task OverTheLimit_Returns429ProblemJson_NotTheLoginText()
    {
        var client = Client("198.51.100.7");

        // The limiter runs before antiforgery, so tokenless posts spend the budget cheaply.
        for (var i = 1; i <= 5; i++)
        {
            (await client.PostAsync(Endpoint, new FormUrlEncodedContent(Form($"limit{i}@example.com"))))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest, $"request {i} is inside the limit");
        }

        var rejected = await client.PostAsync(Endpoint, new FormUrlEncodedContent(Form("limit6@example.com")));

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        rejected.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var body = await rejected.Content.ReadAsStringAsync();
        body.Should().Contain("pedidos").And.NotContain("login");
        rejected.Headers.RetryAfter.Should().NotBeNull();
    }

    [Fact]
    public async Task Failure_AnswersGenerically_WithoutInternalDetail()
    {
        var email = "boom@example.com";
        _factory.Email
            .Setup(e => e.SendNewRequestNotificationAsync(It.IsAny<int>(), It.IsAny<string>(), email, It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime>()))
            .ThrowsAsync(new InvalidOperationException("SMTP-SECRET-DETAIL at Internal.Stack"));

        var response = await PostAsync(Client("198.51.100.8"), Form(email));

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("SMTP-SECRET-DETAIL").And.NotContain("Internal.Stack").And.NotContain("Exception");
    }

    [Fact]
    public async Task GetOnTheSubmitRoute_IsNotAnEndpoint()
    {
        (await Client("198.51.100.9").GetAsync(Endpoint)).StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    // ---------- helpers ----------

    private HttpClient Client(string ip)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, ip);
        return client;
    }

    private static Dictionary<string, string> Form(string email) => new()
    {
        ["name"] = "Ana",
        ["email"] = email,
        ["phone"] = "912345678",
        ["eventType"] = "Serenata",
        ["preferredDate"] = Day(10),
        ["isDateRange"] = "false",
        ["location"] = "Bragança",
        ["message"] = "Serenata para a Ana",
        ["website"] = ""
    };

    private static string Day(int offset) => DateTime.Today.AddDays(offset).ToString("yyyy-MM-dd");

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, Dictionary<string, string> form)
    {
        using var token = JsonDocument.Parse(await client.GetStringAsync("/api/public/antiforgery-token"));
        var fields = new Dictionary<string, string>(form)
        {
            [token.RootElement.GetProperty("fieldName").GetString()!] = token.RootElement.GetProperty("token").GetString()!
        };
        return await client.PostAsync(Endpoint, new FormUrlEncodedContent(fields));
    }

    private async Task<List<RTUB.Core.Entities.Request>> StoredAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Requests.AsNoTracking().Where(r => r.Email == email).ToListAsync();
    }
}
