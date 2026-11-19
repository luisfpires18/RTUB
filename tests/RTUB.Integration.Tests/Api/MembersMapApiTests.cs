// MembersMapApiTests: test class moved here from MemberAreaApiTests.cs.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;
/// <summary>
/// The React /members/map on /api/members/map (task 032, was the Blazor /member/map): city-level only, from the
/// geocoding cache, with the members without a city and the cities still waiting for the background worker.
/// </summary>
public class MembersMapApiTests : IClassFixture<EventsApiFactory>
{
    private readonly EventsApiFactory _factory;

    public MembersMapApiTests(EventsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Visitors_GetNothing_AndTheMapIsAReactMembersPage()
    {
        var anonymous = AreaHttp.Anonymous(_factory, "10.48.0.1");
        (await anonymous.GetAsync("/api/members/map")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var page = await anonymous.GetAsync("/members/map");
        page.StatusCode.Should().Be(HttpStatusCode.Redirect);
        page.Headers.Location!.ToString().Should().Be("/login?returnUrl=" + Uri.EscapeDataString("/members/map"));

        var (member, _) = await SignInAsync();
        (await member.GetStringAsync("/members/map")).Should().Contain("id=\"root\"").And.NotContain("blazor.web.js");
    }

    [Fact]
    public async Task Members_SeeCitiesGrouped_WithoutCityAndPending_AtCityLevelOnly()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var city = $"Qcidade{tag}";
        await CacheAsync(city.ToLowerInvariant(), 41.8, -6.75);
        var (leitao, leitaoUser) = await SignInAsync();
        await SetAsync(leitaoUser.Id, u => { u.City = $"  {city} "; u.Categories = [MemberCategory.Leitao]; });
        var (_, second) = await SignInAsync();
        await SetAsync(second.Id, u => u.City = city.ToUpperInvariant());
        var (_, expelled) = await SignInAsync();
        await SetAsync(expelled.Id, u => { u.City = city; u.IsExpelled = true; });
        var (_, nowhere) = await SignInAsync();
        await SetAsync(nowhere.Id, u => u.City = "   ");
        var (_, waiting) = await SignInAsync();
        await SetAsync(waiting.Id, u => u.City = $"Qespera{tag}");

        var response = await leitao.GetAsync("/api/members/map");
        response.StatusCode.Should().Be(HttpStatusCode.OK, "Leitões see the map, as before");
        var map = await response.Content.ReadFromJsonAsync<JsonElement>();

        var group = map.GetProperty("cities").EnumerateArray()
            .Single(c => string.Equals(c.GetProperty("name").GetString(), city, StringComparison.OrdinalIgnoreCase));
        group.GetProperty("latitude").GetDouble().Should().Be(41.8);
        group.GetProperty("longitude").GetDouble().Should().Be(-6.75);
        group.GetProperty("members").EnumerateArray().Select(m => m.GetProperty("displayName").GetString())
            .Should().BeEquivalentTo(new[] { leitaoUser.Nickname, second.Nickname, expelled.Nickname },
                "the city is grouped trimmed and case-insensitively; every account counts, as before (expelled included)");

        map.GetProperty("withoutCity").EnumerateArray().Select(m => m.GetProperty("displayName").GetString()).Should().Contain(nowhere.Nickname);
        map.GetProperty("pending").EnumerateArray().Select(c => c.GetString()).Should().Contain($"Qespera{tag}");
        map.GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(5);

        var names = new HashSet<string>();
        Collect(map, names);
        names.Should().BeSubsetOf(new[] { "total", "cities", "withoutCity", "pending", "name", "latitude", "longitude", "members",
            "displayName", "fullName", "avatarUrl" }, "city-level only: no id, email, phone or address");
        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain(leitaoUser.Email!).And.NotContain("123456789").And.NotContain(leitaoUser.Id);
    }

    private static void Collect(JsonElement e, HashSet<string> names)
    {
        if (e.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in e.EnumerateObject())
            {
                names.Add(p.Name);
                Collect(p.Value, names);
            }
        }
        else if (e.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in e.EnumerateArray())
            {
                Collect(item, names);
            }
        }
    }

    private async Task CacheAsync(string city, double latitude, double longitude)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.GeocodingCaches.Add(new GeocodingCache { CityName = city, CountryCode = "PT", Latitude = latitude, Longitude = longitude, Source = "Test" });
        await db.SaveChangesAsync();
    }

    private async Task SetAsync(string userId, Action<ApplicationUser> change)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId);
        change(user!);
        (await users.UpdateAsync(user!)).Succeeded.Should().BeTrue();
    }

    private static int _ip;

    private Task<(HttpClient Client, ApplicationUser User)> SignInAsync()
    {
        var n = Interlocked.Increment(ref _ip);
        return CookieTestSession.SignInAsync(_factory, $"qmp{Guid.NewGuid():N}"[..20], $"10.47.{n / 250}.{n % 250 + 1}");
    }
}
