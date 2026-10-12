using System.Text;
using Microsoft.AspNetCore.Mvc;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Web.Services;

namespace RTUB.Web.Endpoints;

/// <summary>
/// The React Owner tools (task 035, docs/react-owner-operations.md; were the Blazor /emails, /notifications, /users,
/// /owner/tracing and /owner/db). Owner only, every call: <see cref="RequireOwner"/> answers 401 to a visitor and 403
/// to any other member (Admin included, since /emails and /notifications were Admin pages) before anything is read or
/// written. Thin: the work is <see cref="IOwnerOperationsService"/>. Nothing is cached (no-store) and every write needs
/// the antiforgery token in the X-CSRF-TOKEN header.
/// </summary>
public static class OwnerEndpoints
{
    public const int AnnouncementMax = 500;

    public static void MapOwnerEndpoints(this IEndpointRouteBuilder app)
    {
        var owner = app.MapGroup("/api/owner").AllowAnonymous().AddEndpointFilter(EventEndpoints.NoStore).AddEndpointFilter(RequireOwner);
        var writes = owner.MapGroup(string.Empty).AddEndpointFilter(EventEndpoints.RequireAntiforgery);

        // ---------- /emails ----------

        owner.MapGet("/emails", async (IOwnerOperationsService service) => Results.Ok(await service.GetEmailRecipientsAsync()));

        writes.MapPut("/emails/recipients", async (OwnerSubscriptionInput input, IOwnerOperationsService service) =>
                ToResult(await service.SetSubscriptionAsync(input)))
            .WithMetadata(new RequestSizeLimitAttribute(256 * 1024));

        writes.MapPost("/emails", async (OwnerEmailInput input, IOwnerOperationsService service) =>
                ToResult(await service.SendEmailAsync(input)))
            .WithMetadata(new RequestSizeLimitAttribute(64 * 1024));

        // ---------- /notifications ----------

        owner.MapGet("/notifications", async (IOwnerOperationsService service) => Results.Ok(await service.GetPushRecipientsAsync()));

        writes.MapPost("/notifications", async (OwnerPushInput input, HttpContext http, IOwnerOperationsService service) =>
                ToResult(await service.SendPushAsync(input, http.User, $"{http.Request.Scheme}://{http.Request.Host}")))
            .WithMetadata(new RequestSizeLimitAttribute(256 * 1024));

        // ---------- /users ----------

        // ?q= &page= &pageSize=20|40|60|80|100
        owner.MapGet("/users", async (string? q, int? page, int? pageSize, IOwnerOperationsService service) =>
            Results.Ok(await service.GetUsersAsync(q, page, pageSize)));

        owner.MapGet("/users/{id}", async (string id, IOwnerOperationsService service) => ToResult(await service.GetUserAsync(id)));

        writes.MapPut("/users/{id}/role", async (string id, OwnerRoleInput input, IOwnerOperationsService service) =>
                ToResult(await service.ChangeRoleAsync(id, input)))
            .WithMetadata(new RequestSizeLimitAttribute(1024));

        writes.MapPost("/users/{id}/unlock", async (string id, IOwnerOperationsService service) => ToResult(await service.UnlockAsync(id)));

        writes.MapPost("/users/sync-status", async (IOwnerOperationsService service) => Results.Ok(await service.SyncMemberStatusesAsync()));

        // ---------- /owner/tracing ----------

        owner.MapGet("/tracing/filters", async (IOwnerOperationsService service) => Results.Ok(await service.GetAuditFiltersAsync()));

        // ?user= &excludeUser= &entityType= &action= &critical=true &q= &page= &pageSize=20|40|60|80|100
        owner.MapGet("/tracing", async (HttpContext http, IOwnerOperationsService service) =>
            Results.Ok(await service.GetAuditLogsAsync(Filter(http.Request.Query))));

        owner.MapGet("/tracing/export", async (HttpContext http, IOwnerOperationsService service) =>
            Results.File(Encoding.UTF8.GetBytes(await service.ExportAuditLogsAsync(Filter(http.Request.Query))), "application/json",
                $"tracing-export-{DateTime.UtcNow:yyyy-MM-dd-HHmmss}.json"));

        // ?from=yyyy-MM-dd &to=yyyy-MM-dd
        owner.MapGet("/tracing/logins", async (string? from, string? to, IOwnerOperationsService service) =>
            ToResult(await service.GetLoginsAsync(from, to)));

        writes.MapDelete("/tracing/{id:int}", async (int id, IOwnerOperationsService service) =>
            ToResult(await service.DeleteAuditLogAsync(id), _ => Results.NoContent()));

        writes.MapPost("/tracing/truncate", async (OwnerTruncateInput input, IOwnerOperationsService service) =>
                ToResult(await service.TruncateAuditLogsAsync(input), _ => Results.NoContent()))
            .WithMetadata(new RequestSizeLimitAttribute(1024));

        // The system announcement reaches the open Blazor pages (AnnouncementBanner), as before; nothing is stored.
        writes.MapPost("/tracing/announcement", async (OwnerAnnouncementInput input, AnnouncementService announcements) =>
            {
                var message = input.Message?.Trim();
                if (string.IsNullOrEmpty(message) || message.Length > AnnouncementMax)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["message"] = [string.IsNullOrEmpty(message) ? "Escreva o anúncio." : $"Máximo de {AnnouncementMax} caracteres."],
                    }, title: "Há campos por corrigir.");
                }

                await announcements.BroadcastAsync(message);
                return Results.NoContent();
            })
            .WithMetadata(new RequestSizeLimitAttribute(8 * 1024));

        // ---------- /owner/db ----------

        owner.MapGet("/db/tables", async (IOwnerOperationsService service) => Results.Ok(await service.GetTablesAsync()));

        // ?page= &pageSize=10|25|50|100
        owner.MapGet("/db/tables/{table}", async (string table, int? page, int? pageSize, IOwnerOperationsService service) =>
            ToResult(await service.GetTableAsync(table, page, pageSize)));

        writes.MapPost("/db/select", async (OwnerDbQueryInput input, IOwnerOperationsService service) =>
                ToResult(await service.RunSelectAsync(input)))
            .WithMetadata(new RequestSizeLimitAttribute(64 * 1024));

        writes.MapPost("/db/modify", async (OwnerDbModifyInput input, IOwnerOperationsService service) =>
                ToResult(await service.RunModifyAsync(input)))
            .WithMetadata(new RequestSizeLimitAttribute(64 * 1024));
    }

    /// <summary>Visitors 401, every other member 403, the Owner through. Runs before the antiforgery check.</summary>
    internal static async ValueTask<object?> RequireOwner(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            return Results.Problem(title: "Reservado a membros da RTUB. Entre para continuar.", statusCode: StatusCodes.Status401Unauthorized);
        }

        if (!user.IsInRole("Owner"))
        {
            return Results.Problem(title: "Reservado ao Owner.", statusCode: StatusCodes.Status403Forbidden);
        }

        return await next(context);
    }

    private static OwnerAuditFilter Filter(IQueryCollection query) => new(
        query["user"], query["excludeUser"], query["entityType"], query["action"],
        string.Equals(query["critical"], "true", StringComparison.OrdinalIgnoreCase), query["q"],
        int.TryParse(query["page"], out var page) ? page : 1, int.TryParse(query["pageSize"], out var size) ? size : 20);

    private static IResult ToResult<T>(EventResult<T> result, Func<T, IResult>? ok = null) => result.Status switch
    {
        EventResultStatus.Ok => ok is null ? Results.Ok(result.Value) : ok(result.Value!),
        EventResultStatus.Invalid => Results.ValidationProblem(
            (result.Errors ?? new Dictionary<string, string[]>()).ToDictionary(e => e.Key, e => e.Value), title: "Há campos por corrigir."),
        _ => Results.Problem(title: "Não encontrado.", statusCode: StatusCodes.Status404NotFound),
    };
}
