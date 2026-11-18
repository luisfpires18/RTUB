// ReactLoginTests: ReturnUrlAndRoutes tests.
using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace RTUB.Integration.Tests;

public partial class ReactLoginTests
{
    // ---------- return URL ----------

    [Theory]
    [InlineData("/profile", "/profile")]
    [InlineData("/music/albums/3?from=share", "/music/albums/3?from=share")]
    [InlineData("", "/events")]
    [InlineData("https://evil.example/", "/events")]
    [InlineData("//evil.example", "/events")]
    [InlineData("/\\evil.example", "/events")]
    [InlineData("javascript:alert(1)", "/events")]
    public async Task ReturnUrl_IsHonouredOnlyWhenLocal(string returnUrl, string expected)
    {
        var (user, password) = await CreateUserAsync("react-login-return-" + Guid.NewGuid().ToString("N")[..8]);

        var response = await PostLoginAsync(CreateClient(), user.UserName!, password, returnUrl);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<SignedIn>())!.Redirect.Should().Be(expected);
    }

    [Fact]
    public async Task PlainFormPost_StillRedirects_ToALocalReturnUrl()
    {
        var (user, password) = await CreateUserAsync("react-login-form");

        var response = await PostLoginAsync(CreateClient(), user.UserName!, password, "/profile", json: false);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Be("/profile");
    }

    // ---------- protections kept ----------

    [Fact]
    public async Task JsonLogin_WithoutAntiforgeryToken_IsRejected()
    {
        var (user, password) = await CreateUserAsync("react-login-csrf");
        var client = CreateClient();
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");

        var response = await client.PostAsync("/auth/login", LoginForm(null, user.UserName!, password, ""));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        SetCookies(response).Should().NotContain(c => c.StartsWith(IdentityCookieName));
    }

    [Fact]
    public async Task JsonLogin_IsRateLimitedPerClient()
    {
        var client = CreateClient();

        for (var i = 0; i < 10; i++)
        {
            (await PostLoginAsync(client, "rl-react-probe", "x")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        (await PostLoginAsync(client, "rl-react-probe", "x")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    // ---------- the page ----------

    [Theory]
    [InlineData("/login")]
    [InlineData("/login?ReturnUrl=%2Fadmin")]
    public async Task SignedInMember_OpeningLogin_GoesToEvents_WithoutALoop(string path)
    {
        // /login is also the cookie's AccessDeniedPath: honouring ReturnUrl here would send a member
        // straight back to a page their role cannot open.
        var (user, password) = await CreateUserAsync("react-login-signed-" + Guid.NewGuid().ToString("N")[..8]);
        var client = CreateClient();
        (await PostLoginAsync(client, user.UserName!, password)).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Be("/events");
    }

    [Fact]
    public async Task ExpelledMember_WithAnOldCookie_GetsTheLoginPage()
    {
        var (user, password) = await CreateUserAsync("react-login-expelled-later");
        var client = CreateClient();
        (await PostLoginAsync(client, user.UserName!, password)).StatusCode.Should().Be(HttpStatusCode.OK);

        await WithUserManager(async m =>
        {
            var u = (await m.FindByIdAsync(user.Id))!;
            u.IsExpelled = true;
            return (await m.UpdateAsync(u)).Succeeded;
        });

        var response = await client.GetAsync("/login");

        response.StatusCode.Should().Be(HttpStatusCode.OK, "the cookie validator drops an expelled member's session");
        (await response.Content.ReadAsStringAsync()).Should().Contain("id=\"root\"");
    }

    [Fact]
    public void LoginPage_IsMembersOnly_WithNoSignUpPath_AndKeepsPasswordRecovery()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src", "RTUB.Web", "portal", "src", "pages/Login.tsx"));
        var content = File.ReadAllText(Path.Combine(RepoRoot(), "src", "RTUB.Web", "portal", "src", "data/content.ts"));

        source.Should().Contain("Não há registo público").And.Contain("criadas e geridas pela própria tuna")
            .And.Contain("O portal é aberto a todos");
        Regex.IsMatch(source, @"/(register|signup|sign-up|registo)\b|Criar conta|Registar|Inscrever", RegexOptions.IgnoreCase)
            .Should().BeFalse("membership is created by the tuna, never by a public sign-up");
        source.Should().Contain("href={portal.forgotPassword}");
        content.Should().Contain("forgotPassword: '/forgot-password'");
        source.Should().Contain("signIn(").And.NotContain("fetch(", "signing in goes through api.ts and POST /auth/login");
    }

    [Theory]
    [InlineData("/register")]
    [InlineData("/signup")]
    [InlineData("/account/register")]
    public async Task NoPublicRegistrationRoute_Exists(string path)
    {
        var response = await CreateClient().GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/forgot-password")]
    [InlineData("/reset-password")]
    [InlineData("/confirm-email")]
    public async Task PasswordRecoveryAndConfirmation_AreReactPages_NeverStored_NeverReferred(string path)
    {
        var response = await CreateClient().GetAsync(path + "?code=abc&email=a%40b.pt&userId=x");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue("{0} links carry an Identity token", path);
        response.Headers.GetValues("Referrer-Policy").Should().Equal("no-referrer");
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("id=\"root\"").And.NotContain("blazor.web.js", "{0} is React since task 036", path);
        html.Should().NotContain("abc", "the server never echoes the token");
    }
}
