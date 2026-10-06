using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// The React /profile editor on /api/me (task 032, was the Blazor /member/profile) through the real host: real login,
/// antiforgery, SQLite and the old services. Image storage is the recording fake of <see cref="EventsApiFactory"/>, so
/// nothing reaches R2. Every test scopes its own members.
/// </summary>
public class ProfileApiTests : IClassFixture<EventsApiFactory>
{
    private static readonly byte[] Png = { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0, 0, 0, 0 };

    private readonly EventsApiFactory _factory;

    public ProfileApiTests(EventsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Visitors_GetNothing_AndTheOldUrlLandsOnTheReactProfile()
    {
        var anonymous = AreaHttp.Anonymous(_factory, "10.46.0.1");
        await AreaHttp.WithTokenAsync(anonymous);

        foreach (var path in new[] { "/api/me/profile", "/api/me/mentors?q=a" })
        {
            (await anonymous.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, path);
        }

        (await anonymous.PutAsJsonAsync("/api/me/profile/personal", new { firstName = "x" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PutAsJsonAsync("/api/me/profile/tuna", new { mentorId = (string?)null })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync("/api/me/instruments", new { instrument = "Guitarra", primary = false })).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.DeleteAsync("/api/me/instruments/1")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PutAsync("/api/me/instruments/1/primary", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsync("/api/me/photo", Photo(Png, "image/png"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PutAsJsonAsync("/api/me/subscription", new { subscribed = false })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync("/api/me/password", new { currentPassword = "a", newPassword = "b", confirmPassword = "b" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await anonymous.GetStringAsync("/profile")).Should().Contain("id=\"root\"", "/profile is React and tells a visitor how to sign in");
        var old = await anonymous.GetAsync("/member/profile");
        old.StatusCode.Should().Be(HttpStatusCode.Redirect);
        old.Headers.Location!.ToString().Should().Be("/profile");
    }

    [Fact]
    public async Task Members_ReadAndEdit_OnlyTheirOwnProfile()
    {
        var (me, user) = await SignInAsync();
        var (other, otherUser) = await SignInAsync();
        await AreaHttp.WithTokenAsync(me);

        var profile = await AreaHttp.Json(me, "/api/me/profile");
        profile.GetProperty("member").GetProperty("id").GetString().Should().Be(user.Id);
        profile.GetProperty("personal").GetProperty("email").GetString().Should().Be(user.Email);
        profile.GetProperty("personal").GetProperty("nicknameLocked").GetBoolean().Should().BeFalse();
        profile.GetProperty("instrumentOptions").GetArrayLength().Should().BeGreaterThan(5);
        profile.GetProperty("rank").GetProperty("level").GetInt32().Should().BeGreaterThan(0);

        var saved = await AreaHttp.Json(await me.PutAsJsonAsync("/api/me/profile/personal",
            Personal(user, firstName: "Renata", city: "  Mirandela ", degree: "Enfermagem", dateOfBirth: "2001-05-04")));
        saved.GetProperty("personal").GetProperty("firstName").GetString().Should().Be("Renata");
        saved.GetProperty("personal").GetProperty("city").GetString().Should().Be("Mirandela", "values are trimmed");
        saved.GetProperty("personal").GetProperty("dateOfBirth").GetString().Should().Be("2001-05-04");

        (await UserAsync(user.Id)).Degree.Should().Be("Enfermagem");
        (await UserAsync(otherUser.Id)).FirstName.Should().Be(otherUser.FirstName, "nobody edits anyone else here");
        (await AreaHttp.Json(other, "/api/me/profile")).GetProperty("member").GetProperty("id").GetString().Should().Be(otherUser.Id);
    }

    [Fact]
    public async Task Personal_IsValidated_AndAnEmailInUseIsRefused()
    {
        var (me, user) = await SignInAsync();
        var (_, otherUser) = await SignInAsync();
        await AreaHttp.WithTokenAsync(me);

        var errors = await AreaHttp.Errors(await me.PutAsJsonAsync("/api/me/profile/personal",
            Personal(user, firstName: "", email: "nao-e-email", city: new string('c', 101), dateOfBirth: "1850-01-01")));
        errors.Keys.Should().Contain(new[] { "firstName", "email", "city", "dateOfBirth" });

        (await AreaHttp.Errors(await me.PutAsJsonAsync("/api/me/profile/personal", Personal(user, email: otherUser.Email))))
            .Should().ContainKey("email").WhoseValue.Single().Should().Contain("outra conta");
        (await UserAsync(user.Id)).Email.Should().Be(user.Email);

        var changed = await AreaHttp.Json(await me.PutAsJsonAsync("/api/me/profile/personal", Personal(user, email: $"novo-{user.UserName}@test.com")));
        changed.GetProperty("personal").GetProperty("email").GetString().Should().Be($"novo-{user.UserName}@test.com");
        (await UserAsync(user.Id)).NormalizedEmail.Should().Be($"NOVO-{user.UserName}@TEST.COM".ToUpperInvariant());
    }

    [Fact]
    public async Task Nickname_IsLocked_ForAMemberWhoIsOnlyALeitao()
    {
        var (leitao, leitaoUser) = await SignInAsync();
        await SetAsync(leitaoUser.Id, u => u.Categories = [MemberCategory.Leitao]);
        await AreaHttp.WithTokenAsync(leitao);

        (await AreaHttp.Json(leitao, "/api/me/profile")).GetProperty("personal").GetProperty("nicknameLocked").GetBoolean().Should().BeTrue();
        (await AreaHttp.Errors(await leitao.PutAsJsonAsync("/api/me/profile/personal", Personal(leitaoUser, nickname: "Novo Nome"))))
            .Keys.Should().Equal("nickname");
        (await leitao.PutAsJsonAsync("/api/me/profile/personal", Personal(leitaoUser, city: "Bragança"))).StatusCode.Should().Be(HttpStatusCode.OK,
            "the rest of the form still saves, with the nickname as it is");
        (await UserAsync(leitaoUser.Id)).Nickname.Should().Be(leitaoUser.Nickname);

        var (caloiro, caloiroUser) = await SignInAsync();
        await SetAsync(caloiroUser.Id, u => u.Categories = [MemberCategory.Caloiro]);
        await AreaHttp.WithTokenAsync(caloiro);
        (await AreaHttp.Json(await caloiro.PutAsJsonAsync("/api/me/profile/personal", Personal(caloiroUser, nickname: "Bacalhau"))))
            .GetProperty("personal").GetProperty("nickname").GetString().Should().Be("Bacalhau");
        (await UserAsync(caloiroUser.Id)).UserName.Should().Be(caloiroUser.UserName, "the username does not follow the nickname here, as before");
    }

    [Fact]
    public async Task Tuna_LocksCompleteDates_AndThePadrinhoIsATunoAndNeverYourself()
    {
        var (me, user) = await SignInAsync();
        await SetAsync(user.Id, u =>
        {
            u.Categories = [MemberCategory.Tuno];
            (u.YearLeitao, u.MonthLeitao) = (2015, 10);
        });
        var (_, caloiro) = await SignInAsync();
        await SetAsync(caloiro.Id, u => u.Categories = [MemberCategory.Caloiro]);
        var (_, tuno) = await SignInAsync();
        await SetAsync(tuno.Id, u => u.Categories = [MemberCategory.Tuno]);
        await AreaHttp.WithTokenAsync(me);

        var tuna = (await AreaHttp.Json(me, "/api/me/profile")).GetProperty("tuna");
        tuna.GetProperty("showMentor").GetBoolean().Should().BeTrue();
        tuna.GetProperty("showCaloiro").GetBoolean().Should().BeTrue();
        tuna.GetProperty("showTuno").GetBoolean().Should().BeTrue();
        tuna.GetProperty("lockedDates").GetProperty("leitao").GetBoolean().Should().BeTrue();
        tuna.GetProperty("lockedDates").GetProperty("caloiro").GetBoolean().Should().BeFalse();

        (await AreaHttp.Errors(await me.PutAsJsonAsync("/api/me/profile/tuna", Tuna(2014, 10, null, null))))
            .Keys.Should().Equal("leitao");
        (await UserAsync(user.Id)).YearLeitao.Should().Be(2015);

        var saved = await AreaHttp.Json(await me.PutAsJsonAsync("/api/me/profile/tuna", Tuna(2015, 10, 2016, 3)));
        saved.GetProperty("tuna").GetProperty("lockedDates").GetProperty("caloiro").GetBoolean().Should().BeTrue("a complete pair is now locked");
        (await AreaHttp.Errors(await me.PutAsJsonAsync("/api/me/profile/tuna", Tuna(2015, 10, 2016, 4)))).Keys.Should().Equal("caloiro");
        (await AreaHttp.Errors(await me.PutAsJsonAsync("/api/me/profile/tuna", Tuna(2015, 10, 2016, 3, yearTuno: 1980, monthTuno: 1))))
            .Keys.Should().Equal("tuno");

        (await AreaHttp.Errors(await me.PutAsJsonAsync("/api/me/profile/tuna", Tuna(2015, 10, 2016, 3, mentorId: user.Id))))
            .Keys.Should().Equal("mentorId");
        (await AreaHttp.Errors(await me.PutAsJsonAsync("/api/me/profile/tuna", Tuna(2015, 10, 2016, 3, mentorId: caloiro.Id))))
            .Keys.Should().Equal("mentorId");
        var withMentor = await AreaHttp.Json(await me.PutAsJsonAsync("/api/me/profile/tuna", Tuna(2015, 10, 2016, 3, mentorId: tuno.Id)));
        withMentor.GetProperty("tuna").GetProperty("mentorId").GetString().Should().Be(tuno.Id);
        withMentor.GetProperty("tuna").GetProperty("mentorName").GetString().Should().NotBeNullOrEmpty();

        var found = (await AreaHttp.Json(me, $"/api/me/mentors?q={Uri.EscapeDataString(user.Nickname!)}")).EnumerateArray()
            .Select(m => m.GetProperty("id").GetString()).ToList();
        found.Should().NotContain(user.Id, "nobody is their own padrinho");
        var candidates = (await AreaHttp.Json(me, "/api/me/mentors?q=qpf")).EnumerateArray().Select(m => m.GetProperty("id").GetString()).ToList();
        candidates.Should().Contain(tuno.Id).And.NotContain(caloiro.Id, "a padrinho is a Tuno");
    }

    [Fact]
    public async Task Instruments_AddRemoveAndPrimary_AreTheMembersOwn()
    {
        var (me, user) = await SignInAsync();
        var (other, _) = await SignInAsync();
        await AreaHttp.WithTokenAsync(me);
        await AreaHttp.WithTokenAsync(other);

        var first = await Instruments(await me.PostAsJsonAsync("/api/me/instruments", new { instrument = "Guitarra", primary = false }));
        first.Should().ContainSingle().Which.Should().Be(("Guitarra", true), "the first instrument is the primary");
        (await AreaHttp.Errors(await me.PostAsJsonAsync("/api/me/instruments", new { instrument = "Guitarra", primary = false })))
            .Keys.Should().Equal("instrument");
        (await AreaHttp.Errors(await me.PostAsJsonAsync("/api/me/instruments", new { instrument = "Tuba", primary = false })))
            .Keys.Should().Equal("instrument");

        var two = await Instruments(await me.PostAsJsonAsync("/api/me/instruments", new { instrument = "Bandolim", primary = false }));
        two.Should().BeEquivalentTo(new[] { ("Guitarra", true), ("Bandolim", false) });
        var bandolim = await InstrumentIdAsync(user.Id, InstrumentType.Bandolim);
        (await Instruments(await me.PutAsync($"/api/me/instruments/{bandolim}/primary", null)))
            .Should().BeEquivalentTo(new[] { ("Guitarra", false), ("Bandolim", true) });

        (await other.DeleteAsync($"/api/me/instruments/{bandolim}")).StatusCode.Should().Be(HttpStatusCode.NotFound, "only your own instruments");
        (await other.PutAsync($"/api/me/instruments/{bandolim}/primary", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await Instruments(await me.DeleteAsync($"/api/me/instruments/{bandolim}")))
            .Should().Equal(new[] { ("Guitarra", true) }, "removing the primary promotes the next");
        (await me.DeleteAsync($"/api/me/instruments/{bandolim}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Photo_IsAnImageUpTo10MB_AndReplacesTheOldOne()
    {
        var (me, user) = await SignInAsync();
        const string oldUrl = "https://pub-test.r2.dev/images/test/profile/old.webp";
        var newUrl = $"https://pub-test.r2.dev/images/test/profile/{user.UserName}.png";
        await SetAsync(user.Id, u => u.ImageUrl = oldUrl);
        _factory.Storage.Setup(s => s.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), "profile", user.UserName!))
            .ReturnsAsync(newUrl);
        await AreaHttp.WithTokenAsync(me);

        (await AreaHttp.Errors(await me.PostAsync("/api/me/photo", Photo(Png, "text/plain")))).Keys.Should().Equal("photo");
        (await AreaHttp.Errors(await me.PostAsync("/api/me/photo", Photo(new byte[64], "image/png")))).Keys
            .Should().Equal(new[] { "photo" }, "the bytes must be an image, whatever the declared type");
        var tooBig = new byte[10 * 1024 * 1024 + 1];
        Png.CopyTo(tooBig, 0);
        (await AreaHttp.Errors(await me.PostAsync("/api/me/photo", Photo(tooBig, "image/png"))))
            .Should().ContainKey("photo").WhoseValue.Single().Should().Contain("10 MB");
        _factory.Storage.Verify(s => s.DeleteImageAsync(oldUrl), Times.Never);

        var stored = await AreaHttp.Json(await me.PostAsync("/api/me/photo", Photo(Png, "image/png")));
        stored.GetProperty("avatarUrl").GetString().Should().Be(newUrl);
        _factory.Storage.Verify(s => s.DeleteImageAsync(oldUrl), Times.Once, "the old photo is deleted when replaced");
        _factory.Storage.Verify(s => s.UploadImageAsync(It.IsAny<Stream>(), "profile-picture.png", "image/png", "profile", user.UserName!), Times.Once);
        (await UserAsync(user.Id)).ImageUrl.Should().Be(newUrl);
    }

    [Fact]
    public async Task EmailSubscription_TogglesOnAndOff()
    {
        var (me, user) = await SignInAsync();
        await AreaHttp.WithTokenAsync(me);

        (await AreaHttp.Json(await me.PutAsJsonAsync("/api/me/subscription", new { subscribed = false }))).GetProperty("subscribed").GetBoolean()
            .Should().BeFalse();
        (await UserAsync(user.Id)).Subscribed.Should().BeFalse();
        (await AreaHttp.Json(me, "/api/me/profile")).GetProperty("subscribed").GetBoolean().Should().BeFalse();
        (await AreaHttp.Json(await me.PutAsJsonAsync("/api/me/subscription", new { subscribed = true }))).GetProperty("subscribed").GetBoolean()
            .Should().BeTrue();
        (await UserAsync(user.Id)).Subscribed.Should().BeTrue();
    }

    [Fact]
    public async Task Password_IsChecked_ClearsRequirePasswordChange_AndKeepsTheSessionSignedIn()
    {
        var (me, user, password) = await SignInWithPasswordAsync(requirePasswordChange: true);
        await AreaHttp.WithTokenAsync(me);
        (await AreaHttp.Json(me, "/api/me/profile")).GetProperty("requirePasswordChange").GetBoolean().Should().BeTrue();

        (await AreaHttp.Errors(await me.PostAsJsonAsync("/api/me/password", Password("errada", "novaPalavra1", "novaPalavra1"))))
            .Keys.Should().Equal("currentPassword");
        (await AreaHttp.Errors(await me.PostAsJsonAsync("/api/me/password", Password(password, "curta", "curta")))).Keys.Should().Equal("newPassword");
        (await AreaHttp.Errors(await me.PostAsJsonAsync("/api/me/password", Password(password, "novaPalavra1", "outraPalavra1"))))
            .Keys.Should().Equal("confirmPassword");

        var changed = await me.PostAsJsonAsync("/api/me/password", Password(password, "novaPalavra1", "novaPalavra1"));
        changed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        CookieTestSession.RenewsIdentityCookie(changed).Should().BeTrue("the sign-in is refreshed with the new security stamp");

        var stored = await UserAsync(user.Id);
        stored.RequirePasswordChange.Should().BeFalse();
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            (await users.CheckPasswordAsync(stored, "novaPalavra1")).Should().BeTrue();
        }

        var after = await me.GetAsync("/api/me/profile");
        after.StatusCode.Should().Be(HttpStatusCode.OK, "the member is still signed in after changing the password");
        (await after.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requirePasswordChange").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Writes_NeedTheAntiforgeryHeader()
    {
        var (me, user) = await SignInAsync();

        (await me.PutAsJsonAsync("/api/me/profile/personal", Personal(user, firstName: "Sem token"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await me.PutAsJsonAsync("/api/me/subscription", new { subscribed = false })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await me.PostAsJsonAsync("/api/me/password", Password("a", "novaPalavra1", "novaPalavra1"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await UserAsync(user.Id)).FirstName.Should().Be(user.FirstName);
    }

    // ---------- helpers ----------

    private static object Personal(ApplicationUser user, string? firstName = null, string? nickname = null, string? email = null,
        string? city = null, string? degree = null, string? dateOfBirth = null) => new
        {
            firstName = firstName ?? user.FirstName,
            lastName = user.LastName,
            nickname = nickname ?? user.Nickname,
            email = email ?? user.Email,
            phoneNumber = user.PhoneNumber,
            dateOfBirth,
            city,
            degree,
        };

    private static object Tuna(int? yearLeitao, int? monthLeitao, int? yearCaloiro, int? monthCaloiro,
        int? yearTuno = null, int? monthTuno = null, string? mentorId = null) =>
        new { mentorId, yearLeitao, monthLeitao, yearCaloiro, monthCaloiro, yearTuno, monthTuno };

    private static object Password(string current, string next, string confirm) =>
        new { currentPassword = current, newPassword = next, confirmPassword = confirm };

    private static MultipartFormDataContent Photo(byte[] bytes, string contentType)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "photo", "profile-picture.png" } };
    }

    private static async Task<List<(string Instrument, bool Primary)>> Instruments(HttpResponseMessage response) =>
        (await AreaHttp.Json(response)).EnumerateArray()
            .Select(i => (i.GetProperty("instrument").GetString()!, i.GetProperty("primary").GetBoolean()))
            .ToList();

    private async Task<int> InstrumentIdAsync(string userId, InstrumentType type)
    {
        using var scope = _factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().MemberInstruments.AsNoTracking()
            .SingleAsync(i => i.MemberId == userId && i.InstrumentType == type)).Id;
    }

    private async Task<ApplicationUser> UserAsync(string id)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.AsNoTracking().SingleAsync(u => u.Id == id);
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
        return CookieTestSession.SignInAsync(_factory, $"qpf{Guid.NewGuid():N}"[..20], $"10.45.{n / 250}.{n % 250 + 1}");
    }

    /// <summary>As <see cref="CookieTestSession.SignInAsync"/>, keeping the password for the change-password test.</summary>
    private async Task<(HttpClient Client, ApplicationUser User, string Password)> SignInWithPasswordAsync(bool requirePasswordChange)
    {
        var n = Interlocked.Increment(ref _ip);
        var userName = $"qpf{Guid.NewGuid():N}"[..20];
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, $"10.45.{n / 250}.{n % 250 + 1}");
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = $"{userName}@test.com",
            EmailConfirmed = true,
            FirstName = "Perfil",
            LastName = "Teste",
            Nickname = userName,
            PhoneNumber = "123456789",
            RequirePasswordChange = requirePasswordChange,
        };
        var password = TestSecret.NewPassword();
        using (var scope = _factory.Services.CreateScope())
        {
            (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user, password)).Succeeded.Should().BeTrue();
        }

        var token = await AntiforgeryFormToken.FetchAsync(client);
        var login = await client.PostAsync("/auth/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [AntiforgeryFormToken.FieldName] = token!,
            ["Username"] = userName,
            ["Password"] = password,
            ["RememberMe"] = "false",
        }));
        login.StatusCode.Should().Be(HttpStatusCode.Redirect);
        return (client, user, password);
    }
}

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
        var old = await anonymous.GetAsync("/member/map");
        old.StatusCode.Should().Be(HttpStatusCode.Redirect);
        old.Headers.Location!.ToString().Should().Be("/members/map");

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

/// <summary>
/// The React /hall-of-fame on /api/hall-of-fame (task 032, was the Blazor page): the twelve records with ties, positive
/// durations only, and attended / "vou" activity on past, not-cancelled rehearsals and events.
/// </summary>
public class HallOfFameApiTests : IClassFixture<EventsApiFactory>
{
    private static readonly string[] Keys =
    {
        "ultimo-caloiro", "ultimo-tuno", "mais-afilhados", "mais-cargos", "mais-magister", "mais-instrumentos",
        "mais-tempo-leitao", "menos-tempo-leitao", "mais-tempo-caloiro", "menos-tempo-caloiro", "mais-ensaios", "mais-atuacoes",
    };

    private readonly EventsApiFactory _factory;

    public HallOfFameApiTests(EventsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Visitors_GetNothing_AndTheHallOfFameIsAReactMembersPage()
    {
        var anonymous = AreaHttp.Anonymous(_factory, "10.50.0.1");
        (await anonymous.GetAsync("/api/hall-of-fame")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var page = await anonymous.GetAsync("/hall-of-fame");
        page.StatusCode.Should().Be(HttpStatusCode.Redirect);
        page.Headers.Location!.ToString().Should().Be("/login?returnUrl=" + Uri.EscapeDataString("/hall-of-fame"));

        var (member, _) = await SignInAsync();
        (await member.GetStringAsync("/hall-of-fame")).Should().Contain("id=\"root\"").And.NotContain("blazor.web.js");
    }

    [Fact]
    public async Task Records_KeepTies_PositiveDurations_AndPastNotCancelledActivity()
    {
        var (member, _) = await SignInAsync();
        var a = await MemberAsync(u => { (u.YearLeitao, u.MonthLeitao, u.YearCaloiro, u.MonthCaloiro) = (1990, 1, 2090, 1); });
        var b = await MemberAsync(u => { (u.YearLeitao, u.MonthLeitao, u.YearCaloiro, u.MonthCaloiro) = (1990, 1, 2090, 1); });
        var oneMonth = await MemberAsync(u => { (u.YearLeitao, u.MonthLeitao, u.YearCaloiro, u.MonthCaloiro) = (2091, 1, 2091, 2); });
        var zero = await MemberAsync(u => { (u.YearLeitao, u.MonthLeitao, u.YearCaloiro, u.MonthCaloiro) = (2092, 5, 2092, 5); });
        var negative = await MemberAsync(u => { (u.YearLeitao, u.MonthLeitao, u.YearCaloiro, u.MonthCaloiro) = (2093, 6, 2093, 1); });
        var latestTuno = await MemberAsync(u => { (u.YearCaloiro, u.MonthCaloiro, u.YearTuno, u.MonthTuno) = (2098, 1, 2099, 12); });

        var mentor = await MemberAsync();
        await MemberAsync(u => u.MentorId = mentor.Id);
        await MemberAsync(u => u.MentorId = mentor.Id);
        await AssignAsync(mentor.Id, Position.Magister, 2001);
        await AssignAsync(mentor.Id, Position.Magister, 2002);
        await AssignAsync(mentor.Id, Position.Magister, 2003);
        var player = await MemberAsync();
        foreach (var type in new[] { InstrumentType.Guitarra, InstrumentType.Bandolim, InstrumentType.Cavaquinho, InstrumentType.Acordeao })
        {
            await InstrumentAsync(player.Id, type);
        }

        var r1 = await MemberAsync();
        var r2 = await MemberAsync();
        foreach (var r in new[] { r1, r2 })
        {
            for (var i = 1; i <= 3; i++)
            {
                await AttendAsync(r.Id, DateTime.UtcNow.AddDays(-i), attended: true);
            }
        }

        await AttendAsync(r1.Id, DateTime.UtcNow.AddDays(-5), attended: true, cancelled: true);
        await AttendAsync(r1.Id, DateTime.UtcNow.AddDays(5), attended: true);
        await AttendAsync(r1.Id, DateTime.UtcNow.AddDays(-6), attended: false);

        var performer = await MemberAsync();
        await EnrollAsync(performer.Id, DateTime.UtcNow.AddDays(-10), willAttend: true);
        await EnrollAsync(performer.Id, DateTime.UtcNow.AddDays(-11), willAttend: true);
        await EnrollAsync(performer.Id, DateTime.UtcNow.AddDays(-12), willAttend: true, cancelled: true);
        await EnrollAsync(performer.Id, DateTime.UtcNow.AddDays(12), willAttend: true);
        await EnrollAsync(performer.Id, DateTime.UtcNow.AddDays(-13), willAttend: false);

        var records = (await AreaHttp.Json(member, "/api/hall-of-fame")).GetProperty("categories").EnumerateArray()
            .ToDictionary(c => c.GetProperty("key").GetString()!);
        records.Keys.Should().Equal(Keys, "the twelve records, in the old page's order");

        Winners(records["mais-tempo-leitao"]).Should().BeEquivalentTo(new[] { a.Nickname, b.Nickname }, "a tie lists every winner");
        records["mais-tempo-leitao"].GetProperty("value").GetString().Should().Be("100 anos");
        Winners(records["menos-tempo-leitao"]).Should().Contain(oneMonth.Nickname).And.NotContain(new[] { zero.Nickname, negative.Nickname },
            "only a positive duration counts");
        records["menos-tempo-leitao"].GetProperty("value").GetString().Should().Be("1 mês");
        Winners(records["ultimo-tuno"]).Should().Equal(latestTuno.Nickname);
        records["ultimo-tuno"].GetProperty("value").GetString().Should().Be("dezembro 2099");
        Winners(records["mais-tempo-caloiro"]).Should().Contain(latestTuno.Nickname);
        records["mais-tempo-caloiro"].GetProperty("value").GetString().Should().Be("1 ano 11 meses");

        Winners(records["mais-afilhados"]).Should().Equal(mentor.Nickname);
        records["mais-afilhados"].GetProperty("value").GetString().Should().Be("2 afilhados");
        Winners(records["mais-magister"]).Should().Equal(mentor.Nickname);
        records["mais-magister"].GetProperty("value").GetString().Should().Be("3 mandatos");
        Winners(records["mais-cargos"]).Should().Equal(mentor.Nickname);
        Winners(records["mais-instrumentos"]).Should().Equal(player.Nickname);
        records["mais-instrumentos"].GetProperty("value").GetString().Should().Be("4 instrumentos");

        Winners(records["mais-ensaios"]).Should().BeEquivalentTo(new[] { r1.Nickname, r2.Nickname },
            "cancelled, future and not-attended rehearsals do not count, so r1 ties r2");
        records["mais-ensaios"].GetProperty("value").GetString().Should().Be("3 ensaios");
        Winners(records["mais-atuacoes"]).Should().Equal(performer.Nickname);
        records["mais-atuacoes"].GetProperty("value").GetString().Should().Be("2 atuações");
    }

    private static List<string?> Winners(JsonElement record) =>
        record.GetProperty("winners").EnumerateArray().Select(w => w.GetProperty("displayName").GetString()).ToList();

    private async Task<ApplicationUser> MemberAsync(Action<ApplicationUser>? setup = null)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var name = $"hof{Guid.NewGuid():N}"[..20];
        var user = new ApplicationUser
        {
            UserName = name,
            Email = $"{name}@test.com",
            EmailConfirmed = true,
            FirstName = "Hall",
            LastName = "Fame",
            Nickname = name,
            PhoneNumber = "912000000",
            Categories = [MemberCategory.Tuno],
        };
        setup?.Invoke(user);
        (await users.CreateAsync(user)).Succeeded.Should().BeTrue();
        return user;
    }

    private async Task AssignAsync(string userId, Position position, int startYear)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.RoleAssignments.Add(RoleAssignment.Create(userId, position, startYear, startYear + 1));
        await db.SaveChangesAsync();
    }

    private async Task InstrumentAsync(string userId, InstrumentType type)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.MemberInstruments.Add(MemberInstrument.Create(userId, type));
        await db.SaveChangesAsync();
    }

    private async Task AttendAsync(string userId, DateTime day, bool attended, bool cancelled = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rehearsal = Rehearsal.Create(day, "Centro Académico");
        rehearsal.IsCanceled = cancelled;
        db.Rehearsals.Add(rehearsal);
        await db.SaveChangesAsync();
        var attendance = RehearsalAttendance.Create(rehearsal.Id, userId);
        attendance.WillAttend = true;
        attendance.MarkAttendance(attended);
        db.RehearsalAttendances.Add(attendance);
        await db.SaveChangesAsync();
    }

    private async Task EnrollAsync(string userId, DateTime day, bool willAttend, bool cancelled = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var evt = Event.Create("Atuação do Hall of Fame", day, "Bragança", EventType.Atuacao);
        evt.IsCancelled = cancelled;
        db.Events.Add(evt);
        await db.SaveChangesAsync();
        var enrollment = Enrollment.Create(userId, evt.Id);
        enrollment.WillAttend = willAttend;
        db.Enrollments.Add(enrollment);
        await db.SaveChangesAsync();
    }

    private static int _ip;

    private Task<(HttpClient Client, ApplicationUser User)> SignInAsync()
    {
        var n = Interlocked.Increment(ref _ip);
        return CookieTestSession.SignInAsync(_factory, $"hof{Guid.NewGuid():N}"[..20], $"10.49.{n / 250}.{n % 250 + 1}");
    }
}

/// <summary>
/// The Hall of Fame on a database with only the seeded Owner and the signed-in member: every record is there, with no
/// winner and no value (the page says "sem dados").
/// </summary>
public class HallOfFameEmptyApiTests : IClassFixture<EventsApiFactory>
{
    private readonly EventsApiFactory _factory;

    public HallOfFameEmptyApiTests(EventsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task EveryRecord_IsThere_EmptyWhenNobodyQualifies()
    {
        var (member, _) = await CookieTestSession.SignInAsync(_factory, $"hof{Guid.NewGuid():N}"[..20], "10.51.0.1");

        var records = (await AreaHttp.Json(member, "/api/hall-of-fame")).GetProperty("categories").EnumerateArray().ToList();

        records.Should().HaveCount(12);
        foreach (var record in records)
        {
            record.GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
            record.GetProperty("winners").GetArrayLength().Should().Be(0, record.GetProperty("key").GetString());
            record.GetProperty("value").ValueKind.Should().Be(JsonValueKind.Null);
        }
    }
}

/// <summary>032: the three Blazor pages and what only they used are gone; the member menu links the React pages.</summary>
public class MemberAreaRetirementTests
{
    [Fact]
    public void BlazorPages_AndWhatOnlyTheyUsed_AreGone()
    {
        var src = Path.Combine(RepoRoot(), "src");
        foreach (var gone in new[]
                 {
                     "RTUB.Web/Pages/Members/Profile.razor", "RTUB.Web/Pages/Members/MemberMap.razor", "RTUB.Web/Pages/Activities/HallOfFame.razor",
                     "RTUB.Shared/Components/Profile/ProfileHeader.razor", "RTUB.Shared/Components/Profile/ProfileSection.razor",
                     "RTUB.Shared/Components/Ranking/RankCard.razor", "RTUB.Shared/Components/Profile/UnifiedTimeline.razor",
                     "RTUB.Shared/Components/Forms/MonthYearPicker.razor", "RTUB.Shared/Components/UI/PushNotificationToggle.razor",
                     "RTUB.Shared/Components/Uploads/ImageCropper.razor", "RTUB.Web/wwwroot/js/memberMap.js", "RTUB.Web/wwwroot/js/imageCropper.js",
                 })
        {
            File.Exists(Path.Combine(src, gone)).Should().BeFalse("{0} was retired in 032", gone);
        }

        var layout = File.ReadAllText(Path.Combine(src, "RTUB.Web", "Shared", "MainLayout.razor"));
        layout.Should().NotContain("leaflet").And.NotContain("cropper").And.NotContain("memberMap.js").And.NotContain("/member/profile");
        layout.Should().Contain("href=\"/hall-of-fame\" data-enhance-nav=\"false\"").And.Contain("href=\"/profile\" data-enhance-nav=\"false\"");

        var web = typeof(RTUB.App).Assembly;
        web.GetType("RTUB.Pages.Members.Profile").Should().BeNull();
        web.GetType("RTUB.Pages.Members.MemberMap").Should().BeNull();
        web.GetType("RTUB.Pages.Activities.HallOfFame").Should().BeNull();
    }

    [Fact]
    public void ReactPages_AreLinked_AndSayNoMigration()
    {
        var portal = Path.Combine(RepoRoot(), "src", "RTUB.Web", "portal", "src");
        var content = File.ReadAllText(Path.Combine(portal, "content.ts"));
        content.Should().Contain("membersMap: '/members/map'").And.Contain("hallOfFame: '/hall-of-fame'").And.NotContain("memberProfile");
        File.ReadAllText(Path.Combine(portal, "MemberShell.tsx")).Should().Contain("href: portal.membersMap,").And.Contain("href: portal.hallOfFame,");
        File.ReadAllText(Path.Combine(portal, "Members.tsx")).Should().Contain("href={portal.membersMap}");
        File.ReadAllText(Path.Combine(portal, "Leaderboard.tsx")).Should().Contain("href={portal.hallOfFame}");
        var main = File.ReadAllText(Path.Combine(portal, "main.tsx"));
        main.Should().Contain("'/members/map': lazy(").And.Contain("'/hall-of-fame': lazy(").And.Contain("'/profile': lazy(");

        foreach (var file in new[] { "Profile.tsx", "MembersMap.tsx", "HallOfFame.tsx", "memberAreaApi.ts" })
        {
            File.ReadAllText(Path.Combine(portal, file)).Should().NotContainAny(new[] { "migra", "Migra" }, file);
        }

        File.ReadAllText(Path.Combine(portal, "Profile.tsx")).Should().NotContain("push", "push notifications come back in task 033, not as a broken toggle");
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

/// <summary>The small HTTP helpers the member-area API tests share.</summary>
internal static class AreaHttp
{
    internal static HttpClient Anonymous(EventsApiFactory factory, string ip)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, ip);
        return client;
    }

    internal static async Task WithTokenAsync(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/public/antiforgery-token");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }

    internal static async Task<JsonElement> Json(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.StatusCode.Should().Be(HttpStatusCode.OK, path);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    internal static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    internal static async Task<Dictionary<string, string[]>> Errors(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("errors").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.EnumerateArray().Select(v => v.GetString()!).ToArray());
    }
}
