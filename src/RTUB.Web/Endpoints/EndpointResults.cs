using RTUB.Application.DTOs;

namespace RTUB.Web.Endpoints;

/// <summary>How the /api areas answer a service outcome (<see cref="EventResult{T}"/>) over HTTP.</summary>
internal static class EndpointResults
{
    /// <summary>
    /// Ok is 200 with the value (or <paramref name="ok"/>), Invalid is 400 with the field errors, SignInRequired is 401,
    /// Forbidden is 403 and anything else is 404 titled <paramref name="notFound"/>. An area with answers of its own
    /// (a 409, another 403 text) returns them from <paramref name="special"/>, which is asked first; null means the usual answer.
    /// </summary>
    public static IResult ToResult<T>(EventResult<T> result, Func<T, IResult>? ok = null, string notFound = "Não encontrado.",
        Func<EventResultStatus, IResult?>? special = null) => special?.Invoke(result.Status) ?? result.Status switch
    {
        EventResultStatus.Ok => ok is null ? Results.Ok(result.Value) : ok(result.Value!),
        EventResultStatus.Invalid => Results.ValidationProblem(
            (result.Errors ?? new Dictionary<string, string[]>()).ToDictionary(e => e.Key, e => e.Value), title: "Há campos por corrigir."),
        EventResultStatus.SignInRequired => Results.Problem(title: "Reservado a membros da RTUB. Entre para continuar.",
            statusCode: StatusCodes.Status401Unauthorized),
        EventResultStatus.Forbidden => Results.Problem(title: "Não tem permissão para esta ação.", statusCode: StatusCodes.Status403Forbidden),
        _ => Results.Problem(title: notFound, statusCode: StatusCodes.Status404NotFound),
    };

    /// <summary>The 401 problem body, same as <see cref="EventEndpoints.RequireMember"/> and the SignInRequired answer.</summary>
    public static IResult SignInRequired() => Results.Problem(title: "Reservado a membros da RTUB. Entre para continuar.",
        statusCode: StatusCodes.Status401Unauthorized);

    /// <summary>A 400 for one field, in the shape of a service's Invalid answer.</summary>
    public static IResult Invalid(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = new[] { message } }, title: "Há campos por corrigir.");
}
