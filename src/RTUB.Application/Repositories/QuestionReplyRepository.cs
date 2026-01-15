using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for QuestionReply operations
/// </summary>
public class QuestionReplyRepository : IQuestionReplyRepository
{
    private readonly ApplicationDbContext _context;

    public QuestionReplyRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<QuestionReply>> GetByQuestionIdAsync(int questionId)
    {
        return await _context.QuestionReplies
            .AsNoTracking()
            .Include(r => r.Author)
            .Where(r => r.QuestionId == questionId && !r.IsDeleted)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<QuestionReply?> GetByIdAsync(int id)
    {
        return await _context.QuestionReplies
            .Include(r => r.Author)
            .Include(r => r.Question)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
    }

    public async Task<QuestionReply> AddAsync(QuestionReply reply)
    {
        _context.QuestionReplies.Add(reply);
        await _context.SaveChangesAsync();
        return reply;
    }

    public async Task UpdateAsync(QuestionReply reply)
    {
        _context.QuestionReplies.Update(reply);
        await _context.SaveChangesAsync();
    }

    public async Task SoftDeleteAsync(int id)
    {
        var reply = await _context.QuestionReplies.FindAsync(id);
        if (reply != null)
        {
            reply.SoftDelete();
            await _context.SaveChangesAsync();
        }
    }
}
