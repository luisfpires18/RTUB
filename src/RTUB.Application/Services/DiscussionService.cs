using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Discussion service implementation
/// </summary>
public class DiscussionService : IDiscussionService
{
    private readonly IDiscussionRepository _discussionRepository;

    public DiscussionService(IDiscussionRepository discussionRepository)
    {
        _discussionRepository = discussionRepository;
    }

    public async Task<Discussion?> GetByIdAsync(int id)
    {
        return await _discussionRepository.GetByIdWithEventAsync(id);
    }

    public async Task<Discussion?> GetByEventIdAsync(int eventId)
    {
        return await _discussionRepository.GetByEventIdAsync(eventId);
    }

    public async Task<Discussion> CreateForEventAsync(int eventId)
    {
        var discussion = Discussion.Create(eventId);
        return await _discussionRepository.AddAsync(discussion);
    }

    public async Task<Discussion> GetOrCreateForEventAsync(int eventId)
    {
        return await GetByEventIdAsync(eventId) ?? await CreateForEventAsync(eventId);
    }
}
