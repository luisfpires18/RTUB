using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Discussion service implementation
/// Contains business logic for discussion operations
/// </summary>
public class DiscussionService : IDiscussionService
{
    private readonly ApplicationDbContext _context;

    public DiscussionService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Discussion?> GetDiscussionByIdAsync(int id)
    {
        return await _context.Discussions
            .Include(d => d.Posts)
            .ThenInclude(p => p.User)
            .Include(d => d.Event)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<Discussion?> GetDiscussionByEventIdAsync(int eventId)
    {
        return await _context.Discussions
            .Include(d => d.Posts)
            .ThenInclude(p => p.User)
            .Include(d => d.Event)
            .FirstOrDefaultAsync(d => d.EventId == eventId);
    }

    public async Task<Discussion> CreateDiscussionAsync(int eventId, string? title = null)
    {
        var discussion = Discussion.Create(eventId, title);
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();
        return discussion;
    }

    public async Task UpdateDiscussionAsync(int id, string? title)
    {
        var discussion = await _context.Discussions.FindAsync(id);
        if (discussion == null)
            throw new InvalidOperationException($"Discussion with ID {id} not found");

        discussion.UpdateTitle(title);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteDiscussionAsync(int id)
    {
        var discussion = await _context.Discussions.FindAsync(id);
        if (discussion == null)
            throw new InvalidOperationException($"Discussion with ID {id} not found");

        _context.Discussions.Remove(discussion);
        await _context.SaveChangesAsync();
    }
}
