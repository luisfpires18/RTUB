namespace RTUB.Application.DTOs;

// Contracts of the public "Novidades" feed (/news, React track 025, docs/react-news.md). The public poster is always
// "RTUB": AuthorName is filled only for Admin/Owner, and no account id or username ever leaves the server.

/// <summary>
/// One post. <c>PublishedAt</c> (UTC) is null for a draft; <c>AuthorName</c> is null unless the caller manages.
/// <c>MembersOnly</c> (#222) is only ever true for a signed-in caller: a visitor never gets such a post.
/// </summary>
public sealed record NewsPostDto(int Id, string? Title, string Body, DateTime? PublishedAt, string? AuthorName, bool MembersOnly = false);

/// <summary>
/// A page of published posts, newest first. <c>Drafts</c> (every draft, newest first) is filled only for Admin/Owner and
/// only on the first page; it is empty for everyone else. <c>SignedIn</c> (#222): the caller has a session and so sees
/// members-only posts too (the Todas / Públicas / Privadas tabs); it says nothing about whether any exist.
/// </summary>
public sealed record NewsFeedDto(IReadOnlyList<NewsPostDto> Drafts, IReadOnlyList<NewsPostDto> Posts, bool HasMore, bool CanManage, bool SignedIn = false);

/// <summary>
/// Create or edit. <c>Publish</c> is read on create only ("Publicar" vs "Guardar rascunho"). <c>MembersOnly</c> (#222):
/// on create, true = members only, false or absent = public; on update, true / false set it and absent keeps what the
/// post has, so a client that does not know the field can never make a private post public.
/// </summary>
public sealed record NewsPostInput(string? Title, string? Body, bool Publish = false, bool? MembersOnly = null);
