// ProfileApiTests tests: instruments, photo, email subscription and password.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;
public partial class ProfileApiTests
{
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

    private static object Password(string current, string next, string confirm) =>
        new { currentPassword = current, newPassword = next, confirmPassword = confirm };

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
