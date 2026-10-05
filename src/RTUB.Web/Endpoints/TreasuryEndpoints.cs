using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;

namespace RTUB.Web.Endpoints;

/// <summary>
/// The React /treasury pages ("Tesouraria", React track 024, docs/react-treasury.md; were the Blazor /finance,
/// /finance/report/{id}, /calotes, /mbway and /nerba pages). Thin: every rule lives in <see cref="ITreasuryService"/> and
/// <see cref="ITreasuryRecordsService"/>, decided from the session. Signed-in members only; every write needs the
/// antiforgery token in the X-CSRF-TOKEN header.
/// </summary>
public static class TreasuryEndpoints
{
    public static void MapTreasuryEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/treasury").AllowAnonymous().AddEndpointFilter(EventEndpoints.NoStore);
        var writes = api.MapGroup(string.Empty).AddEndpointFilter(EventEndpoints.RequireAntiforgery);
        var small = new RequestSizeLimitAttribute(64 * 1024);
        var withReceipt = new RequestSizeLimitAttribute(TreasuryService.MaxReceiptBytes + 64 * 1024);

        // Reports
        api.MapGet("/", async (HttpContext http, ITreasuryService s) => ToResult(await s.GetReportsAsync(http.User)));
        writes.MapPost("/reports", async (TreasuryReportInput input, HttpContext http, ITreasuryService s) =>
            ToResult(await s.CreateReportAsync(input, http.User), r => Results.Created($"/api/treasury/reports/{r.Id}", r))).WithMetadata(small);
        writes.MapPost("/reports/{id:int}/publish", async (int id, HttpContext http, ITreasuryService s) =>
            ToResult(await s.PublishReportAsync(id, http.User))).WithMetadata(small);
        writes.MapDelete("/reports/{id:int}", async (int id, HttpContext http, ITreasuryService s) =>
            ToResult(await s.DeleteReportAsync(id, http.User), _ => Results.NoContent()));
        api.MapGet("/reports/{id:int}", async (int id, HttpContext http, ITreasuryService s) => ToResult(await s.GetReportAsync(id, http.User)));
        api.MapGet("/reports/{id:int}/pdf", async (int id, HttpContext http, ITreasuryService s) =>
            ToResult(await s.GetReportPdfAsync(id, http.User), f => Results.File(f.Content, "application/pdf", f.FileName)));
        api.MapGet("/reports/{id:int}/history", async (int id, int? page, int? pageSize, HttpContext http, ITreasuryService s) =>
            ToResult(await s.GetHistoryAsync(id, page ?? 1, pageSize ?? 10, http.User)));
        writes.MapPut("/reports/{id:int}/balance", async (int id, TreasuryBalanceInput input, HttpContext http, ITreasuryService s) =>
            ToResult(await s.SetBalanceAsync(id, input, http.User))).WithMetadata(small);

        // Activities
        writes.MapPost("/reports/{id:int}/activities", async (int id, TreasuryActivityInput input, HttpContext http, ITreasuryService s) =>
            ToResult(await s.CreateActivityAsync(id, input, http.User))).WithMetadata(small);
        writes.MapPut("/activities/{id:int}", async (int id, TreasuryActivityInput input, HttpContext http, ITreasuryService s) =>
            ToResult(await s.UpdateActivityAsync(id, input, http.User))).WithMetadata(small);
        writes.MapPost("/activities/{id:int}/lock", async (int id, TreasuryLockInput input, HttpContext http, ITreasuryService s) =>
            ToResult(await s.SetActivityLockAsync(id, input, http.User))).WithMetadata(small);
        writes.MapDelete("/activities/{id:int}", async (int id, HttpContext http, ITreasuryService s) =>
            ToResult(await s.DeleteActivityAsync(id, http.User)));

        // Transactions: multipart fields date (yyyy-MM-dd), description, category, amount (12.34), type, removeReceipt; optional file "receipt".
        writes.MapPost("/activities/{id:int}/transactions", async (int id, IFormCollection form, HttpContext http, ITreasuryService s) =>
            {
                await using var receipt = form.Files.GetFile("receipt")?.OpenReadStream();
                return ToResult(await s.CreateTransactionAsync(id, Transaction(form, receipt), http.User));
            })
            .WithMetadata(withReceipt);
        writes.MapPut("/transactions/{id:int}", async (int id, IFormCollection form, HttpContext http, ITreasuryService s) =>
            {
                await using var receipt = form.Files.GetFile("receipt")?.OpenReadStream();
                return ToResult(await s.UpdateTransactionAsync(id, Transaction(form, receipt), http.User));
            })
            .WithMetadata(withReceipt);
        writes.MapDelete("/transactions/{id:int}", async (int id, HttpContext http, ITreasuryService s) =>
            ToResult(await s.DeleteTransactionAsync(id, http.User)));

        // Calotes
        api.MapGet("/calotes", async (string? fy, HttpContext http, ITreasuryRecordsService s) => ToResult(await s.GetCalotesAsync(fy, http.User)));
        writes.MapPost("/calotes", async (TreasuryDebtInput input, HttpContext http, ITreasuryRecordsService s) =>
            ToResult(await s.AddDebtAsync(input, http.User))).WithMetadata(small);
        writes.MapPut("/calotes/{id:int}", async (int id, TreasuryDebtInput input, HttpContext http, ITreasuryRecordsService s) =>
            ToResult(await s.UpdateDebtAsync(id, input, http.User))).WithMetadata(small);
        writes.MapDelete("/calotes/{id:int}", async (int id, HttpContext http, ITreasuryRecordsService s) =>
            ToResult(await s.DeleteDebtAsync(id, http.User)));
        writes.MapPut("/calotes/commitment", async (TreasuryCommitmentInput input, HttpContext http, ITreasuryRecordsService s) =>
            ToResult(await s.SetCommitmentAsync(input, http.User))).WithMetadata(small);
        api.MapGet("/members", async (string? q, bool? forDebts, HttpContext http, ITreasuryRecordsService s) =>
            ToResult(await s.SearchMembersAsync(q, forDebts == true, http.User)));

        // MBWay
        api.MapGet("/mbway", async (HttpContext http, ITreasuryRecordsService s) => ToResult(await s.GetTransfersAsync(http.User)));
        writes.MapPost("/mbway", async (TreasuryTransferInput input, HttpContext http, ITreasuryRecordsService s) =>
            ToResult(await s.AddTransferAsync(input, http.User))).WithMetadata(small);
        writes.MapPut("/mbway/{id:int}", async (int id, TreasuryTransferInput input, HttpContext http, ITreasuryRecordsService s) =>
            ToResult(await s.UpdateTransferAsync(id, input, http.User))).WithMetadata(small);
        writes.MapDelete("/mbway/{id:int}", async (int id, HttpContext http, ITreasuryRecordsService s) =>
            ToResult(await s.DeleteTransferAsync(id, http.User)));

        // Nerba
        api.MapGet("/nerba", async (HttpContext http, ITreasuryRecordsService s) => ToResult(await s.GetNerbaAsync(http.User)));
        writes.MapDelete("/nerba/{eventId:int}", async (int eventId, HttpContext http, ITreasuryRecordsService s) =>
            ToResult(await s.DeleteNerbaOrdersAsync(eventId, http.User)));
        api.MapGet("/nerba/{eventId:int}", async (int eventId, HttpContext http, ITreasuryRecordsService s) =>
            ToResult(await s.GetNerbaOrdersAsync(eventId, http.User)));
        writes.MapPost("/nerba/{eventId:int}/orders", async (int eventId, TreasuryNerbaOrderInput input, HttpContext http, ITreasuryRecordsService s) =>
            ToResult(await s.AddNerbaOrderAsync(eventId, input, http.User))).WithMetadata(small);
        writes.MapPut("/nerba/orders/{id:int}", async (int id, TreasuryNerbaOrderInput input, HttpContext http, ITreasuryRecordsService s) =>
            ToResult(await s.UpdateNerbaOrderAsync(id, input, http.User))).WithMetadata(small);
        writes.MapDelete("/nerba/orders/{id:int}", async (int id, HttpContext http, ITreasuryRecordsService s) =>
            ToResult(await s.DeleteNerbaOrderAsync(id, http.User)));
    }

    private static TreasuryTransactionInput Transaction(IFormCollection form, Stream? receipt)
    {
        var file = form.Files.GetFile("receipt");
        return new TreasuryTransactionInput(
            DateTime.TryParseExact((string?)form["date"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null,
            (string?)form["description"],
            (string?)form["category"],
            decimal.TryParse((string?)form["amount"], NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ? amount : null,
            (string?)form["type"],
            string.Equals((string?)form["removeReceipt"], "true", StringComparison.OrdinalIgnoreCase),
            file is null || receipt is null ? null : new TreasuryReceiptUpload(receipt, file.FileName, file.ContentType ?? string.Empty, file.Length));
    }

    private static IResult ToResult<T>(EventResult<T> result, Func<T, IResult>? ok = null) => result.Status switch
    {
        EventResultStatus.Ok => ok is null ? Results.Ok(result.Value) : ok(result.Value!),
        EventResultStatus.Invalid => Results.ValidationProblem(
            (result.Errors ?? new Dictionary<string, string[]>()).ToDictionary(e => e.Key, e => e.Value), title: "Há campos por corrigir."),
        EventResultStatus.SignInRequired => Results.Problem(title: "Reservado a membros da RTUB. Entre para continuar.",
            statusCode: StatusCodes.Status401Unauthorized),
        EventResultStatus.Forbidden => Results.Problem(title: "Não tem permissão para esta ação.", statusCode: StatusCodes.Status403Forbidden),
        EventResultStatus.Closed => Results.Problem(title: "O relatório já foi publicado ou a atividade está bloqueada.",
            statusCode: StatusCodes.Status409Conflict),
        _ => Results.Problem(title: "Não encontrado.", statusCode: StatusCodes.Status404NotFound),
    };
}
