namespace RTUB.Application.DTOs;

// Contracts of the public "Novidades" feed (/news, React track 025, docs/react-news.md). The public poster is always
// "RTUB": AuthorName is filled only for Admin/Owner, and no account id or username ever leaves the server.

/// <summary>One post. <c>PublishedAt</c> (UTC) is null for a draft; <c>AuthorName</c> is null unless the caller manages.</summary>
public sealed record NewsPostDto(int Id, string? Title, string Body, DateTime? PublishedAt, string? AuthorName);

/// <summary>
/// A page of published posts, newest first. <c>Drafts</c> (every draft, newest first) is filled only for Admin/Owner and
/// only on the first page; it is empty for everyone else.
/// </summary>
public sealed record NewsFeedDto(IReadOnlyList<NewsPostDto> Drafts, IReadOnlyList<NewsPostDto> Posts, bool HasMore, bool CanManage);

/// <summary>Create or edit. <c>Publish</c> is read on create only ("Publicar" vs "Guardar rascunho").</summary>
public sealed record NewsPostInput(string? Title, string? Body, bool Publish = false);
