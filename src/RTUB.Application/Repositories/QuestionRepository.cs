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

    public async Task<IEnumerable<Question>> GetAllAsync(int page, int pageSize, string? searchTerm = null, bool? isClosedFilter = null, string? assignedMemberIdFilter = null)
    {
        var query = BuildBaseQuery(searchTerm, isClosedFilter, assignedMemberIdFilter, includeReplies: true);

        // For non-closed questions, order by latest reply or creation date
        // Note: Fetch to client-side first to avoid expensive SQL subquery
        if (isClosedFilter == false)
        {
            return await ApplyClientSideSortingAndPagination(query, page, pageSize);
        }

        return await query
            .OrderByDescending(q => q.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<IEnumerable<Question>> GetAllWithRepliesAsync(int page, int pageSize, string? searchTerm = null, bool? isClosedFilter = null, string? assignedMemberIdFilter = null)
    {
        var query = BuildBaseQueryWithReplies(searchTerm, isClosedFilter, assignedMemberIdFilter);

        // For non-closed questions, order by latest reply or creation date
        // Note: Fetch to client-side first to avoid expensive SQL subquery
        if (isClosedFilter == false)
        {
            return await ApplyClientSideSortingAndPagination(query, page, pageSize);
        }

        return await query
            .OrderByDescending(q => q.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    private IQueryable<Question> BuildBaseQuery(string? searchTerm, bool? isClosedFilter, string? assignedMemberIdFilter, bool includeReplies)
    {
        IQueryable<Question> query = _context.Questions
            .AsNoTracking()
            .Include(q => q.Author)
            .Include(q => q.AssignedMember);

        if (includeReplies)
        {
            query = query.Include(q => q.Replies.Where(r => !r.IsDeleted));
        }

        query = query.Where(q => !q.IsDeleted);

        return ApplyFilters(query, searchTerm, isClosedFilter, assignedMemberIdFilter);
    }

    private IQueryable<Question> BuildBaseQueryWithReplies(string? searchTerm, bool? isClosedFilter, string? assignedMemberIdFilter)
    {
        var query = _context.Questions
            .AsNoTracking()
            .Include(q => q.Author)
            .Include(q => q.AssignedMember)
            .Include(q => q.Replies.Where(r => !r.IsDeleted).OrderBy(r => r.CreatedAt))
                .ThenInclude(r => r.Author)
            .Where(q => !q.IsDeleted);

        return ApplyFilters(query, searchTerm, isClosedFilter, assignedMemberIdFilter);
    }

    private IQueryable<Question> ApplyFilters(IQueryable<Question> query, string? searchTerm, bool? isClosedFilter, string? assignedMemberIdFilter)
    {
        // Apply status filter
        if (isClosedFilter.HasValue)
        {
            if (isClosedFilter.Value)
            {
                query = query.Where(q => q.Status == QuestionStatus.Closed);
            }
            else
            {
                query = query.Where(q => q.Status != QuestionStatus.Closed);
            }
        }

        // Apply assigned member filter
        if (!string.IsNullOrWhiteSpace(assignedMemberIdFilter))
        {
            query = query.Where(q => q.AssignedMemberId == assignedMemberIdFilter);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var lowerSearch = searchTerm.ToLower();
            query = query.Where(q =>
                q.Title.ToLower().Contains(lowerSearch) ||
                q.Content.ToLower().Contains(lowerSearch) ||
                q.Author.Nickname!.ToLower().Contains(lowerSearch) ||
                q.Author.UserName!.ToLower().Contains(lowerSearch) ||
                q.AssignedMember.Nickname!.ToLower().Contains(lowerSearch) ||
                q.AssignedMember.UserName!.ToLower().Contains(lowerSearch));
        }

        return query;
    }

    private async Task<List<Question>> ApplyClientSideSortingAndPagination(IQueryable<Question> query, int page, int pageSize)
    {
        // Note: This approach loads all matching records to avoid complex SQL.
        // For very large datasets (>1000 records), consider database-level sorting with computed columns.
        var results = await query.ToListAsync();
        return results
            .OrderByDescending(q => q.Replies.Any() ? q.Replies.Max(r => r.CreatedAt) : q.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
    }

    public async Task<int> GetCountAsync(string? searchTerm = null, bool? isClosedFilter = null, string? assignedMemberIdFilter = null)
    {
        var query = _context.Questions
            .AsNoTracking()
            .Where(q => !q.IsDeleted);

        // Apply status filter
        if (isClosedFilter.HasValue)
        {
            if (isClosedFilter.Value)
            {
                query = query.Where(q => q.Status == QuestionStatus.Closed);
            }
            else
            {
                query = query.Where(q => q.Status != QuestionStatus.Closed);
            }
        }

        // Apply assigned member filter
        if (!string.IsNullOrWhiteSpace(assignedMemberIdFilter))
        {
            query = query.Where(q => q.AssignedMemberId == assignedMemberIdFilter);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var lowerSearch = searchTerm.ToLower();
            query = query.Where(q =>
                q.Title.ToLower().Contains(lowerSearch) ||
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
