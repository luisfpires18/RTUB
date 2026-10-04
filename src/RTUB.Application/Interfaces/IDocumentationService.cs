using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>The React /documentation ("Documentação", React track 022; was the Blazor Documentation.razor).</summary>
public interface IDocumentationService
{
    Task<EventResult<DocumentationDto>> GetAsync(string? fiscalYear, ClaimsPrincipal user);

    Task<EventResult<DocumentLinkDto>> GetDownloadAsync(string? fiscalYear, string? folder, string? name, ClaimsPrincipal user);

    Task<EventResult<DocumentFolderDto>> CreateFolderAsync(DocumentFolderInput input, ClaimsPrincipal user);

    Task<EventResult<bool>> DeleteFolderAsync(string? fiscalYear, string? folder, ClaimsPrincipal user);

    Task<EventResult<DocumentFileDto>> UploadAsync(string? fiscalYear, string? folder, DocumentUpload upload, ClaimsPrincipal user);

    Task<EventResult<bool>> DeleteDocumentAsync(string? fiscalYear, string? folder, string? name, ClaimsPrincipal user);
}
