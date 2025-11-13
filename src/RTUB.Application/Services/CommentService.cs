using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Comment service implementation
/// Contains business logic for comment operations
/// </summary>
public class CommentService : ICommentService
{
    private readonly ApplicationDbContext _context;

    public CommentService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Comment?> GetCommentByIdAsync(int id)
    {
        return await _context.Comments
            .Include(c => c.User)
            .Include(c => c.Post)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<IEnumerable<Comment>> GetCommentsByPostIdAsync(int postId)
    {
        return await _context.Comments
            .Include(c => c.User)
            .Where(c => c.PostId == postId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Comment>> GetCommentsByUserIdAsync(string userId)
    {
        return await _context.Comments
            .Include(c => c.User)
            .Include(c => c.Post)
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<Comment> CreateCommentAsync(int postId, string userId, string content)
    {
        var comment = Comment.Create(postId, userId, content);
        _context.Comments.Add(comment);
        await _context.SaveChangesAsync();
        return comment;
    }

    public async Task UpdateCommentAsync(int id, string content)
    {
        var comment = await _context.Comments.FindAsync(id);
        if (comment == null)
            throw new InvalidOperationException($"Comment with ID {id} not found");

        comment.UpdateContent(content);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteCommentAsync(int id)
    {
        var comment = await _context.Comments.FindAsync(id);
        if (comment == null)
            throw new InvalidOperationException($"Comment with ID {id} not found");

        _context.Comments.Remove(comment);
        await _context.SaveChangesAsync();
    }
}
