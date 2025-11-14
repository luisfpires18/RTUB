using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

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
            .Include(p => p.Author)
            .Include(p => p.Comments)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<IEnumerable<Post>> GetByDiscussionIdAsync(int discussionId, int page = 1, int pageSize = 20, string? searchTerm = null)
    {
        var query = _context.Posts
            .Include(p => p.Author)
            .Include(p => p.Comments)
            .Where(p => p.DiscussionId == discussionId && !p.IsDeleted);

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var lowerSearch = searchTerm.ToLower();
            query = query.Where(p =>
                p.Title.ToLower().Contains(lowerSearch) ||
                p.Body.ToLower().Contains(lowerSearch) ||
                p.Author.UserName!.ToLower().Contains(lowerSearch) ||
                (p.Author.Nickname != null && p.Author.Nickname.ToLower().Contains(lowerSearch))
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
            .Where(p => p.DiscussionId == discussionId && !p.IsDeleted);

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var lowerSearch = searchTerm.ToLower();
            query = query.Where(p =>
                p.Title.ToLower().Contains(lowerSearch) ||
                p.Body.ToLower().Contains(lowerSearch) ||
                p.Author.UserName!.ToLower().Contains(lowerSearch) ||
                (p.Author.Nickname != null && p.Author.Nickname.ToLower().Contains(lowerSearch))
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
            throw new InvalidOperationException($"Post with ID {id} not found");

        post.Edit(title, body);
        if (!string.IsNullOrWhiteSpace(mentionsJson))
        {
            post.SetMentions(mentionsJson);
        }

        _context.Posts.Update(post);
        await _context.SaveChangesAsync();
    }

    public async Task PinAsync(int id)
    {
        var post = await _context.Posts.FindAsync(id);
        if (post == null)
            throw new InvalidOperationException($"Post with ID {id} not found");

        post.Pin();
        _context.Posts.Update(post);
        await _context.SaveChangesAsync();
    }

    public async Task UnpinAsync(int id)
    {
        var post = await _context.Posts.FindAsync(id);
        if (post == null)
            throw new InvalidOperationException($"Post with ID {id} not found");

        post.Unpin();
        _context.Posts.Update(post);
        await _context.SaveChangesAsync();
    }

    public async Task LockAsync(int id)
    {
        var post = await _context.Posts.FindAsync(id);
        if (post == null)
            throw new InvalidOperationException($"Post with ID {id} not found");

        post.Lock();
        _context.Posts.Update(post);
        await _context.SaveChangesAsync();
    }

    public async Task UnlockAsync(int id)
    {
        var post = await _context.Posts.FindAsync(id);
        if (post == null)
            throw new InvalidOperationException($"Post with ID {id} not found");

        post.Unlock();
        _context.Posts.Update(post);
        await _context.SaveChangesAsync();
    }

    public async Task SoftDeleteAsync(int id)
    {
        var post = await _context.Posts.FindAsync(id);
        if (post == null)
            throw new InvalidOperationException($"Post with ID {id} not found");

        post.SoftDelete();
        _context.Posts.Update(post);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateLastActivityAsync(int id)
    {
        var post = await _context.Posts.FindAsync(id);
        if (post == null)
            throw new InvalidOperationException($"Post with ID {id} not found");

        post.UpdateLastActivity();
        _context.Posts.Update(post);
        await _context.SaveChangesAsync();
    }
}
