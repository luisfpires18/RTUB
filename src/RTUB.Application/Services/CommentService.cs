using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Comment service implementation
/// </summary>
public class CommentService : ICommentService
{
    private readonly ApplicationDbContext _context;
    private readonly IPostService _postService;

    public CommentService(ApplicationDbContext context, IPostService postService)
    {
        _context = context;
        _postService = postService;
    }

    public async Task<Comment?> GetByIdAsync(int id)
    {
        return await _context.Comments
            .Include(c => c.Author)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<IEnumerable<Comment>> GetByPostIdAsync(int postId, int page = 1, int pageSize = 50)
    {
        return await _context.Comments
            .Include(c => c.Author)
            .Where(c => c.PostId == postId && !c.IsDeleted)
            .OrderBy(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetCountByPostIdAsync(int postId)
    {
        return await _context.Comments
            .Where(c => c.PostId == postId && !c.IsDeleted)
            .CountAsync();
    }

    public async Task<Comment> CreateAsync(int postId, string authorId, string body, string? mentionsJson = null)
    {
        var comment = Comment.Create(postId, authorId, body);
        if (!string.IsNullOrWhiteSpace(mentionsJson))
        {
            comment.SetMentions(mentionsJson);
        }

        _context.Comments.Add(comment);
        await _context.SaveChangesAsync();

        // Update post's last activity timestamp
        await _postService.UpdateLastActivityAsync(postId);

        return comment;
    }

    public async Task UpdateAsync(int id, string body, string? mentionsJson = null)
    {
        var comment = await _context.Comments.FindAsync(id);
        if (comment == null)
            throw new InvalidOperationException($"Comment with ID {id} not found");

        comment.Edit(body);
        if (!string.IsNullOrWhiteSpace(mentionsJson))
        {
            comment.SetMentions(mentionsJson);
        }

        _context.Comments.Update(comment);
        await _context.SaveChangesAsync();
    }

    public async Task SoftDeleteAsync(int id)
    {
        var comment = await _context.Comments.FindAsync(id);
        if (comment == null)
            throw new InvalidOperationException($"Comment with ID {id} not found");

        comment.SoftDelete();
        _context.Comments.Update(comment);
        await _context.SaveChangesAsync();
    }
}
