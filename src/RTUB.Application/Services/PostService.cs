using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services;

/// <summary>
/// Post service implementation using Repository pattern
/// Now depends on IPostRepository abstraction instead of concrete DbContext
/// </summary>
public class PostService : IPostService
{
    private readonly IPostRepository _postRepository;
    private readonly IDiscussionRepository _discussionRepository;
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly IPushNotificationFactory _pushNotificationFactory;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PostService(
        IPostRepository postRepository,
        IDiscussionRepository discussionRepository,
        IEnrollmentRepository enrollmentRepository,
        IPushNotificationFactory pushNotificationFactory,
        IPushNotificationService pushNotificationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _postRepository = postRepository;
        _discussionRepository = discussionRepository;
        _enrollmentRepository = enrollmentRepository;
        _pushNotificationFactory = pushNotificationFactory;
        _pushNotificationService = pushNotificationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Post?> GetByIdAsync(int id)
    {
        return await _postRepository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<Post>> GetByDiscussionIdAsync(int discussionId, int page = 1, int pageSize = 20, string? searchTerm = null)
    {
        return await _postRepository.GetByDiscussionIdAsync(discussionId, page, pageSize, searchTerm);
    }

    public async Task<int> GetCountByDiscussionIdAsync(int discussionId, string? searchTerm = null)
    {
        return await _postRepository.GetCountByDiscussionIdAsync(discussionId, searchTerm);
    }

    public async Task<Post> CreateAsync(int discussionId, string authorId, string title, string body, string? mentionsJson = null)
    {
        var post = Post.Create(discussionId, authorId, title, body);
        if (!string.IsNullOrWhiteSpace(mentionsJson))
        {
            post.SetMentions(mentionsJson);
        }

        var createdPost = await _postRepository.AddAsync(post);

        // Send push notification to enrolled users
        try
        {
            var discussion = await _discussionRepository.GetByIdWithEventAsync(discussionId);
            if (discussion?.Event != null)
            {
                // Load author to get nickname (use Query to include navigation properties)
                var authorUser = await _enrollmentRepository.Query()
                    .Where(e => e.UserId == authorId)
                    .Select(e => e.User)
                    .FirstOrDefaultAsync();

                if (authorUser != null)
                {
                    var baseUrl = GetBaseUrl();
                    var authorNickname = authorUser.Nickname ?? authorUser.FirstName ?? "Utilizador";
                    var notification = _pushNotificationFactory.CreateDiscussionPostNotification(
                        discussion.Event,
                        authorNickname,
                        title,
                        baseUrl);

                    // Get enrolled users with WillAttend=true (excluding the author)
                    var enrolledUserIds = await _enrollmentRepository.Query()
                        .Where(e => e.EventId == discussion.Event.Id && e.UserId != authorId && e.WillAttend)
                        .Select(e => e.UserId)
                        .ToListAsync();

                    // Send to each enrolled user
                    foreach (var userId in enrolledUserIds)
                    {
                        await _pushNotificationService.SendToUserAsync(userId, notification);
                    }
                }
            }
        }
        catch
        {
            // Log error but don't fail the operation
            // Notification is secondary to the main operation
        }

        return createdPost;
    }

    public async Task UpdateAsync(int id, string title, string body, string? mentionsJson = null)
    {
        var post = await _postRepository.GetByIdAsync(id);
        if (post == null)
            throw new EntityNotFoundException(nameof(Post), id);

        post.Edit(title, body);
        if (!string.IsNullOrWhiteSpace(mentionsJson))
        {
            post.SetMentions(mentionsJson);
        }

        await _postRepository.UpdateAsync(post);
    }

    public async Task PinAsync(int id)
    {
        var post = await _postRepository.GetByIdAsync(id);
        if (post == null)
            throw new EntityNotFoundException(nameof(Post), id);

        post.Pin();
        await _postRepository.UpdateAsync(post);
    }

    public async Task UnpinAsync(int id)
    {
        var post = await _postRepository.GetByIdAsync(id);
        if (post == null)
            throw new EntityNotFoundException(nameof(Post), id);

        post.Unpin();
        await _postRepository.UpdateAsync(post);
    }

    public async Task LockAsync(int id)
    {
        var post = await _postRepository.GetByIdAsync(id);
        if (post == null)
            throw new EntityNotFoundException(nameof(Post), id);

        post.Lock();
        await _postRepository.UpdateAsync(post);
    }

    public async Task UnlockAsync(int id)
    {
        var post = await _postRepository.GetByIdAsync(id);
        if (post == null)
            throw new EntityNotFoundException(nameof(Post), id);

        post.Unlock();
        await _postRepository.UpdateAsync(post);
    }

    public async Task SoftDeleteAsync(int id)
    {
        var post = await _postRepository.GetByIdAsync(id);
        if (post == null)
            throw new EntityNotFoundException(nameof(Post), id);

        post.SoftDelete();
        await _postRepository.UpdateAsync(post);
    }

    public async Task UpdateLastActivityAsync(int id)
    {
        var post = await _postRepository.GetByIdAsync(id);
        if (post == null)
            throw new EntityNotFoundException(nameof(Post), id);

        post.UpdateLastActivity();
        await _postRepository.UpdateAsync(post);
    }

    private string GetBaseUrl()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request != null)
        {
            return $"{request.Scheme}://{request.Host}";
        }
        return "https://rtub.pt"; // Fallback
    }
}
