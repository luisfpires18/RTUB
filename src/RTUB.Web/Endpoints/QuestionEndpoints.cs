using Microsoft.AspNetCore.Mvc;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;

namespace RTUB.Web.Endpoints;

/// <summary>
/// The React /questions ("Perguntas aos Órgãos Sociais", task 031, docs/react-requests-questions.md; was the Blazor
/// page). Thin: every rule lives in <see cref="IQuestionBoardService"/>, decided from the session. Signed-in members
/// only, as before. Every write needs the antiforgery token in the X-CSRF-TOKEN header.
/// </summary>
public static class QuestionEndpoints
{
    public static void MapQuestionEndpoints(this IEndpointRouteBuilder app)
    {
        var questions = app.MapGroup("/api/questions").AllowAnonymous().AddEndpointFilter(EventEndpoints.NoStore);
        var writes = questions.MapGroup(string.Empty).AddEndpointFilter(EventEndpoints.RequireAntiforgery);

        // ?closed=false&q=&recipient=<user id>&page=1&pageSize=10
        questions.MapGet("/", async (bool? closed, string? q, string? recipient, int? page, int? pageSize, HttpContext http, IQuestionBoardService service) =>
            ToResult(await service.GetPageAsync(closed ?? false, q, recipient, page ?? 1, pageSize ?? 0, http.User)));

        questions.MapGet("/recipients", async (HttpContext http, IQuestionBoardService service) =>
            ToResult(await service.GetRecipientsAsync(http.User)));

        questions.MapGet("/{id:int}", async (int id, HttpContext http, IQuestionBoardService service) =>
            ToResult(await service.GetAsync(id, http.User)));

        writes.MapPost("/", async (QuestionInput input, HttpContext http, IQuestionBoardService service) =>
                ToResult(await service.AskAsync(input, http.User), created => Results.Created($"/api/questions/{created.Question.Id}", created)))
            .WithMetadata(new RequestSizeLimitAttribute(32 * 1024));

        writes.MapPost("/{id:int}/replies", async (int id, QuestionReplyInput input, HttpContext http, IQuestionBoardService service) =>
                ToResult(await service.ReplyAsync(id, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(32 * 1024));

        writes.MapPost("/{id:int}/close", async (int id, HttpContext http, IQuestionBoardService service) =>
            ToResult(await service.CloseAsync(id, http.User)));

        writes.MapPost("/{id:int}/remind", async (int id, HttpContext http, IQuestionBoardService service) =>
            ToResult(await service.RemindAsync(id, http.User), _ => Results.NoContent()));

        writes.MapDelete("/{id:int}", async (int id, HttpContext http, IQuestionBoardService service) =>
            ToResult(await service.DeleteAsync(id, http.User), _ => Results.NoContent()));
    }

    private static IResult ToResult<T>(EventResult<T> result, Func<T, IResult>? ok = null) => result.Status switch
    {
        EventResultStatus.Ok => ok is null ? Results.Ok(result.Value) : ok(result.Value!),
        EventResultStatus.Invalid => Results.ValidationProblem(
            (result.Errors ?? new Dictionary<string, string[]>()).ToDictionary(e => e.Key, e => e.Value), title: "Há campos por corrigir."),
        EventResultStatus.SignInRequired => Results.Problem(title: "Reservado a membros da RTUB. Entre para continuar.",
            statusCode: StatusCodes.Status401Unauthorized),
        EventResultStatus.Forbidden => Results.Problem(title: "Não tem permissão para esta ação.", statusCode: StatusCodes.Status403Forbidden),
        _ => Results.Problem(title: "Não encontrado.", statusCode: StatusCodes.Status404NotFound),
    };
}
