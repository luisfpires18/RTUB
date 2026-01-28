namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for event discussion operations (comment counts, etc.)
/// Extracted from EventDiscussion.razor to improve separation of concerns
/// </summary>
public interface IEventDiscussionService
{
    /// <summary>
    /// Loads comment counts for multiple posts in a single batch operation
    /// Avoids N+1 queries by batching the count requests
    /// </summary>
    /// <param name="postIds">Collection of post IDs to get comment counts for</param>
    /// <returns>Dictionary mapping post ID to comment count</returns>
    Task<Dictionary<int, int>> GetCommentCountsByPostIdsAsync(IEnumerable<int> postIds);
}
