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
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public QuestionRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    private ApplicationDbContext CreateContext() => _contextFactory.CreateDbContext();

    public async Task<IEnumerable<Question>> GetAllAsync(int page, int pageSize, string? searchTerm = null, bool? isClosedFilter = null, string? assignedMemberIdFilter = null)
    {
        using var context = CreateContext();
        var query = BuildBaseQuery(context, searchTerm, isClosedFilter, assignedMemberIdFilter, includeReplies: true);

        return await query
            .OrderByDescending(q => q.LastActivityAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<IEnumerable<Question>> GetAllWithRepliesAsync(int page, int pageSize, string? searchTerm = null, bool? isClosedFilter = null, string? assignedMemberIdFilter = null)
    {
        using var context = CreateContext();
        var query = BuildBaseQueryWithReplies(context, searchTerm, isClosedFilter, assignedMemberIdFilter);

        return await query
            .OrderByDescending(q => q.LastActivityAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    private static IQueryable<Question> BuildBaseQuery(ApplicationDbContext context, string? searchTerm, bool? isClosedFilter, string? assignedMemberIdFilter, bool includeReplies)
    {
        IQueryable<Question> query = context.Questions
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

    private static IQueryable<Question> BuildBaseQueryWithReplies(ApplicationDbContext context, string? searchTerm, bool? isClosedFilter, string? assignedMemberIdFilter)
    {
        var query = context.Questions
            .AsNoTracking()
            .Include(q => q.Author)
            .Include(q => q.AssignedMember)
            .Include(q => q.Replies.Where(r => !r.IsDeleted).OrderBy(r => r.CreatedAt))
                .ThenInclude(r => r.Author)
            .Where(q => !q.IsDeleted);

        return ApplyFilters(query, searchTerm, isClosedFilter, assignedMemberIdFilter);
    }

    private static IQueryable<Question> ApplyFilters(IQueryable<Question> query, string? searchTerm, bool? isClosedFilter, string? assignedMemberIdFilter)
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

    public async Task<int> GetCountAsync(string? searchTerm = null, bool? isClosedFilter = null, string? assignedMemberIdFilter = null)
    {
        using var context = CreateContext();
        var query = context.Questions
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
        using var context = CreateContext();
        return await context.Questions
            .AsNoTracking()
            .Include(q => q.Author)
            .Include(q => q.AssignedMember)
            .Include(q => q.Replies.Where(r => !r.IsDeleted).OrderBy(r => r.CreatedAt))
                .ThenInclude(r => r.Author)
            .FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted);
    }

    public async Task<Question?> GetByIdAsync(int id)
    {
        using var context = CreateContext();
        return await context.Questions
            .AsNoTracking()
            .Include(q => q.Author)
            .Include(q => q.AssignedMember)
            .FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted);
    }

    public async Task<IEnumerable<Question>> GetByAssignedMemberIdAsync(string memberId)
    {
        using var context = CreateContext();
        return await context.Questions
            .AsNoTracking()
            .Include(q => q.Author)
            .Include(q => q.AssignedMember)
            .Where(q => q.AssignedMemberId == memberId && !q.IsDeleted)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Question>> GetByAuthorIdAsync(string authorId)
    {
        using var context = CreateContext();
        return await context.Questions
            .AsNoTracking()
            .Include(q => q.Author)
            .Include(q => q.AssignedMember)
            .Where(q => q.AuthorId == authorId && !q.IsDeleted)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Question>> GetUnansweredQuestionsForNotificationAsync()
    {
        using var context = CreateContext();
        return await context.Questions
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
        using var context = CreateContext();
        context.Questions.Add(question);
        await context.SaveChangesAsync();
        return question;
    }

    public async Task UpdateAsync(Question question)
    {
        using var context = CreateContext();
        // Use Entry().State to only update the Question entity, ignoring navigation
        // properties (Author, AssignedMember, Replies) to avoid tracking conflicts.
        context.Entry(question).State = EntityState.Modified;
        await context.SaveChangesAsync();
    }

    public async Task SoftDeleteAsync(int id)
    {
        using var context = CreateContext();
        var question = await context.Questions.FindAsync(id);
        if (question != null)
        {
            question.SoftDelete();
            await context.SaveChangesAsync();
        }
    }
}
