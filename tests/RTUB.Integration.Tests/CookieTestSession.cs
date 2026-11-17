using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using RTUB.Core.Entities;

namespace RTUB.Integration.Tests;

/// <summary>
/// Signing a real user in through the real login form, shared by the two cookie-validation test
/// classes so they cannot drift apart on how a session is established.
/// </summary>
internal static class CookieTestSession
{
    /// <summary>
    /// The raw, unquoted statement the handler emits via <c>ExecuteSqlInterpolatedAsync</c>. EF Core
    /// quotes identifiers, so nothing else in the application collides with it.
    /// </summary>
    internal const string LastLoginWrite = "UPDATE AspNetUsers";

    internal const string IdentityCookie = ".AspNetCore.Identity.Application";

    internal static bool DeletesIdentityCookie(HttpResponseMessage response) =>
        SetCookies(response).Any(c => c.StartsWith($"{IdentityCookie}=;", StringComparison.Ordinal));

    /// <summary>A renewal writes the cookie back with a value; a rejection writes it back empty.</summary>
    internal static bool RenewsIdentityCookie(HttpResponseMessage response) =>
        SetCookies(response).Any(c =>
            c.StartsWith($"{IdentityCookie}=", StringComparison.Ordinal) &&
            !c.StartsWith($"{IdentityCookie}=;", StringComparison.Ordinal));

    private static IEnumerable<string> SetCookies(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies : [];

    /// <summary>
    /// Creates a confirmed user and signs it in through the real login form, antiforgery token and
    /// all. <paramref name="clientIp"/> gives each test its own login rate-limiting partition.
    /// </summary>
    internal static async Task<(HttpClient Client, ApplicationUser User)> SignInAsync(
        TestWebApplicationFactory factory, string userName, string clientIp, string? role = null)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, clientIp);

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = $"{userName}@test.com",
            EmailConfirmed = true,
            FirstName = "Cookie",
            LastName = "Validation",
            Nickname = userName,
            PhoneNumber = "123456789"
        };
        var password = TestSecret.NewPassword();

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            (await userManager.CreateAsync(user, password)).Succeeded.Should().BeTrue();
            if (role is not null)
            {
                (await userManager.AddToRoleAsync(user, role)).Succeeded.Should().BeTrue();
            }
        }

        var token = await AntiforgeryFormToken.FetchAsync(client);

        var loginResponse = await client.PostAsync("/auth/login", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                [AntiforgeryFormToken.FieldName] = token!,
                ["Username"] = userName,
                ["Password"] = password,
                ["RememberMe"] = "false"
            }));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        return (client, user);
    }
}
