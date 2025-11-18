using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services;

/// <summary>
/// Post service implementation
/// </summary>
public class PostService : IPostService
{
    private readonly ApplicationDbContext _context;

    public PostService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Post?> GetByIdAsync(int id)
    {
        return await _context.Posts
            .AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.Comments)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<IEnumerable<Post>> GetByDiscussionIdAsync(int discussionId, int page = 1, int pageSize = 20, string? searchTerm = null)
    {
        var query = _context.Posts
            .AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.Comments)
            .Where(p => p.DiscussionId == discussionId && !p.IsDeleted);

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(p =>
                p.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                p.Body.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                p.Author.UserName!.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                (p.Author.Nickname != null && p.Author.Nickname.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            );
        }

        // Sort: pinned first, then by last activity
        query = query.OrderByDescending(p => p.IsPinned)
                     .ThenByDescending(p => p.LastActivityAt);

        // Apply pagination
        query = query.Skip((page - 1) * pageSize).Take(pageSize);

        return await query.ToListAsync();
    }

    public async Task<int> GetCountByDiscussionIdAsync(int discussionId, string? searchTerm = null)
    {
        var query = _context.Posts
            .AsNoTracking()
            .Where(p => p.DiscussionId == discussionId && !p.IsDeleted);

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(p =>
                p.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                p.Body.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                p.Author.UserName!.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                (p.Author.Nickname != null && p.Author.Nickname.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            );
        }

        return await query.CountAsync();
    }

    public async Task<Post> CreateAsync(int discussionId, string authorId, string title, string body, string? mentionsJson = null)
    {
        var post = Post.Create(discussionId, authorId, title, body);
        if (!string.IsNullOrWhiteSpace(mentionsJson))
        {
            post.SetMentions(mentionsJson);
        }

        _context.Posts.Add(post);
        await _context.SaveChangesAsync();
        return post;
    }

    public async Task UpdateAsync(int id, string title, string body, string? mentionsJson = null)
    {
        var post = await _context.Posts.FindAsync(id);
        if (post == null)
            throw new EntityNotFoundException(nameof(Post), id);

        post.Edit(title, body);
        if (!string.IsNullOrWhiteSpace(mentionsJson))
        {
            post.SetMentions(mentionsJson);
        }

        await _context.SaveChangesAsync();
    }

    public async Task PinAsync(int id)
    {
        var post = await _context.Posts.FindAsync(id);
        if (post == null)
            throw new EntityNotFoundException(nameof(Post), id);

        post.Pin();
        await _context.SaveChangesAsync();
    }

    public async Task UnpinAsync(int id)
    {
        var post = await _context.Posts.FindAsync(id);
        if (post == null)
            throw new EntityNotFoundException(nameof(Post), id);

        post.Unpin();
        await _context.SaveChangesAsync();
    }

    public async Task LockAsync(int id)
    {
        var post = await _context.Posts.FindAsync(id);
        if (post == null)
            throw new EntityNotFoundException(nameof(Post), id);

        post.Lock();
        await _context.SaveChangesAsync();
    }

    public async Task UnlockAsync(int id)
    {
        var post = await _context.Posts.FindAsync(id);
        if (post == null)
            throw new EntityNotFoundException(nameof(Post), id);

        post.Unlock();
        await _context.SaveChangesAsync();
    }

    public async Task SoftDeleteAsync(int id)
    {
        var post = await _context.Posts.FindAsync(id);
        if (post == null)
            throw new EntityNotFoundException(nameof(Post), id);

        post.SoftDelete();
        await _context.SaveChangesAsync();
    }

    public async Task UpdateLastActivityAsync(int id)
    {
        var post = await _context.Posts.FindAsync(id);
        if (post == null)
            throw new EntityNotFoundException(nameof(Post), id);

        post.UpdateLastActivity();
        await _context.SaveChangesAsync();
    }
}
