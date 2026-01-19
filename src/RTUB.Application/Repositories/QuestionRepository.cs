using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for Question operations
/// </summary>
public class QuestionRepository : IQuestionRepository
{
    private readonly ApplicationDbContext _context;

    public QuestionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Question>> GetAllAsync(int page, int pageSize, string? searchTerm = null)
    {
        var query = _context.Questions
            .AsNoTracking()
            .Include(q => q.Author)
            .Include(q => q.AssignedMember)
            .Where(q => !q.IsDeleted);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var lowerSearch = searchTerm.ToLower();
            query = query.Where(q =>
                q.Content.ToLower().Contains(lowerSearch) ||
                q.Author.Nickname!.ToLower().Contains(lowerSearch) ||
                q.Author.UserName!.ToLower().Contains(lowerSearch) ||
                q.AssignedMember.Nickname!.ToLower().Contains(lowerSearch) ||
                q.AssignedMember.UserName!.ToLower().Contains(lowerSearch));
        }

        return await query
            .OrderByDescending(q => q.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<IEnumerable<Question>> GetAllWithRepliesAsync(int page, int pageSize, string? searchTerm = null)
    {
        var query = _context.Questions
            .AsNoTracking()
            .Include(q => q.Author)
            .Include(q => q.AssignedMember)
            .Include(q => q.Replies.Where(r => !r.IsDeleted).OrderBy(r => r.CreatedAt))
                .ThenInclude(r => r.Author)
            .Where(q => !q.IsDeleted);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var lowerSearch = searchTerm.ToLower();
            query = query.Where(q =>
                q.Content.ToLower().Contains(lowerSearch) ||
                q.Author.Nickname!.ToLower().Contains(lowerSearch) ||
                q.Author.UserName!.ToLower().Contains(lowerSearch) ||
                q.AssignedMember.Nickname!.ToLower().Contains(lowerSearch) ||
                q.AssignedMember.UserName!.ToLower().Contains(lowerSearch));
        }

        return await query
            .OrderByDescending(q => q.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetCountAsync(string? searchTerm = null)
    {
        var query = _context.Questions
            .AsNoTracking()
            .Where(q => !q.IsDeleted);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var lowerSearch = searchTerm.ToLower();
            query = query.Where(q =>
                q.Content.ToLower().Contains(lowerSearch) ||
                q.Author.Nickname!.ToLower().Contains(lowerSearch) ||
                q.Author.UserName!.ToLower().Contains(lowerSearch) ||
                q.AssignedMember.Nickname!.ToLower().Contains(lowerSearch) ||
                q.AssignedMember.UserName!.ToLower().Contains(lowerSearch));
        }

        return await query.CountAsync();
    }

    public async Task<Question?> GetByIdWithRepliesAsync(int id)
    {
        return await _context.Questions
            .Include(q => q.Author)
            .Include(q => q.AssignedMember)
            .Include(q => q.Replies.Where(r => !r.IsDeleted).OrderBy(r => r.CreatedAt))
                .ThenInclude(r => r.Author)
            .FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted);
    }

    public async Task<Question?> GetByIdAsync(int id)
    {
        return await _context.Questions
            .Include(q => q.Author)
            .Include(q => q.AssignedMember)
            .FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted);
    }

    public async Task<IEnumerable<Question>> GetByAssignedMemberIdAsync(string memberId)
    {
        return await _context.Questions
            .AsNoTracking()
            .Include(q => q.Author)
            .Include(q => q.AssignedMember)
            .Where(q => q.AssignedMemberId == memberId && !q.IsDeleted)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Question>> GetByAuthorIdAsync(string authorId)
    {
        return await _context.Questions
            .AsNoTracking()
            .Include(q => q.Author)
            .Include(q => q.AssignedMember)
            .Where(q => q.AuthorId == authorId && !q.IsDeleted)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Question>> GetUnansweredQuestionsForNotificationAsync()
    {
        return await _context.Questions
            .AsNoTracking()
            .Include(q => q.Author)
            .Include(q => q.AssignedMember)
            .Where(q => !q.IsDeleted &&
                        !q.IsAwaitingUserReply &&
                        (q.Status == QuestionStatus.Unanswered || q.Status == QuestionStatus.InDiscussion))
            .ToListAsync();
    }

    public async Task<Question> AddAsync(Question question)
    {
        _context.Questions.Add(question);
        await _context.SaveChangesAsync();
        return question;
    }

    public async Task UpdateAsync(Question question)
    {
        var entry = _context.Entry(question);
        if (entry.State == EntityState.Detached)
        {
            _context.ChangeTracker.TrackGraph(question, node =>
            {
                if (node.Entry.Entity is Question)
                {
                    node.Entry.State = EntityState.Modified;
                }
                else
                {
                    node.Entry.State = EntityState.Detached;
                }
            });
        }
        else
        {
            entry.State = EntityState.Modified;
        }

        await _context.SaveChangesAsync();
    }

    public async Task SoftDeleteAsync(int id)
    {
        var question = await _context.Questions.FindAsync(id);
        if (question != null)
        {
            question.SoftDelete();
            await _context.SaveChangesAsync();
        }
    }
}
