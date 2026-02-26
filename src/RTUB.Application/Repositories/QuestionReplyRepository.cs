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
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public QuestionReplyRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    private ApplicationDbContext CreateContext() => _contextFactory.CreateDbContext();

    public async Task<IEnumerable<QuestionReply>> GetByQuestionIdAsync(int questionId)
    {
        using var context = CreateContext();
        return await context.QuestionReplies
            .AsNoTracking()
            .Include(r => r.Author)
            .Where(r => r.QuestionId == questionId && !r.IsDeleted)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<QuestionReply?> GetByIdAsync(int id)
    {
        using var context = CreateContext();
        return await context.QuestionReplies
            .AsNoTracking()
            .Include(r => r.Author)
            .Include(r => r.Question)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
    }

    public async Task<QuestionReply> AddAsync(QuestionReply reply)
    {
        using var context = CreateContext();
        context.QuestionReplies.Add(reply);
        await context.SaveChangesAsync();
        return reply;
    }

    public async Task UpdateAsync(QuestionReply reply)
    {
        using var context = CreateContext();
        // Use Entry().State to avoid traversing navigation properties (Author, Question)
        context.Entry(reply).State = EntityState.Modified;
        await context.SaveChangesAsync();
    }

    public async Task SoftDeleteAsync(int id)
    {
        using var context = CreateContext();
        var reply = await context.QuestionReplies.FindAsync(id);
        if (reply != null)
        {
            reply.SoftDelete();
            await context.SaveChangesAsync();
        }
    }
}
