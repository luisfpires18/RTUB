using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Web.Extensions;

namespace RTUB.Web.Endpoints;

/// <summary>
/// Public request submission for the React portal (React track 003, docs/react-portal-pilot.md).
/// Anonymous by design; protected by antiforgery (form-bound, like POST /auth/login), a per-IP rate
/// limit of its own, a honeypot field and server-side validation through
/// <see cref="IPublicRequestService"/>. The only public request submission path.
/// </summary>
public static class PublicRequestEndpoints
{
    /// <summary>Hidden form field that people never see and bots tend to fill.</summary>
    public const string HoneypotField = "website";

    public static void MapPublicRequestEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/public");

        // Issues the antiforgery cookie and the matching form token for the React form.
        group.MapGet("/antiforgery-token", (HttpContext context, IAntiforgery antiforgery) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Json(new { fieldName = tokens.FormFieldName, token = tokens.RequestToken });
        });

        // [FromForm] makes this an antiforgery-validated form endpoint: the framework answers 400
        // before the handler runs when the token is missing or wrong. Do not switch it to JSON
        // without adding equivalent CSRF protection.
        group.MapPost("/requests", async (
                [FromForm] PublicRequestForm form,
                IPublicRequestService requests,
                ILogger<PublicRequestForm> logger) =>
            {
                if (!string.IsNullOrWhiteSpace(form.Website))
                {
                    // Answer like a success so the bot learns nothing; nothing is stored or sent.
                    logger.LogInformation("Public request dropped by the honeypot");
                    return Results.Ok(new { submitted = true });
                }

                var parseErrors = new Dictionary<string, string[]>();
                var preferredDate = ParseDate(form.PreferredDate, nameof(Request.PreferredDate),
                    "A data preferida é obrigatória", parseErrors);
                var isDateRange = string.Equals(form.IsDateRange, "true", StringComparison.OrdinalIgnoreCase);
                var endDate = isDateRange && !string.IsNullOrWhiteSpace(form.PreferredEndDate)
                    ? ParseDate(form.PreferredEndDate, nameof(Request.PreferredEndDate), "Data inválida", parseErrors)
                    : null;

                if (parseErrors.Count > 0)
                {
                    return ValidationProblem(parseErrors);
                }

                try
                {
                    var errors = await requests.SubmitAsync(new PublicRequestSubmission(
                        form.Name ?? string.Empty,
                        form.Email ?? string.Empty,
                        form.Phone ?? string.Empty,
                        form.EventType ?? string.Empty,
                        preferredDate!.Value,
                        isDateRange,
                        endDate,
                        form.Location ?? string.Empty,
                        form.Message));

                    return errors.Count > 0 ? ValidationProblem(errors) : Results.Ok(new { submitted = true });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Public request submission failed");
                    return Results.Problem(
                        title: "Não foi possível enviar o pedido. Tente novamente ou contacte a RTUB por email.",
                        statusCode: StatusCodes.Status500InternalServerError);
                }
            })
            .RequireRateLimiting(ServiceCollectionExtensions.PublicRequestRateLimitPolicy)
            .WithMetadata(new RequestSizeLimitAttribute(32 * 1024));
    }

    private static DateTime? ParseDate(string? value, string field, string requiredMessage, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = new[] { requiredMessage };
            return null;
        }

        if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        errors[field] = new[] { "Data inválida" };
        return null;
    }

    /// <summary>400 with field errors keyed like the React form (camelCase).</summary>
    private static IResult ValidationProblem(IReadOnlyDictionary<string, string[]> errors) =>
        Results.ValidationProblem(
            errors.ToDictionary(e => JsonNamingPolicy.CamelCase.ConvertName(e.Key), e => e.Value),
            title: "O pedido tem campos por corrigir.");
}

/// <summary>The React form's fields, as posted (dates as yyyy-MM-dd).</summary>
public sealed class PublicRequestForm
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? EventType { get; set; }
    public string? PreferredDate { get; set; }
    public string? IsDateRange { get; set; }
    public string? PreferredEndDate { get; set; }
    public string? Location { get; set; }
    public string? Message { get; set; }

    /// <summary>Honeypot (<see cref="PublicRequestEndpoints.HoneypotField"/>).</summary>
    public string? Website { get; set; }
}
