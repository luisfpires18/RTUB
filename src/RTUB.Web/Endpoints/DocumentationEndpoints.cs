using Microsoft.AspNetCore.Mvc;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;

namespace RTUB.Web.Endpoints;

/// <summary>
/// The React /documentation ("Documentação", docs/react-documentation.md). Thin:
/// every rule lives in <see cref="IDocumentationService"/>, decided from the session. Signed-in members only, Leitões
/// refused. A document is addressed by fiscalYear, folder and name; every write needs the antiforgery token
/// in the X-CSRF-TOKEN header.
/// </summary>
public static class DocumentationEndpoints
{
    public static void MapDocumentationEndpoints(this IEndpointRouteBuilder app)
    {
        var docs = app.MapGroup("/api/documentation").AllowAnonymous().AddEndpointFilter(EventEndpoints.NoStore);
        var writes = docs.MapGroup(string.Empty).AddEndpointFilter(EventEndpoints.RequireAntiforgery);

        // ?fiscalYear= (absent or empty: the current year)
        docs.MapGet("/", async (string? fiscalYear, HttpContext http, IDocumentationService service) =>
            ToResult(await service.GetAsync(fiscalYear, http.User)));

        // A short-lived pre-signed download URL; the page opens it in the same tab.
        docs.MapGet("/file", async (string? fiscalYear, string? folder, string? name, HttpContext http, IDocumentationService service) =>
            ToResult(await service.GetDownloadAsync(fiscalYear, folder, name, http.User)));

        writes.MapPost("/folders", async (DocumentFolderInput input, HttpContext http, IDocumentationService service) =>
                ToResult(await service.CreateFolderAsync(input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(4 * 1024));

        writes.MapDelete("/folders", async (string? fiscalYear, string? folder, HttpContext http, IDocumentationService service) =>
            ToResult(await service.DeleteFolderAsync(fiscalYear, folder, http.User), _ => Results.NoContent()));

        // Multipart: "fiscalYear", "folder" and "file".
        writes.MapPost("/documents", async (IFormCollection form, HttpContext http, IDocumentationService service) =>
            {
                if (form.Files.GetFile("file") is not { } file)
                {
                    return EndpointResults.Invalid("file", "Escolha um ficheiro.");
                }

                await using var content = file.OpenReadStream();
                return ToResult(await service.UploadAsync(form["fiscalYear"].ToString(), form["folder"].ToString(),
                    new DocumentUpload(content, file.FileName, file.ContentType ?? string.Empty, file.Length), http.User));
            })
            .WithMetadata(new RequestSizeLimitAttribute(DocumentationService.MaxFileBytes + 64 * 1024));

        writes.MapDelete("/documents", async (string? fiscalYear, string? folder, string? name, HttpContext http, IDocumentationService service) =>
            ToResult(await service.DeleteDocumentAsync(fiscalYear, folder, name, http.User), _ => Results.NoContent()));
    }

    private static IResult ToResult<T>(EventResult<T> result, Func<T, IResult>? ok = null) =>
        EndpointResults.ToResult(result, ok, "Documento ou pasta não encontrado.");
}
