using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using RTUB.Core.Entities;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// /api/account/forgot-password, /reset-password and /confirm-email (task 036, docs/react-identity.md) through the real
/// host and real Identity tokens. Email is a recording fake: the reset link is read from the email the template rendered,
/// the way a member would follow it. Also: /Error is the fixed server page, and /share is gone.
/// </summary>
public partial class AccountRecoveryApiTests : IClassFixture<AccountRecoveryFactory>
{
    private const string Neutral = "Se existir uma conta para este email, foi enviado um link de recuperação de palavra-passe.";
    private static int _ip;
    private readonly AccountRecoveryFactory _factory;

    public AccountRecoveryApiTests(AccountRecoveryFactory factory)
    {
        _factory = factory;
    }

    // ---------- forgot-password ----------

    [Fact]
    public async Task Forgot_ValidatesTheAddress()
    {
        var client = await VisitorAsync();

        (await AreaHttp.Errors(await client.PostAsJsonAsync("/api/account/forgot-password", new { email = " " }))).Keys.Should().Equal("email");
        (await AreaHttp.Errors(await client.PostAsJsonAsync("/api/account/forgot-password", new { email = "nao-e-email" }))).Keys.Should().Equal("email");
    }

    [Fact]
    public async Task Forgot_AnswersTheSame_WithOrWithoutAnAccount_AndEmailsOnlyAConfirmedOne()
    {
        var client = await VisitorAsync();
        var confirmed = await UserAsync(confirmed: true);
        var unconfirmed = await UserAsync(confirmed: false);
        var nobody = $"ninguem{Tag()}@test.com";

        var bodies = new List<string>();
        foreach (var email in new[] { confirmed.Email!, unconfirmed.Email!, nobody })
        {
            var response = await client.PostAsJsonAsync("/api/account/forgot-password", new { email });
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Headers.CacheControl!.NoStore.Should().BeTrue();
            bodies.Add(await response.Content.ReadAsStringAsync());
        }

        bodies.Distinct().Should().ContainSingle("nothing tells an account apart");
        JsonDocument.Parse(bodies[0]).RootElement.GetProperty("message").GetString().Should().Be(Neutral);
        _factory.Sent.Where(m => m.To == unconfirmed.Email || m.To == nobody).Should().BeEmpty();

        var mail = _factory.Sent.Single(m => m.To == confirmed.Email);
        mail.Subject.Should().Be("Recuperação de palavra-passe");
        var (code, linkEmail) = ResetLink(mail.Html);
        linkEmail.Should().Be(confirmed.Email);
        bodies[0].Should().NotContain(code, "the token only ever goes into the email");
    }

    [Fact]
    public async Task Forgot_AFailedSend_AnswersTheSameNeutralSentence()
    {
        var client = await VisitorAsync();
        var user = await UserAsync(confirmed: true);
        _factory.FailFor.Add(user.Email!);

        var result = await AreaHttp.Json(await client.PostAsJsonAsync("/api/account/forgot-password", new { email = user.Email }));
        result.GetProperty("message").GetString().Should().Be(Neutral);
    }

    /// <summary>Task 135: the link carries the configured address. The Host header is the caller's word.</summary>
    [Fact]
    public async Task Forgot_BuildsTheLinkFromTheConfiguredAddress_NotFromTheHostHeader()
    {
        var client = await VisitorAsync();
        var user = await UserAsync(confirmed: true);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/account/forgot-password") { Content = JsonContent.Create(new { email = user.Email }) };
        request.Headers.Host = "evil.example";

        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.OK);

        _factory.Sent.Single(m => m.To == user.Email).Html.Should()
            .Contain($"href=\"{AccountRecoveryFactory.BaseUrl}/reset-password?").And.NotContain("evil.example");
    }

    /// <summary>
    /// Task 135: the account calls spend the login budget of their client (10 per 5 minutes, as
    /// <see cref="LoginRateLimitTests"/>), so a script cannot fill a member's inbox.
    /// </summary>
    [Fact]
    public async Task Forgot_IsLimitedPerClient_AndSendsNothingOnceRefused()
    {
        const int permitLimit = 10;
        var client = await VisitorAsync();
        var user = await UserAsync(confirmed: true);

        for (var i = 1; i <= permitLimit; i++)
        {
            (await client.PostAsJsonAsync("/api/account/forgot-password", new { email = user.Email }))
                .StatusCode.Should().Be(HttpStatusCode.OK, $"request {i} is inside the limit");
        }

        var refused = await client.PostAsJsonAsync("/api/account/forgot-password", new { email = user.Email });

        refused.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        refused.Headers.RetryAfter.Should().NotBeNull();
        (await client.PostAsJsonAsync("/api/account/reset-password", new { email = user.Email, code = "x", password = "x", confirmPassword = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.TooManyRequests, "the three account calls share one budget");
        _factory.Sent.Count(m => m.To == user.Email).Should().Be(permitLimit);
    }

    // ---------- reset-password ----------

    [Fact]
    public async Task Reset_WithTheEmailedLink_ChangesThePassword_SignsNobodyIn_AndTheLinkThenStopsWorking()
    {
        var client = await VisitorAsync();
        var user = await UserAsync(confirmed: true);
        await client.PostAsJsonAsync("/api/account/forgot-password", new { email = user.Email });
        var (code, email) = ResetLink(_factory.Sent.Single(m => m.To == user.Email).Html);
        var password = TestSecret.NewPassword();
        await PasskeyApiTests.RegisterAsync(_factory.Services, user, new PasskeyApiTests.Device());

        var response = await client.PostAsJsonAsync("/api/account/reset-password", new { email, code, password, confirmPassword = password });
        var result = await AreaHttp.Json(response);
        result.GetProperty("succeeded").GetBoolean().Should().BeTrue();
        result.GetProperty("message").GetString().Should().StartWith("A sua palavra-passe foi redefinida com sucesso.");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        SetCookies(response).Should().NotContain(c => c.StartsWith(".AspNetCore.Identity.Application"), "a reset never signs in");
        (await CheckPasswordAsync(user.Id, password)).Should().BeTrue();
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            (await users.GetPasskeysAsync((await users.FindByIdAsync(user.Id))!)).Should().BeEmpty(
                "a reset takes the account back, so passkeys made before it go (task 057)");
        }

        var again = TestSecret.NewPassword();
        (await AreaHttp.Errors(await client.PostAsJsonAsync("/api/account/reset-password", new { email, code, password = again, confirmPassword = again })))
            .Keys.Should().Equal("reset");
        (await CheckPasswordAsync(user.Id, password)).Should().BeTrue();
    }

    [Fact]
    public async Task Reset_ValidatesTheLinkAndThePasswords()
    {
        var client = await VisitorAsync();
        var user = await UserAsync(confirmed: true);
        var good = TestSecret.NewPassword();

        (await AreaHttp.Errors(await client.PostAsJsonAsync("/api/account/reset-password", new { email = user.Email, code = "", password = good, confirmPassword = good })))
            .Keys.Should().Equal("code");
        (await AreaHttp.Errors(await client.PostAsJsonAsync("/api/account/reset-password", new { email = user.Email, code = "x", password = "curta", confirmPassword = "curta" })))
            .Keys.Should().Equal("password");
        (await AreaHttp.Errors(await client.PostAsJsonAsync("/api/account/reset-password", new { email = user.Email, code = "x", password = good, confirmPassword = good + "!" })))
            .Keys.Should().Equal("confirmPassword");
        var wrong = await AreaHttp.Errors(await client.PostAsJsonAsync("/api/account/reset-password", new { email = user.Email, code = "nao-e-um-token", password = good, confirmPassword = good }));
        wrong.Keys.Should().Equal("reset");
        wrong["reset"].Should().Equal("Este link de redefinição é inválido ou já expirou. Pede um novo link.");
        (await CheckPasswordAsync(user.Id, good)).Should().BeFalse();

        // An unknown address is answered as a success, as before: nothing tells an account apart.
        var unknown = await AreaHttp.Json(await client.PostAsJsonAsync("/api/account/reset-password",
            new { email = $"ninguem{Tag()}@test.com", code = "x", password = good, confirmPassword = good }));
        unknown.GetProperty("message").GetString().Should().Be("Redefinição de palavra-passe concluída.");
    }

    [Fact]
    public async Task EveryCall_NeedsTheAntiforgeryToken()
    {
        var client = AreaHttp.Anonymous(_factory, NextIp());
        var user = await UserAsync(confirmed: true);
        var password = TestSecret.NewPassword();

        (await client.PostAsJsonAsync("/api/account/forgot-password", new { email = user.Email })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync("/api/account/reset-password", new { email = user.Email, code = "x", password, confirmPassword = password }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync("/api/account/confirm-email", new { userId = user.Id, code = "x" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _factory.Sent.Should().NotContain(m => m.To == user.Email);
    }

    // ---------- helpers ----------

    private async Task<HttpClient> VisitorAsync()
    {
        var client = AreaHttp.Anonymous(_factory, NextIp());
        await AreaHttp.WithTokenAsync(client);
        return client;
    }

    private static string NextIp()
    {
        var n = Interlocked.Increment(ref _ip);
        return $"10.73.{n / 250}.{n % 250 + 1}";
    }

    private async Task<ApplicationUser> UserAsync(bool confirmed)
    {
        var tag = Tag();
        var user = new ApplicationUser
        {
            UserName = $"rec{tag}",
            Email = $"rec{tag}@test.com",
            EmailConfirmed = confirmed,
            FirstName = "Recupera",
            LastName = tag,
            Nickname = $"Rec{tag}",
        };
        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user, TestSecret.NewPassword()))
            .Succeeded.Should().BeTrue();
        return user;
    }

    private async Task<ApplicationUser> StoredAsync(string id)
    {
        using var scope = _factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(id))!;
    }

    private async Task<bool> CheckPasswordAsync(string id, string password)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await users.CheckPasswordAsync((await users.FindByIdAsync(id))!, password);
    }

    /// <summary>The reset link in the rendered email, its query decoded once as a browser does.</summary>
    private static (string Code, string Email) ResetLink(string html)
    {
        var href = Regex.Match(html, "href=\"(?<url>[^\"]*/reset-password\\?[^\"]*)\"").Groups["url"].Value;
        href.Should().NotBeEmpty("the email links the reset page");
        var url = new Uri(WebUtility.HtmlDecode(href));
        url.AbsolutePath.Should().Be("/reset-password");
        var query = QueryHelpers.ParseQuery(url.Query);
        return (query["code"].ToString(), query["email"].ToString());
    }

    private static string WebUtilityEncode(string value) => WebUtility.UrlEncode(value);

    private static IEnumerable<string> SetCookies(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];

    private static string Tag() => Guid.NewGuid().ToString("N")[..10];
}
