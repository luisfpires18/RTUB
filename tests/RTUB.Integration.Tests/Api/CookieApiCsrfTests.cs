using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// Integration tests for CSRF exposure of the cookie-authenticated API endpoints.
///
/// The browser authentication POSTs carry real antiforgery tokens (see AuthAntiforgeryTests).
/// These tests cover the remaining cookie-authenticated mutations:
///
/// * <c>POST /api/admin/refresh-all</c> was CSRF-reachable — an authenticated Admin form POST
///   with an arbitrary body returned 200 — and had no caller, so it was deleted.
/// * The <c>PushController</c> JSON actions are not form-CSRF-reachable: the JSON input
///   formatter rejects every content type a cross-site HTML form can emit with 415, before
///   the action runs. No antiforgery token plumbing was added for them.
/// </summary>
public class CookieApiCsrfTests : IntegrationTestBase
{
    private const string IdentityCookieName = ".AspNetCore.Identity.Application";

    public CookieApiCsrfTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task RefreshAllEndpoint_NoLongerExists()
    {
        var client = await SignInAsAdminAsync();

        // Authenticated as Admin, so a 404 means the route is gone — not that access was denied.
        var response = await client.PostAsync("/api/admin/refresh-all", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "the unused Admin refresh endpoint was removed rather than given antiforgery plumbing");
    }

    [Fact]
    public async Task PushJsonEndpoint_RejectsEveryContentTypeACrossSiteFormCanPost()
    {
        var client = await SignInAsAdminAsync();

        // The only three enctypes an HTML <form> can produce. All are CORS-"simple", so a
        // cross-site page can submit them without a preflight and with the sign-in cookie
        // attached — which is exactly the classic CSRF shape.
        foreach (var content in CrossSiteFormBodies())
        {
            var contentType = content.Headers.ContentType!.ToString();

            var response = await client.SendAsync(
                new HttpRequestMessage(HttpMethod.Post, "/api/push/unsubscribe") { Content = content });

            // 415 comes from input-formatter selection during model binding, before the action
            // body runs, so a cross-site form cannot invoke this endpoint even while carrying a
            // valid sign-in cookie. This is a server-side gate, independent of browser policy.
            response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType,
                $"a cross-site form posting {contentType} must never reach the action");
        }
    }

    private static IEnumerable<HttpContent> CrossSiteFormBodies()
    {
        yield return new FormUrlEncodedContent(
            new Dictionary<string, string> { ["endpoint"] = "https://attacker.example/victim" });

        yield return new MultipartFormDataContent
        {
            { new StringContent("https://attacker.example/victim"), "endpoint" }
        };

        yield return new StringContent(
            "{\"endpoint\":\"https://attacker.example/victim\"}", Encoding.UTF8, "text/plain");
    }

    [Fact]
    public async Task PushJsonEndpoint_AcceptsApplicationJsonFromRealCaller()
    {
        var client = await SignInAsAdminAsync();

        // The shape push-notifications.js and service-worker.js actually send.
        var response = await client.PostAsync("/api/push/unsubscribe", new StringContent(
            "{\"endpoint\":\"https://fcm.example/real-caller\"}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the 415 above must be a content-type gate, not a blanket block on the real caller");
    }

    [Fact]
    public async Task AuthenticationCookie_IsSameSiteLax()
    {
        var client = CreateBrowserClient();

        var response = await SignInAsync(client);

        var authCookie = SetCookieHeaders(response)
            .Should().ContainSingle(c => c.StartsWith(IdentityCookieName + "=", StringComparison.Ordinal))
            .Subject;

        // SameSite=Lax is what stops a cross-site POST from carrying the sign-in cookie at all.
        // It is the browser-side mitigation the Push endpoints are left relying on, so a change
        // to SameSite=None here would silently reopen CSRF across every cookie-authenticated POST.
        authCookie.Should().Contain("samesite=lax", "the sign-in cookie must not be sent cross-site");
    }

    private HttpClient CreateBrowserClient() => Factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    private async Task<HttpClient> SignInAsAdminAsync()
    {
        var client = CreateBrowserClient();
        var response = await SignInAsync(client);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect, "the seeded admin must sign in");
        return client;
    }

    /// <summary>
    /// Signs in the seeded admin through the real rendered login form, so the antiforgery
    /// protection added in unit 012 is exercised rather than bypassed.
    /// </summary>
    private static async Task<HttpResponseMessage> SignInAsync(HttpClient client)
    {
        var page = await client.GetAsync("/login");
        var token = AntiforgeryFormToken.Find(await page.Content.ReadAsStringAsync());
        token.Should().NotBeNullOrEmpty();

        return await client.PostAsync("/auth/login", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                [AntiforgeryFormToken.FieldName] = token!,
                ["Username"] = "testadmin",
                ["Password"] = "TestPassword123!",
                ["RememberMe"] = "false"
            }));
    }

    private static IEnumerable<string> SetCookieHeaders(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values) ? values : Array.Empty<string>();
}
