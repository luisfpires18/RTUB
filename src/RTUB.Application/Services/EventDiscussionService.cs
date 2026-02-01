using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Service for event discussion operations (comment counts, etc.)
/// Extracted from EventDiscussion.razor to improve separation of concerns
/// </summary>
public class EventDiscussionService : IEventDiscussionService
{
    private readonly ICommentService _commentService;

    /// <summary>
    /// Initializes a new instance of the EventDiscussionService
    /// </summary>
    /// <param name="commentService">Service for comment operations</param>
    public EventDiscussionService(ICommentService commentService)
    {
        _commentService = commentService ?? throw new ArgumentNullException(nameof(commentService));
    }

    /// <summary>
    /// Loads comment counts for multiple posts in a single batch operation
    /// Avoids N+1 queries by batching the count requests
    /// </summary>
    /// <param name="postIds">Collection of post IDs to get comment counts for</param>
    /// <returns>Dictionary mapping post ID to comment count</returns>
    public async Task<Dictionary<int, int>> GetCommentCountsByPostIdsAsync(IEnumerable<int> postIds)
    {
        var counts = await _commentService.GetCountsByPostIdsAsync(postIds);

        // Handle null return (should not happen, but defensive programming)
        if (counts == null)
        {
            return postIds.ToDictionary(id => id, _ => 0);
        }

        return counts;
    }
}
