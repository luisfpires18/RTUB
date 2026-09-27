using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Core.Entities;
using Xunit;

namespace RTUB.Integration.Tests.Pages;

/// <summary>
/// The app shell contract from UI refactor 034 (docs/design/RTUB_UI_REFACTOR.md section 21),
/// checked on real responses: landmarks, skip link, document titles, current-location markup,
/// and the static-asset cache contract that lets changed CSS reach returning users.
/// The test host runs outside Development, so the production cache headers apply.
/// </summary>
public class AppShellTests : IntegrationTestBase
{
    public AppShellTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Theory]
    [InlineData("/", "RTUB - Real Tuna Universitária de Bragança")]
    [InlineData("/login", "Entrar - RTUB")]
    [InlineData("/privacy", "Política de Privacidade - RTUB")]
    [InlineData("/events", "Atuações - RTUB")]
    public async Task Page_HasMeaningfulTitle(string path, string title)
    {
        var html = await Factory.CreateClient().GetStringAsync(path);

        Regex.Matches(html, "<title>(.*?)</title>").Select(m => WebUtility.HtmlDecode(m.Groups[1].Value))
            .Should().Equal(title);
    }

    [Fact]
    public async Task Shell_HasSkipLinkFirst_AndLandmarks()
    {
        var html = await Factory.CreateClient().GetStringAsync("/login");
        var body = html[html.IndexOf("<body", StringComparison.Ordinal)..];

        var firstInteractive = Regex.Match(body, "<(a|button|input|select|textarea)\\b[^>]*>");
        firstInteractive.Value.Should().Contain("class=\"skip-link\"").And.Contain("href=\"#content\"");
        body.Should().Contain("Saltar para o conteúdo");
        body.IndexOf("class=\"skip-link\"", StringComparison.Ordinal)
            .Should().BeLessThan(body.IndexOf("<header", StringComparison.Ordinal));

        Regex.IsMatch(body, "<main id=\"content\"[^>]*tabindex=\"-1\"").Should().BeTrue();
        Regex.IsMatch(body, "<nav [^>]*aria-label=\"Principal\"").Should().BeTrue();
        Regex.Matches(body, "<header\\b").Should().HaveCount(1);
        Regex.Matches(body, "<main\\b").Should().HaveCount(1);
        Regex.Matches(body, "<footer\\b").Should().HaveCount(1);
        body.Should().Contain("aria-label=\"Abrir menu de navegação\"").And.Contain("aria-label=\"Fechar menu\"");
    }

    [Fact]
    public async Task Shell_MarksTheCurrentPage_AndItsSection()
    {
        var client = await SignedInClientAsync("shell-member");

        var events = await client.GetStringAsync("/events");
        Regex.IsMatch(events, "<a[^>]*href=\"/events\"[^>]*aria-current=\"page\"|<a[^>]*aria-current=\"page\"[^>]*href=\"/events\"")
            .Should().BeTrue("the current page's link carries aria-current");
        Regex.IsMatch(events, "class=\"nav-link dropdown-toggle active\"[^>]*id=\"membersDropdown\"").Should().BeFalse();

        var members = await client.GetStringAsync("/members");
        Regex.IsMatch(members, "class=\"nav-link dropdown-toggle active\"[^>]*id=\"membersDropdown\"")
            .Should().BeTrue("a menu is marked while one of its pages is open");
        members.Should().Contain("class=\"navbar-account__toggle\"").And.Contain("Conta de shell-member");
    }

    [Fact]
    public async Task GlobalStylesheets_AreLinkedVersioned_InsteadOfSiteCss()
    {
        var html = await Factory.CreateClient().GetStringAsync("/login");

        var cssLinks = Regex.Matches(html, "<link href=\"(/css/[^\"]+)\" rel=\"stylesheet\"")
            .Select(m => m.Groups[1].Value).ToList();

        cssLinks.Should().NotBeEmpty();
        cssLinks.Should().AllSatisfy(href => href.Should().MatchRegex(@"\?v=[\w-]+$"));
        cssLinks.Should().NotContain(href => href.StartsWith("/css/site.css"));
        cssLinks.Should().HaveCountGreaterThan(50, "every sheet site.css lists is linked on its own");
    }

    [Theory]
    [InlineData("/css/2-layout/navbar.css", "no-cache")]
    [InlineData("/js/navOffcanvas.js", "no-cache")]
    [InlineData("/css/2-layout/navbar.css?v=abc", "public,max-age=2592000")]
    [InlineData("/lib/bootstrap-icons/fonts/bootstrap-icons.woff2", "public,max-age=2592000")]
    public async Task StaticAsset_CacheControl_FollowsTheVersioningContract(string path, string cacheControl)
    {
        var response = await Factory.CreateClient().GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.ToString().Replace(" ", string.Empty).Should().Be(cacheControl);
    }

    private async Task<HttpClient> SignedInClientAsync(string userName)
    {
        var password = TestSecret.NewPassword();
        using (var scope = Factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var result = await userManager.CreateAsync(new ApplicationUser
            {
                UserName = userName,
                Email = $"{userName}@test.com",
                EmailConfirmed = true,
                FirstName = "Shell",
                LastName = "Member",
                Nickname = userName,
                PhoneNumber = "123456789"
            }, password);
            result.Succeeded.Should().BeTrue();
        }

        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var token = AntiforgeryFormToken.Find(await client.GetStringAsync("/login"));
        var login = await client.PostAsync("/auth/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [AntiforgeryFormToken.FieldName] = token!,
            ["Username"] = userName,
            ["Password"] = password,
            ["RememberMe"] = "false"
        }));
        login.StatusCode.Should().Be(HttpStatusCode.Redirect);
        return client;
    }
}
