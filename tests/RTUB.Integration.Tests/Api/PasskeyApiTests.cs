using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Core.Entities;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// Face ID / fingerprint sign-in (task 057) through the real host: Identity's own WebAuthn verification, antiforgery,
/// SQLite. The "device" is a software ES256 key: the test registers its public key straight through UserManager and
/// signs the server's real challenge, so the whole assertion path runs, not a mock.
/// </summary>
public class PasskeyApiTests : IClassFixture<EventsApiFactory>
{
    private readonly EventsApiFactory _factory;

    public PasskeyApiTests(EventsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Visitors_ManageNothing_AndEveryPostNeedsTheToken()
    {
        var anonymous = AreaHttp.Anonymous(_factory, "10.57.0.1");

        (await anonymous.PostAsync("/auth/passkey/options", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest, "no antiforgery token");

        await AreaHttp.WithTokenAsync(anonymous);
        (await anonymous.GetAsync("/api/me/passkeys")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync("/api/me/passkeys/options", new { currentPassword = "x" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync("/api/me/passkeys", new { credential = "{}" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.DeleteAsync("/api/me/passkeys/AAAA")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var options = await AreaHttp.Json(await anonymous.PostAsync("/auth/passkey/options", null));
        options.GetProperty("challenge").GetString().Should().NotBeNullOrEmpty();

        var refused = await anonymous.PostAsJsonAsync("/auth/passkey/login", new { credential = "{\"id\":\"x\"}", rememberMe = false });
        refused.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().Should().Be("Invalid");
    }

    [Fact]
    public async Task Members_SeeAndRemove_OnlyTheirOwnPasskeys()
    {
        var (me, user, password) = await SignInAsync();
        var (other, _, _) = await SignInAsync();
        await AreaHttp.WithTokenAsync(me);
        await AreaHttp.WithTokenAsync(other);

        (await AreaHttp.Json(me, "/api/me/passkeys")).GetArrayLength().Should().Be(0);

        (await AreaHttp.Errors(await me.PostAsJsonAsync("/api/me/passkeys/options", new { currentPassword = "errada" })))
            .Keys.Should().Equal("currentPassword");
        (await AreaHttp.Errors(await me.PostAsJsonAsync("/api/me/passkeys/options", new { })))
            .Keys.Should().Equal(["currentPassword"], "an open session alone never enrols a passkey");

        var options = await AreaHttp.Json(await me.PostAsJsonAsync("/api/me/passkeys/options", new { currentPassword = password }));
        options.GetProperty("user").GetProperty("name").GetString().Should().Be(user.UserName);
        options.GetProperty("challenge").GetString().Should().NotBeNullOrEmpty();
        (await me.PostAsJsonAsync("/api/me/passkeys", new { credential = "{\"id\":\"x\"}" })).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "a credential that does not verify is never stored");

        var device = new Device();
        await RegisterAsync(_factory.Services, user, device);
        var list = await AreaHttp.Json(me, "/api/me/passkeys");
        list.GetArrayLength().Should().Be(1);
        var id = list[0].GetProperty("id").GetString()!;
        id.Should().Be(WebEncoders.Base64UrlEncode(device.CredentialId));

        (await other.DeleteAsync($"/api/me/passkeys/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound, "someone else's passkey");
        (await AreaHttp.Json(me, "/api/me/passkeys")).GetArrayLength().Should().Be(1);

        (await me.DeleteAsync($"/api/me/passkeys/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AreaHttp.Json(me, "/api/me/passkeys")).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task APasskey_SignsIn_WithoutAPassword_AndAnExpelledMemberIsStillRefused()
    {
        var (_, user, _) = await SignInAsync();
        var device = new Device();
        await RegisterAsync(_factory.Services, user, device);

        var visitor = AreaHttp.Anonymous(_factory, "10.57.1.1");
        var signedIn = await PasskeyLoginAsync(visitor, user, device, "/profile");
        signedIn.StatusCode.Should().Be(HttpStatusCode.OK, await signedIn.Content.ReadAsStringAsync());
        (await signedIn.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("redirect").GetString().Should().Be("/profile");
        (await visitor.GetAsync("/api/me/passkeys")).StatusCode.Should().Be(HttpStatusCode.OK, "the cookie is issued");

        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var stored = await users.FindByIdAsync(user.Id);
            (await users.GetPasskeyAsync(stored!, device.CredentialId))!.SignCount.Should().Be(1, "the counter is kept");
            stored!.LastLoginDate.Should().NotBeNull();
            stored.IsExpelled = true;
            (await users.UpdateAsync(stored)).Succeeded.Should().BeTrue();
        }

        var refused = await PasskeyLoginAsync(AreaHttp.Anonymous(_factory, "10.57.1.2"), user, device, null);
        refused.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().Should().Be("Expelled");
    }

    private async Task<HttpResponseMessage> PasskeyLoginAsync(HttpClient client, ApplicationUser user, Device device, string? returnUrl)
    {
        await AreaHttp.WithTokenAsync(client);
        var options = await AreaHttp.Json(await client.PostAsync("/auth/passkey/options", null));
        var credential = device.Sign(options.GetProperty("challenge").GetString()!, options.GetProperty("rpId").GetString()!, user.Id);
        // A browser sends Origin on this POST; Identity matches the signed origin against it.
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/passkey/login")
        {
            Content = JsonContent.Create(new { credential, rememberMe = false, returnUrl }),
        };
        request.Headers.Add("Origin", Origin);
        return await client.SendAsync(request);
    }

    internal static async Task RegisterAsync(IServiceProvider services, ApplicationUser user, Device device)
    {
        using var scope = services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var stored = await users.FindByIdAsync(user.Id);
        var passkey = new UserPasskeyInfo(device.CredentialId, device.CosePublicKey, DateTimeOffset.UtcNow, 0, ["internal"],
            isUserVerified: true, isBackupEligible: false, isBackedUp: false, attestationObject: [0xA0], clientDataJson: [0x7B, 0x7D])
        {
            Name = "Teste",
        };
        (await users.AddOrUpdatePasskeyAsync(stored!, passkey)).Succeeded.Should().BeTrue();
    }

    private const string Origin = "http://localhost";

    private static int _ip;

    /// <summary>A confirmed member signed in through the real login form, with the password kept for enrolment.</summary>
    private async Task<(HttpClient Client, ApplicationUser User, string Password)> SignInAsync()
    {
        var n = Interlocked.Increment(ref _ip);
        var userName = $"qpk{Guid.NewGuid():N}"[..20];
        var client = AreaHttp.Anonymous(_factory, $"10.57.{n / 250 + 10}.{n % 250 + 1}");
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = $"{userName}@test.com",
            EmailConfirmed = true,
            FirstName = "Passkey",
            LastName = "Teste",
            Nickname = userName,
            PhoneNumber = "123456789",
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

    /// <summary>A platform authenticator in software: one P-256 key, one credential id, a signature counter.</summary>
    internal sealed class Device
    {
        private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        private uint _count;

        public byte[] CredentialId { get; } = RandomNumberGenerator.GetBytes(16);

        /// <summary>COSE_Key {1: 2 (EC2), 3: -7 (ES256), -1: 1 (P-256), -2: x, -3: y} in CBOR.</summary>
        public byte[] CosePublicKey
        {
            get
            {
                var p = _key.ExportParameters(false).Q;
                return [0xA5, 0x01, 0x02, 0x03, 0x26, 0x20, 0x01, 0x21, 0x58, 0x20, .. p.X!, 0x22, 0x58, 0x20, .. p.Y!];
            }
        }

        public string Sign(string challenge, string rpId, string userId)
        {
            var clientData = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            {
                type = "webauthn.get",
                challenge,
                origin = Origin,
                crossOrigin = false,
            }));
            var counter = BitConverter.GetBytes(++_count);
            if (BitConverter.IsLittleEndian) Array.Reverse(counter);
            byte[] authData = [.. SHA256.HashData(Encoding.UTF8.GetBytes(rpId)), 0x05, .. counter]; // flags: user present + verified
            var signature = _key.SignData([.. authData, .. SHA256.HashData(clientData)], HashAlgorithmName.SHA256,
                DSASignatureFormat.Rfc3279DerSequence);

            return JsonSerializer.Serialize(new
            {
                id = WebEncoders.Base64UrlEncode(CredentialId),
                rawId = WebEncoders.Base64UrlEncode(CredentialId),
                type = "public-key",
                authenticatorAttachment = "platform",
                clientExtensionResults = new { },
                response = new
                {
                    clientDataJSON = WebEncoders.Base64UrlEncode(clientData),
                    authenticatorData = WebEncoders.Base64UrlEncode(authData),
                    signature = WebEncoders.Base64UrlEncode(signature),
                    userHandle = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(userId)),
                },
            });
        }
    }
}
