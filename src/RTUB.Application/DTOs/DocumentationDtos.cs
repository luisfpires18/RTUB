namespace RTUB.Application.DTOs;

// Contracts of the React /documentation ("Documentação", React track 022; was the Blazor Documentation.razor). Documents
// live only in storage (docs/{environment}/{fiscal year}/{folder}/{file}); no table. Storage keys never leave the server:
// a document is named by its fiscal year, folder and file name, and the server checks all three against the listing.

/// <summary>
/// <c>FiscalYear</c>: the year shown (the current one by default, as the old page). <c>Folders</c>: only those the caller
/// may see, in storage order, Logistics boards in place of the Logistics folder. <c>CanManage</c>: Owner.
/// </summary>
public sealed record DocumentationDto(
    IReadOnlyList<MemberOptionDto> FiscalYears,
    string FiscalYear,
    IReadOnlyList<DocumentFolderDto> Folders,
    IReadOnlyList<string> Extensions,
    long MaxFileBytes,
    bool CanManage);

/// <summary><c>Name</c> addresses the folder ("Logistics/Board" for a board); <c>Label</c> is what the old page showed.</summary>
public sealed record DocumentFolderDto(string Name, string Label, IReadOnlyList<DocumentFileDto> Documents);

/// <summary>A document by file name, as the old card: name, type and size.</summary>
public sealed record DocumentFileDto(string Name, string Extension, long SizeBytes);

/// <summary>A pre-signed download URL (attachment), valid for a limited time, as the old page opened.</summary>
public sealed record DocumentLinkDto(string Url);

public sealed record DocumentFolderInput(string? Name);

public sealed record DocumentUpload(Stream Content, string FileName, string ContentType, long Length);
