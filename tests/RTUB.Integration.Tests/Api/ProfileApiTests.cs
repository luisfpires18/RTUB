// ProfileApiTests: test class moved here from MemberAreaApiTests.cs.
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;
/// <summary>
/// The React /profile editor on /api/me (task 032, was the Blazor /member/profile) through the real host: real login,
/// antiforgery, SQLite and the old services. Image storage is the recording fake of <see cref="EventsApiFactory"/>, so
/// nothing reaches R2. Every test scopes its own members.
/// </summary>
public partial class ProfileApiTests : IClassFixture<EventsApiFactory>
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
        profile.GetProperty("instrumentPictures").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
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
        withMentor.GetProperty("tuna").TryGetProperty("mentorAvatarUrl", out _).Should().BeTrue();

        var found = (await AreaHttp.Json(me, $"/api/me/mentors?q={Uri.EscapeDataString(user.Nickname!)}")).EnumerateArray()
            .Select(m => m.GetProperty("id").GetString()).ToList();
        found.Should().NotContain(user.Id, "nobody is their own padrinho");
        var candidates = (await AreaHttp.Json(me, "/api/me/mentors?q=qpf")).EnumerateArray().Select(m => m.GetProperty("id").GetString()).ToList();
        candidates.Should().Contain(tuno.Id).And.NotContain(caloiro.Id, "a padrinho is a Tuno");
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

    private static MultipartFormDataContent Photo(byte[] bytes, string contentType)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "photo", "profile-picture.png" } };
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
}
