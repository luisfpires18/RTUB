using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// The public "Novidades" feed behind /news (React track 025, docs/react-news.md): text posts written by Admin/Owner.
/// - read: anyone, visitors included, sees published posts, newest <see cref="NewsPost.PublishedAt"/> first; drafts
///   (<c>PublishedAt</c> null) only reach Admin/Owner;
/// - write: Admin/Owner (<see cref="NewsAuthorization"/>) create (as a draft or published), edit title and text,
///   publish (now), unpublish (back to draft) and delete (permanent);
/// - privacy: the public poster is "RTUB"; the author's display name is returned to Admin/Owner only, and no account id
///   or username is ever returned.
/// Nothing is sent (no push, no email).
/// </summary>
public sealed class NewsService : INewsService
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 20;

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;

    public NewsService(IDbContextFactory<ApplicationDbContext> contexts)
    {
        _contexts = contexts;
    }

    public async Task<EventResult<NewsFeedDto>> GetFeedAsync(int page, int pageSize, ClaimsPrincipal user)
    {
        if (page < 1)
        {
            return EventResult<NewsFeedDto>.Invalid("page", "Página inválida.");
        }

        var size = Math.Clamp(pageSize, 1, MaxPageSize);
        var manage = NewsAuthorization.CanManage(user);

        await using var db = await _contexts.CreateDbContextAsync();
        var posts = await Posts(db, manage)
            .Where(p => p.PublishedAt != null)
            .OrderByDescending(p => p.PublishedAt).ThenByDescending(p => p.Id)
            .Skip((page - 1) * size).Take(size + 1)
            .ToListAsync();

        var drafts = manage && page == 1
            ? await Posts(db, manage).Where(p => p.PublishedAt == null).OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id).ToListAsync()
            : new List<NewsPost>();

        return EventResult<NewsFeedDto>.Ok(new NewsFeedDto(
            drafts.Select(p => Dto(p, manage)).ToList(),
            posts.Take(size).Select(p => Dto(p, manage)).ToList(),
            posts.Count > size,
            manage));
    }

    public async Task<EventResult<NewsPostDto>> CreateAsync(NewsPostInput input, ClaimsPrincipal user)
    {
        if (Refusal<NewsPostDto>(user) is { } refused)
        {
            return refused;
        }

        if (Validate(input, out var title, out var body) is { } invalid)
        {
            return invalid;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        var post = new NewsPost
        {
            Title = title,
            Body = body,
            AuthorId = NewsAuthorization.UserId(user),
            PublishedAt = input.Publish ? DateTime.UtcNow : null,
        };
        db.NewsPosts.Add(post);
        await db.SaveChangesAsync();
        return await ResultAsync(post.Id);
    }

    public async Task<EventResult<NewsPostDto>> UpdateAsync(int id, NewsPostInput input, ClaimsPrincipal user)
    {
        if (Refusal<NewsPostDto>(user) is { } refused)
        {
            return refused;
        }

        if (Validate(input, out var title, out var body) is { } invalid)
        {
            return invalid;
        }

        return await ChangeAsync(id, p =>
        {
            p.Title = title;
            p.Body = body;
        });
    }

    public async Task<EventResult<NewsPostDto>> PublishAsync(int id, ClaimsPrincipal user) =>
        Refusal<NewsPostDto>(user) ?? await ChangeAsync(id, p => p.PublishedAt ??= DateTime.UtcNow);

    public async Task<EventResult<NewsPostDto>> UnpublishAsync(int id, ClaimsPrincipal user) =>
        Refusal<NewsPostDto>(user) ?? await ChangeAsync(id, p => p.PublishedAt = null);

    public async Task<EventResult<bool>> DeleteAsync(int id, ClaimsPrincipal user)
    {
        if (Refusal<bool>(user) is { } refused)
        {
            return refused;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await db.NewsPosts.FirstOrDefaultAsync(p => p.Id == id) is not { } post)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        db.NewsPosts.Remove(post);
        await db.SaveChangesAsync();
        return EventResult<bool>.Ok(true);
    }

    // ---------------------------------------------------------------- helpers

    private static IQueryable<NewsPost> Posts(ApplicationDbContext db, bool withAuthor) =>
        withAuthor ? db.NewsPosts.AsNoTracking().Include(p => p.Author) : db.NewsPosts.AsNoTracking();

    private static NewsPostDto Dto(NewsPost p, bool manage) => new(
        p.Id,
        p.Title,
        p.Body,
        p.PublishedAt is { } at ? DateTime.SpecifyKind(at, DateTimeKind.Utc) : null,
        manage ? p.Author?.GetDisplayName() : null);

    private async Task<EventResult<NewsPostDto>> ChangeAsync(int id, Action<NewsPost> change)
    {
        await using (var db = await _contexts.CreateDbContextAsync())
        {
            if (await db.NewsPosts.FirstOrDefaultAsync(p => p.Id == id) is not { } post)
            {
                return EventResult<NewsPostDto>.Fail(EventResultStatus.NotFound);
            }

            change(post);
            await db.SaveChangesAsync();
        }

        return await ResultAsync(id);
    }

    /// <summary>The post as Admin/Owner see it (the only callers of the writes).</summary>
    private async Task<EventResult<NewsPostDto>> ResultAsync(int id)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        return await Posts(db, withAuthor: true).FirstOrDefaultAsync(p => p.Id == id) is { } post
            ? EventResult<NewsPostDto>.Ok(Dto(post, manage: true))
            : EventResult<NewsPostDto>.Fail(EventResultStatus.NotFound);
    }

    /// <summary>401 for visitors, 403 for anyone who is not Admin/Owner.</summary>
    private static EventResult<T>? Refusal<T>(ClaimsPrincipal user) =>
        NewsAuthorization.UserId(user) is null ? EventResult<T>.Fail(EventResultStatus.SignInRequired)
        : NewsAuthorization.CanManage(user) ? null
        : EventResult<T>.Fail(EventResultStatus.Forbidden);

    /// <summary>Title optional (≤ 150, blank = none), text required (≤ 5000); both trimmed.</summary>
    private static EventResult<NewsPostDto>? Validate(NewsPostInput input, out string? title, out string body)
    {
        title = string.IsNullOrWhiteSpace(input.Title) ? null : input.Title.Trim();
        body = input.Body?.Trim() ?? string.Empty;

        var errors = new Dictionary<string, string[]>();
        if (title?.Length > NewsPost.TitleMaxLength)
        {
            errors["title"] = new[] { $"O título não pode exceder {NewsPost.TitleMaxLength} caracteres." };
        }

        if (body.Length == 0)
        {
            errors["body"] = new[] { "Escreve o texto da publicação." };
        }
        else if (body.Length > NewsPost.BodyMaxLength)
        {
            errors["body"] = new[] { $"O texto não pode exceder {NewsPost.BodyMaxLength} caracteres." };
        }

        return errors.Count > 0 ? new EventResult<NewsPostDto>(EventResultStatus.Invalid, Errors: errors) : null;
    }
}
