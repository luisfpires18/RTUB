using Microsoft.AspNetCore.Mvc;
using RTUB.Application.DTOs;
using RTUB.Application.Services;

namespace RTUB.Web.Endpoints;

/// <summary>
/// The React /forgot-password, /reset-password and /confirm-email. Public. Thin: the work is <see cref="AccountRecoveryService"/>. Every call is a POST with the antiforgery token in the
/// X-CSRF-TOKEN header, and nothing is cached. Tokens travel in the request body only, never back in a response.
/// The three calls spend the login budget of their client (429 when it is used up), so a script cannot fill a
/// member's inbox with reset emails.
/// </summary>
public static class AccountRecoveryEndpoints
{
    public static void MapAccountRecoveryEndpoints(this IEndpointRouteBuilder app)
    {
        var account = app.MapGroup("/api/account").AllowAnonymous()
            .AddEndpointFilter(EventEndpoints.NoStore)
            .AddEndpointFilter(EventEndpoints.RequireAntiforgery)
            .RequireRateLimiting(RTUB.Web.Extensions.ServiceCollectionExtensions.LoginRateLimitPolicy);

        account.MapPost("/forgot-password", async (ForgotPasswordInput input, HttpContext http, AccountRecoveryService service) =>
                ToResult(await service.ForgotAsync(input, SiteAddress.Of(http))))
            .WithMetadata(new RequestSizeLimitAttribute(4 * 1024));

        account.MapPost("/reset-password", async (ResetPasswordInput input, AccountRecoveryService service) =>
                ToResult(await service.ResetAsync(input)))
            .WithMetadata(new RequestSizeLimitAttribute(8 * 1024));

        account.MapPost("/confirm-email", async (ConfirmEmailInput input, AccountRecoveryService service) =>
                Results.Ok(await service.ConfirmAsync(input)))
            .WithMetadata(new RequestSizeLimitAttribute(8 * 1024));
    }

    private static IResult ToResult(EventResult<AccountRecoveryResult> result) => result.Status == EventResultStatus.Ok
        ? Results.Ok(result.Value)
        : Results.ValidationProblem((result.Errors ?? new Dictionary<string, string[]>()).ToDictionary(e => e.Key, e => e.Value),
            title: "Há campos por corrigir.");
}
