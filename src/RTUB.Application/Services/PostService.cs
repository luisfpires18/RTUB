using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Post service implementation
/// Contains business logic for post operations
/// </summary>
public class PostService : IPostService
{
    private readonly ApplicationDbContext _context;

    public PostService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Post?> GetPostByIdAsync(int id)
    {
        return await _context.Posts
            .Include(p => p.User)
            .Include(p => p.Comments)
            .ThenInclude(c => c.User)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<IEnumerable<Post>> GetPostsByDiscussionIdAsync(int discussionId)
    {
        return await _context.Posts
            .Include(p => p.User)
            .Include(p => p.Comments)
            .Where(p => p.DiscussionId == discussionId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Post>> GetPostsByUserIdAsync(string userId)
    {
        return await _context.Posts
            .Include(p => p.User)
            .Include(p => p.Discussion)
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<Post> CreatePostAsync(int discussionId, string userId, string content)
    {
        var post = Post.Create(discussionId, userId, content);
        _context.Posts.Add(post);
        await _context.SaveChangesAsync();
        return post;
    }

    public async Task UpdatePostAsync(int id, string content)
    {
        var post = await _context.Posts.FindAsync(id);
        if (post == null)
            throw new InvalidOperationException($"Post with ID {id} not found");

        post.UpdateContent(content);
        await _context.SaveChangesAsync();
    }

    public async Task DeletePostAsync(int id)
    {
        var post = await _context.Posts.FindAsync(id);
        if (post == null)
            throw new InvalidOperationException($"Post with ID {id} not found");

        _context.Posts.Remove(post);
        await _context.SaveChangesAsync();
    }
}
