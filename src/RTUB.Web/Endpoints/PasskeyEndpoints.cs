using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Helpers;

namespace RTUB.Web.Endpoints;

/// <summary>
/// Face ID / fingerprint sign-in (task 057, docs/react-identity.md → Passkeys): WebAuthn passkeys on ASP.NET Core
/// Identity's own implementation (schema Version3, table AspNetUserPasskeys). A member turns it on in /profile while
/// signed in; /login then signs in with the device's unlock, no password. The passkey is bound to the host it was made
/// on, so DEV and PROD keep separate ones. Every POST needs the antiforgery token in the X-CSRF-TOKEN header; the
/// challenge state lives in Identity's own cookie between the options call and the verify call.
/// </summary>
public static class PasskeyEndpoints
{
    /// <summary>Most passkeys one member keeps (one per phone / laptop is the normal case).</summary>
    internal const int MaxPerMember = 10;

    public static void MapPasskeyEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/auth/passkey").AllowAnonymous()
            .AddEndpointFilter(EventEndpoints.NoStore).AddEndpointFilter(EventEndpoints.RequireAntiforgery);

        // Discoverable credential: no username asked, the device offers its RTUB passkeys.
        auth.MapPost("/options", async (SignInManager<ApplicationUser> signIn) =>
            Results.Content(await signIn.MakePasskeyRequestOptionsAsync(null), "application/json"));

        // Same pre-checks (unconfirmed, locked, expelled) and the same tail as POST /auth/login. Answers
        // { redirect } or 401 { error }, like /auth/login with Accept: application/json.
        auth.MapPost("/login", async (PasskeyLoginInput input,
                    SignInManager<ApplicationUser> signIn,
                    UserManager<ApplicationUser> users,
                    ApplicationDbContext db,
                    ILogger<Program> logger,
                    AuditContext auditContext,
                    IMemoryCache cache) =>
                {
                    var assertion = await signIn.PerformPasskeyAssertionAsync(input.Credential);
                    if (!assertion.Succeeded)
                    {
                        logger.LogInformation("Passkey sign-in refused: {Reason}", assertion.Failure.Message);
                        return Fail("Invalid");
                    }

                    var user = assertion.User;
                    if (await Program.SignInBlockAsync(user, users) is { } blocked)
                    {
                        return Fail(blocked);
                    }

                    // Keeps the signature counter current (clone detection) and the last-used flags.
                    await users.AddOrUpdatePasskeyAsync(user, assertion.Passkey);

                    return await Program.CompleteSignInAsync(user, input.RememberMe, input.ReturnUrl, wantsJson: true,
                        signIn, users, db, logger, auditContext, cache);
                })
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024))
            .RequireRateLimiting(RTUB.Web.Extensions.ServiceCollectionExtensions.LoginRateLimitPolicy);

        // /profile → Segurança. Signed-in members only, their own passkeys only.
        var mine = app.MapGroup("/api/me/passkeys").AllowAnonymous().AddEndpointFilter(EventEndpoints.NoStore);

        mine.MapGet(string.Empty, async (HttpContext http, UserManager<ApplicationUser> users) =>
            await users.GetUserAsync(http.User) is { } user
                ? Results.Ok((await users.GetPasskeysAsync(user))
                    .OrderBy(p => p.CreatedAt)
                    .Select(p => new MyPasskey(Encode(p.CredentialId), p.Name ?? "Dispositivo", p.CreatedAt)))
                : SignInRequired());

        var writes = mine.MapGroup(string.Empty).AddEndpointFilter(EventEndpoints.RequireAntiforgery);

        // The current password first: an open session alone (a borrowed phone, a stolen cookie) must not be able to plant
        // a passkey that outlives it. The challenge state this call issues is what the POST below verifies against, so
        // the password gate covers the whole enrolment. Wrong passwords count towards the lockout, as at /auth/login.
        writes.MapPost("/options", async (PasskeyOptionsInput input, HttpContext http, UserManager<ApplicationUser> users,
            SignInManager<ApplicationUser> signIn) =>
        {
            if (await users.GetUserAsync(http.User) is not { } user) return SignInRequired();
            if (await users.IsLockedOutAsync(user)) return WrongPassword("Conta bloqueada de momento. Tente mais tarde.");
            if (!await users.CheckPasswordAsync(user, input.CurrentPassword ?? string.Empty))
            {
                await users.AccessFailedAsync(user);
                return WrongPassword("Palavra-passe incorreta.");
            }

            await users.ResetAccessFailedCountAsync(user);
            if ((await users.GetPasskeysAsync(user)).Count >= MaxPerMember) return TooMany();

            var entity = new PasskeyUserEntity
            {
                Id = await users.GetUserIdAsync(user),
                Name = user.UserName ?? user.Email ?? user.Id,
                DisplayName = string.IsNullOrWhiteSpace(user.Nickname) ? $"{user.FirstName} {user.LastName}".Trim() : user.Nickname,
            };
            return Results.Content(await signIn.MakePasskeyCreationOptionsAsync(entity), "application/json");
        });

        writes.MapPost(string.Empty, async (PasskeyCreateInput input, HttpContext http, UserManager<ApplicationUser> users,
                    SignInManager<ApplicationUser> signIn, ILogger<Program> logger) =>
                {
                    if (await users.GetUserAsync(http.User) is not { } user) return SignInRequired();
                    if ((await users.GetPasskeysAsync(user)).Count >= MaxPerMember) return TooMany();

                    var attestation = await signIn.PerformPasskeyAttestationAsync(input.Credential);
                    if (!attestation.Succeeded)
                    {
                        logger.LogInformation("Passkey for {UserName} refused: {Reason}", user.UserName, attestation.Failure.Message);
                        return Results.Problem(title: "Não foi possível ativar neste dispositivo. Tente de novo.",
                            statusCode: StatusCodes.Status400BadRequest);
                    }

                    var passkey = attestation.Passkey;
                    passkey.Name = UserAgentHelper.GetShortUserAgent(http.Request.Headers.UserAgent.ToString());
                    var saved = await users.AddOrUpdatePasskeyAsync(user, passkey);
                    if (!saved.Succeeded)
                    {
                        return Results.Problem(title: "Não foi possível guardar. Tente de novo.", statusCode: StatusCodes.Status400BadRequest);
                    }

                    logger.LogInformation("User {UserName} added a passkey ({Device})", user.UserName, passkey.Name);
                    return Results.Ok(new MyPasskey(Encode(passkey.CredentialId), passkey.Name, passkey.CreatedAt));
                })
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024));

        writes.MapDelete("/{id}", async (string id, HttpContext http, UserManager<ApplicationUser> users, ILogger<Program> logger) =>
        {
            if (await users.GetUserAsync(http.User) is not { } user) return SignInRequired();

            byte[] credentialId;
            try
            {
                credentialId = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlDecode(id);
            }
            catch (FormatException)
            {
                return NotFound();
            }

            // GetPasskeyAsync is scoped to this user: someone else's credential id is a 404, never a delete.
            if (await users.GetPasskeyAsync(user, credentialId) is null) return NotFound();

            await users.RemovePasskeyAsync(user, credentialId);
            logger.LogInformation("User {UserName} removed a passkey", user.UserName);
            return Results.NoContent();
        });
    }

    private static string Encode(byte[] id) => Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(id);

    private static IResult Fail(string error) => Results.Json(new { error }, statusCode: StatusCodes.Status401Unauthorized);

    private static IResult SignInRequired() => Results.Problem(title: "Reservado a membros da RTUB. Entre para continuar.",
        statusCode: StatusCodes.Status401Unauthorized);

    private static IResult TooMany() => Results.Problem(title: $"Já tem {MaxPerMember} dispositivos. Remova um antes de juntar outro.",
        statusCode: StatusCodes.Status409Conflict);

    private static IResult WrongPassword(string message) => Results.ValidationProblem(
        new Dictionary<string, string[]> { ["currentPassword"] = [message] }, title: "Há campos por corrigir.");

    private static IResult NotFound() => Results.Problem(title: "Dispositivo não encontrado.", statusCode: StatusCodes.Status404NotFound);
}

public sealed record PasskeyLoginInput(string Credential, bool RememberMe, string? ReturnUrl);

public sealed record PasskeyOptionsInput(string? CurrentPassword);

public sealed record PasskeyCreateInput(string Credential);

public sealed record MyPasskey(string Id, string Name, DateTimeOffset CreatedAt);
