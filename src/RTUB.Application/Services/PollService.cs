using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Poll service implementation
/// </summary>
public class PollService : IPollService
{
    private readonly ApplicationDbContext _context;

    public PollService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Poll?> GetPollAsync(int pollId)
    {
        return await _context.Polls
            .Include(p => p.Options)
                .ThenInclude(o => o.Votes)
            .Include(p => p.CreatedByUser)
            .FirstOrDefaultAsync(p => p.Id == pollId);
    }

    public async Task<IEnumerable<Poll>> GetAllPollsAsync(PollStatus? filterStatus = null)
    {
        var query = _context.Polls
            .Include(p => p.Options)
            .Include(p => p.CreatedByUser)
            .AsQueryable();

        if (filterStatus.HasValue)
        {
            query = query.Where(p => p.Status == filterStatus.Value);
        }

        return await query
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<Poll> CreatePollAsync(string title, string? description, string createdByUserId, 
        PollType pollType, bool isAnonymous, int maxVotesPerUser, DateTime? startDate = null, DateTime? endDate = null)
    {
        // Validate dates
        if (startDate.HasValue && endDate.HasValue && endDate.Value <= startDate.Value)
        {
            throw new InvalidOperationException("A data de término deve ser posterior à data de início");
        }

        var poll = Poll.Create(title, createdByUserId, pollType);
        poll.UpdateDetails(title, description, startDate, endDate, isAnonymous, maxVotesPerUser);

        // Set status based on start date
        if (startDate.HasValue && startDate.Value > DateTime.UtcNow)
        {
            poll.SetStatus(PollStatus.Scheduled);
        }

        _context.Polls.Add(poll);
        await _context.SaveChangesAsync();
        
        return poll;
    }

    public async Task<PollOption> AddOptionAsync(int pollId, string optionText)
    {
        var poll = await _context.Polls.FindAsync(pollId);
        if (poll == null)
        {
            throw new InvalidOperationException("Votação não encontrada");
        }

        var option = PollOption.Create(optionText, pollId);
        _context.PollOptions.Add(option);
        await _context.SaveChangesAsync();
        
        return option;
    }

    public async Task UpdatePollAsync(int pollId, string title, string? description, DateTime? startDate, 
        DateTime? endDate, bool isAnonymous, int maxVotesPerUser)
    {
        var poll = await _context.Polls.FindAsync(pollId);
        if (poll == null)
        {
            throw new InvalidOperationException("Votação não encontrada");
        }

        // Validate dates
        if (startDate.HasValue && endDate.HasValue && endDate.Value <= startDate.Value)
        {
            throw new InvalidOperationException("A data de término deve ser posterior à data de início");
        }

        poll.UpdateDetails(title, description, startDate, endDate, isAnonymous, maxVotesPerUser);
        await _context.SaveChangesAsync();
    }

    public async Task DeletePollAsync(int pollId)
    {
        var poll = await _context.Polls
            .Include(p => p.Options)
                .ThenInclude(o => o.Votes)
            .FirstOrDefaultAsync(p => p.Id == pollId);
        
        if (poll == null)
        {
            throw new InvalidOperationException("Votação não encontrada");
        }

        _context.Polls.Remove(poll);
        await _context.SaveChangesAsync();
    }

    public async Task ClosePollAsync(int pollId)
    {
        var poll = await _context.Polls.FindAsync(pollId);
        if (poll == null)
        {
            throw new InvalidOperationException("Votação não encontrada");
        }

        poll.Close();
        await _context.SaveChangesAsync();
    }

    public async Task VoteAsync(int pollId, string userId, List<int> optionIds)
    {
        var poll = await _context.Polls
            .Include(p => p.Options)
                .ThenInclude(o => o.Votes)
            .FirstOrDefaultAsync(p => p.Id == pollId);

        if (poll == null)
        {
            throw new InvalidOperationException("Votação não encontrada");
        }

        // Validate poll is active
        if (!poll.IsActive())
        {
            throw new InvalidOperationException("Esta votação não está ativa");
        }

        // Validate options belong to this poll
        var validOptionIds = poll.Options.Select(o => o.Id).ToList();
        if (optionIds.Any(id => !validOptionIds.Contains(id)))
        {
            throw new InvalidOperationException("Opção inválida selecionada");
        }

        // Check if user has already voted (for non-anonymous polls)
        if (!poll.IsAnonymous)
        {
            var existingVotes = await _context.PollVotes
                .Where(v => v.PollOption!.PollId == pollId && v.UserId == userId)
                .ToListAsync();

            if (existingVotes.Any())
            {
                throw new InvalidOperationException("Você já votou nesta votação");
            }
        }

        // Validate vote count
        if (poll.PollType == PollType.SingleChoice && optionIds.Count != 1)
        {
            throw new InvalidOperationException("Você deve selecionar exatamente uma opção");
        }

        if (poll.PollType == PollType.MultipleChoice && optionIds.Count > poll.MaxVotesPerUser)
        {
            throw new InvalidOperationException($"Você pode selecionar no máximo {poll.MaxVotesPerUser} opções");
        }

        if (optionIds.Count == 0)
        {
            throw new InvalidOperationException("Você deve selecionar pelo menos uma opção");
        }

        // Create votes
        var votes = optionIds.Select(optionId => 
            PollVote.Create(optionId, poll.IsAnonymous ? null : userId)
        ).ToList();

        _context.PollVotes.AddRange(votes);
        await _context.SaveChangesAsync();
    }

    public async Task<Dictionary<int, int>> GetPollResultsAsync(int pollId)
    {
        var poll = await _context.Polls
            .Include(p => p.Options)
                .ThenInclude(o => o.Votes)
            .FirstOrDefaultAsync(p => p.Id == pollId);

        if (poll == null)
        {
            throw new InvalidOperationException("Votação não encontrada");
        }

        var results = poll.Options.ToDictionary(
            option => option.Id,
            option => option.Votes.Count
        );

        return results;
    }

    public async Task<List<int>> GetUserVotesForPollAsync(int pollId, string userId)
    {
        var votes = await _context.PollVotes
            .Where(v => v.PollOption!.PollId == pollId && v.UserId == userId)
            .Select(v => v.PollOptionId)
            .ToListAsync();

        return votes;
    }

    public async Task<bool> HasUserVotedAsync(int pollId, string userId)
    {
        return await _context.PollVotes
            .AnyAsync(v => v.PollOption!.PollId == pollId && v.UserId == userId);
    }

    public async Task RemoveVoteAsync(int pollId, string userId)
    {
        var votes = await _context.PollVotes
            .Where(v => v.PollOption!.PollId == pollId && v.UserId == userId)
            .ToListAsync();

        if (votes.Any())
        {
            _context.PollVotes.RemoveRange(votes);
            await _context.SaveChangesAsync();
        }
    }
}
