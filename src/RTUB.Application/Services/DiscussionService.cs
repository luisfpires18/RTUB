using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Discussion service implementation
/// </summary>
public class DiscussionService : IDiscussionService
{
    private readonly ApplicationDbContext _context;

    public DiscussionService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Discussion?> GetByIdAsync(int id)
    {
        return await _context.Discussions
            .Include(d => d.Event)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<Discussion?> GetByEventIdAsync(int eventId)
    {
        return await _context.Discussions
            .Include(d => d.Event)
            .FirstOrDefaultAsync(d => d.EventId == eventId);
    }

    public async Task<Discussion> CreateForEventAsync(int eventId)
    {
        var discussion = Discussion.Create(eventId);
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();
        return discussion;
    }

    public async Task<Discussion> GetOrCreateForEventAsync(int eventId)
    {
        var discussion = await GetByEventIdAsync(eventId);
        if (discussion != null)
            return discussion;

        return await CreateForEventAsync(eventId);
    }
}
