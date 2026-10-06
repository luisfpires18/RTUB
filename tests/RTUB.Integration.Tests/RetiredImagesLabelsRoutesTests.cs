using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace RTUB.Integration.Tests;

/// <summary>
/// Task 029A retired the Blazor admin pages /images (homepage slideshows, shown nowhere since React task 004) and
/// /labels (editable site texts; the Leaderboard story, their last reader, is fixed in code now) with no replacement:
/// both URLs are plain 404s (no redirect) for visitors and admins alike, and the Admin menu no longer offers them. The
/// Labels and Slideshows tables stay until 029B removes them with a migration.
/// </summary>
public class RetiredImagesLabelsRoutesTests : IntegrationTestBase
{
    private static readonly string[] RetiredUrls = ["/images", "/labels"];

    public RetiredImagesLabelsRoutesTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Theory]
    [InlineData("/images")]
    [InlineData("/labels")]
    public async Task RetiredPage_IsNotFound_ForVisitors(string url)
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        (await client.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.NotFound, "{0} was retired in 029A", url);
    }

    [Fact]
    public async Task OnlyTheBareImagesUrl_IsRetired_TheImageFilesUnderItAreStillServed()
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        (await client.GetAsync("/images")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync("/images/default-avatar.webp")).StatusCode.Should().Be(HttpStatusCode.OK,
            "ImagesController still serves wwwroot/images");
    }

    [Fact]
    public async Task RetiredUrls_AreNotFound_ForAnAdmin_AndTheAdminMenuNoLongerOffersThem()
    {
        var (admin, _) = await CookieTestSession.SignInAsync(Factory, "retired-images-admin", "10.95.0.1", "Admin");

        foreach (var url in RetiredUrls)
        {
            (await admin.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.NotFound, "{0} was retired in 029A", url);
        }

        var page = await admin.GetAsync("/hall-of-fame");
        page.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await page.Content.ReadAsStringAsync();
        html.Should().Contain("href=\"/emails\"", "the Admin menu is rendered, so the absences below mean something");
        html.Should().NotContain("href=\"/images\"").And.NotContain("href=\"/labels\"");
    }

    [Fact]
    public void NoBlazorPage_RoutesToTheRetiredUrls()
    {
        typeof(RTUB.App).Assembly.GetTypes()
            .SelectMany(t => t.GetCustomAttributes(typeof(RouteAttribute), false).Cast<RouteAttribute>())
            .Select(r => r.Template)
            .Should().NotContain(RetiredUrls);
    }
}
