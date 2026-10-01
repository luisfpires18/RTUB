using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Core.Entities;
using Xunit;

namespace RTUB.Integration.Tests;

/// <summary>
/// The React /login (React track 007) against the unchanged POST /auth/login. The page posts the
/// same form, with the token from GET /api/public/antiforgery-token and Accept: application/json,
/// and reads { redirect } or 401 { error } instead of a redirect. Each test gets its own client IP,
/// so the per-IP login limit of one test never spends another's budget.
/// </summary>
public class ReactLoginTests : IntegrationTestBase
{
    private const string IdentityCookieName = ".AspNetCore.Identity.Application";
    private static int _nextIp;

    public ReactLoginTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    // ---------- outcomes ----------

    [Fact]
    public async Task ValidCredentials_SignIn_AndLandOnEvents()
    {
        var (user, password) = await CreateUserAsync("react-login-ok");
        var client = CreateClient();

        var response = await PostLoginAsync(client, user.UserName!, password);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<SignedIn>())!.Redirect.Should().Be("/events");
        SetCookies(response).Should().Contain(c => c.StartsWith(IdentityCookieName), "a valid login issues the sign-in cookie");

        var me = await client.GetFromJsonAsync<Me>("/api/account/me");
        me!.Authenticated.Should().BeTrue("the cookie from the JSON answer is a real session");
    }

    [Fact]
    public async Task Email_IsAcceptedInPlaceOfTheUserName()
    {
        var (user, password) = await CreateUserAsync("react-login-email");

        var response = await PostLoginAsync(CreateClient(), user.Email!, password);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WrongPassword_IsInvalid_AndCountsTowardLockout()
    {
        var (user, _) = await CreateUserAsync("react-login-wrong");

        var response = await PostLoginAsync(CreateClient(), user.UserName!, TestSecret.NewPassword());

        await ShouldFailWith(response, "Invalid");
        (await WithUserManager(async m => await m.GetAccessFailedCountAsync((await m.FindByIdAsync(user.Id))!)))
            .Should().Be(1, "every wrong password still counts toward Identity's lockout");
    }

    [Fact]
    public async Task UnknownUser_IsInvalid()
    {
        var response = await PostLoginAsync(CreateClient(), "nobody-here", TestSecret.NewPassword());

        await ShouldFailWith(response, "Invalid");
    }

    [Fact]
    public async Task UnconfirmedEmail_IsInvalid()
    {
        var (user, password) = await CreateUserAsync("react-login-unconfirmed", u => u.EmailConfirmed = false);

        await ShouldFailWith(await PostLoginAsync(CreateClient(), user.UserName!, password), "Invalid");
    }

    [Fact]
    public async Task ExpelledMember_CannotSignIn_EvenWithTheRightPassword()
    {
        var (user, password) = await CreateUserAsync("react-login-expelled", u => u.IsExpelled = true);

        await ShouldFailWith(await PostLoginAsync(CreateClient(), user.UserName!, password), "Expelled");
    }

    [Fact]
    public async Task LockedAccount_CannotSignIn_EvenWithTheRightPassword()
    {
        var (user, password) = await CreateUserAsync("react-login-locked");
        await WithUserManager(async m =>
            (await m.SetLockoutEndDateAsync((await m.FindByIdAsync(user.Id))!, DateTimeOffset.UtcNow.AddMinutes(5))).Succeeded);

        await ShouldFailWith(await PostLoginAsync(CreateClient(), user.UserName!, password), "Locked");
    }

    [Fact]
    public async Task FiveWrongPasswords_LockTheAccount()
    {
        var (user, password) = await CreateUserAsync("react-login-lockout");
        var client = CreateClient();

        for (var i = 0; i < 5; i++)
        {
            await ShouldFailWith(await PostLoginAsync(client, user.UserName!, TestSecret.NewPassword()), "Invalid");
        }

        await ShouldFailWith(await PostLoginAsync(client, user.UserName!, password), "Locked");
    }

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
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src", "RTUB.Web", "portal", "src", "Login.tsx"));
        var content = File.ReadAllText(Path.Combine(RepoRoot(), "src", "RTUB.Web", "portal", "src", "content.ts"));

        source.Should().Contain("Não há registo público").And.Contain("criadas e geridas pela própria tuna")
            .And.Contain("O portal é aberto a todos");
        Regex.IsMatch(source, @"/(register|signup|sign-up|registo)\b|Criar conta|Registar|Inscrever", RegexOptions.IgnoreCase)
            .Should().BeFalse("membership is created by the tuna, never by a public sign-up");
        source.Should().Contain("href={legacy.forgotPassword}");
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
    public async Task PasswordRecoveryAndConfirmation_StayReachableBlazorPages(string path)
    {
        var response = await CreateClient().GetAsync(path);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            (await response.Content.ReadAsStringAsync()).Should().Contain("blazor.web.js", "{0} is still a Blazor page", path);
        }
    }

    // ---------- helpers ----------

    private HttpClient CreateClient()
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var n = Interlocked.Increment(ref _nextIp);
        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, $"10.77.{n / 250}.{n % 250 + 1}");
        return client;
    }

    private static async Task<HttpResponseMessage> PostLoginAsync(
        HttpClient client, string userName, string password, string returnUrl = "", bool json = true)
    {
        var token = await AntiforgeryFormToken.FetchAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/login")
        {
            Content = LoginForm(token, userName, password, returnUrl)
        };
        if (json)
        {
            request.Headers.Accept.ParseAdd("application/json");
        }

        return await client.SendAsync(request);
    }

    private static FormUrlEncodedContent LoginForm(string? token, string userName, string password, string returnUrl)
    {
        var fields = new Dictionary<string, string>
        {
            ["Username"] = userName,
            ["Password"] = password,
            ["RememberMe"] = "false",
            ["ReturnUrl"] = returnUrl
        };
        if (token is not null)
        {
            fields[AntiforgeryFormToken.FieldName] = token;
        }

        return new FormUrlEncodedContent(fields);
    }

    private static async Task ShouldFailWith(HttpResponseMessage response, string error)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadFromJsonAsync<Failed>())!.Error.Should().Be(error);
        SetCookies(response).Should().NotContain(c => c.StartsWith(IdentityCookieName), "a refused login issues no session");
    }

    private async Task<(ApplicationUser User, string Password)> CreateUserAsync(string userName, Action<ApplicationUser>? configure = null)
    {
        var password = TestSecret.NewPassword();
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = userName + "@test.com",
            EmailConfirmed = true,
            FirstName = "React",
            LastName = "Login",
            Nickname = userName,
            PhoneNumber = "123456789"
        };
        configure?.Invoke(user);

        (await WithUserManager(async m => (await m.CreateAsync(user, password)).Succeeded)).Should().BeTrue();
        return (user, password);
    }

    private async Task<T> WithUserManager<T>(Func<UserManager<ApplicationUser>, Task<T>> action)
    {
        using var scope = Factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>());
    }

    private static IEnumerable<string> SetCookies(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values) ? values : Array.Empty<string>();

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src", "RTUB.Web")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find the repository root");
    }

    private sealed record SignedIn(string Redirect);
    private sealed record Failed(string Error);
    private sealed record Me(bool Authenticated);
}
