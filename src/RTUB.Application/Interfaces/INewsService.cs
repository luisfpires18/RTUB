using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>The public "Novidades" feed (/news, React track 025). Every rule is decided here, from the session.</summary>
public interface INewsService
{
    /// <summary>Published posts, newest first, <paramref name="pageSize"/> (1-20) per page; drafts too for Admin/Owner.</summary>
    Task<EventResult<NewsFeedDto>> GetFeedAsync(int page, int pageSize, ClaimsPrincipal user);
    Task<EventResult<NewsPostDto>> CreateAsync(NewsPostInput input, ClaimsPrincipal user);
    Task<EventResult<NewsPostDto>> UpdateAsync(int id, NewsPostInput input, ClaimsPrincipal user);
    /// <summary>Sets <c>PublishedAt</c> to now on a draft (a re-published post goes back to the top); a published post is left as is.</summary>
    Task<EventResult<NewsPostDto>> PublishAsync(int id, ClaimsPrincipal user);
    /// <summary>Clears <c>PublishedAt</c>: the post is a draft again.</summary>
    Task<EventResult<NewsPostDto>> UnpublishAsync(int id, ClaimsPrincipal user);
    /// <summary>Permanent.</summary>
    Task<EventResult<bool>> DeleteAsync(int id, ClaimsPrincipal user);
}
