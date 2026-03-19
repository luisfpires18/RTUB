using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Application.Utilities;
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
    private readonly IPostMediaRepository _postMediaRepository;
    private readonly IEventMediaStorageService _eventMediaStorageService;
    private readonly IPushNotificationFactory _pushNotificationFactory;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUserProfileService _userProfileService;

    public PostService(
        IPostRepository postRepository,
        IDiscussionRepository discussionRepository,
        IEnrollmentRepository enrollmentRepository,
        IPostMediaRepository postMediaRepository,
        IEventMediaStorageService eventMediaStorageService,
        IPushNotificationFactory pushNotificationFactory,
        IPushNotificationService pushNotificationService,
        IHttpContextAccessor httpContextAccessor,
        IUserProfileService userProfileService)
    {
        _postRepository = postRepository;
        _discussionRepository = discussionRepository;
        _enrollmentRepository = enrollmentRepository;
        _postMediaRepository = postMediaRepository;
        _eventMediaStorageService = eventMediaStorageService;
        _pushNotificationFactory = pushNotificationFactory;
        _pushNotificationService = pushNotificationService;
        _httpContextAccessor = httpContextAccessor;
        _userProfileService = userProfileService;
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

    public async Task<Post> CreateAsync(int discussionId, string authorId, string title, string body, string? mentionsJson = null, IReadOnlyList<IBrowserFile>? imageFiles = null, IReadOnlyList<IBrowserFile>? videoFiles = null)
    {
        var post = Post.Create(discussionId, authorId, title, body);
        if (!string.IsNullOrWhiteSpace(mentionsJson))
        {
            post.SetMentions(mentionsJson);
        }

        var createdPost = await _postRepository.AddAsync(post);

        // Upload and save media if provided
        var discussion = await _discussionRepository.GetByIdWithEventAsync(discussionId);
        var eventId = discussion?.EventId ?? 0;

        if (imageFiles != null && imageFiles.Count > 0)
        {
            await UploadPostMediaAsync(createdPost.Id, imageFiles, "Image", eventId);
        }

        if (videoFiles != null && videoFiles.Count > 0)
        {
            await UploadPostMediaAsync(createdPost.Id, videoFiles, "Video", eventId);
        }

        // Send push notifications
        try
        {
            if (discussion?.Event != null)
            {
                var authorUser = await _userProfileService.GetUserByIdAsync(authorId);

                if (authorUser != null)
                {
                    var baseUrl = GetBaseUrl();
                    var authorNickname = authorUser.Nickname ?? authorUser.FirstName ?? "Utilizador";

                    // Send post notification to enrolled users (only for future events)
                    if (discussion.Event.Date >= DateTime.UtcNow)
                    {
                        var notification = _pushNotificationFactory.CreateDiscussionPostNotification(
                            discussion.Event,
                            authorNickname,
                            title,
                            baseUrl);

                        var enrolledUserIds = await _enrollmentRepository.QueryAsync(q => q
                            .Where(e => e.EventId == discussion.Event.Id && e.UserId != authorId && e.WillAttend)
                            .Select(e => e.UserId)
                            .ToListAsync());

                        foreach (var userId in enrolledUserIds)
                        {
                            await _pushNotificationService.SendToUserAsync(userId, notification);
                        }
                    }

                    // Send mention notifications (always, regardless of event date)
                    if (!string.IsNullOrWhiteSpace(mentionsJson))
                    {
                        var mentions = JsonSerializer.Deserialize<Dictionary<string, string>>(mentionsJson);
                        if (mentions != null)
                        {
                            var mentionNotification = _pushNotificationFactory.CreateMentionNotification(
                                discussion.Event, authorNickname, title, isComment: false, baseUrl);
                            foreach (var mentionedUserId in mentions.Values.Where(id => id != authorId))
                            {
                                await _pushNotificationService.SendToUserAsync(mentionedUserId, mentionNotification);
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Notification failure must not fail the operation
        }

        return createdPost;
    }

    private async Task UploadPostMediaAsync(int postId, IReadOnlyList<IBrowserFile> files, string mediaType, int eventId)
    {
        int sortOrder = 0;
        foreach (var file in files)
        {
            // Copy to MemoryStream to make it seekable (required for S3 checksum calculation)
            using var browserStream = file.OpenReadStream(maxAllowedSize: 100 * 1024 * 1024); // 100MB max
            using var memoryStream = new MemoryStream();
            await browserStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            // Ensure we have a valid MIME type (mobile uploads may have empty/incorrect contentType)
            var mimeType = MimeTypeHelper.GetMediaMimeType(file.Name, file.ContentType, mediaType == "Video");

            string url;
            if (mediaType == "Image")
            {
                url = await _eventMediaStorageService.UploadImageAsync(
                    memoryStream, file.Name, mimeType, eventId, "post");
            }
            else
            {
                url = await _eventMediaStorageService.UploadVideoAsync(
                    memoryStream, file.Name, mimeType, eventId);
            }

            var media = mediaType == "Image"
                ? PostMedia.CreateImage(postId, url, mimeType, file.Size, sortOrder++)
                : PostMedia.CreateVideo(postId, url, mimeType, file.Size, sortOrder++);

            await _postMediaRepository.AddAsync(media);
        }
    }

    public async Task UpdateAsync(int id, string title, string body, string? mentionsJson = null)
    {
        var post = await _postRepository.GetByIdOrThrowAsync(id);

        post.Edit(title, body);
        if (!string.IsNullOrWhiteSpace(mentionsJson))
        {
            post.SetMentions(mentionsJson);
        }

        await _postRepository.UpdateAsync(post);
    }

    public async Task PinAsync(int id)
    {
        var post = await _postRepository.GetByIdOrThrowAsync(id);

        post.Pin();
        await _postRepository.UpdateAsync(post);
    }

    public async Task UnpinAsync(int id)
    {
        var post = await _postRepository.GetByIdOrThrowAsync(id);

        post.Unpin();
        await _postRepository.UpdateAsync(post);
    }

    public async Task LockAsync(int id)
    {
        var post = await _postRepository.GetByIdOrThrowAsync(id);

        post.Lock();
        await _postRepository.UpdateAsync(post);
    }

    public async Task UnlockAsync(int id)
    {
        var post = await _postRepository.GetByIdOrThrowAsync(id);

        post.Unlock();
        await _postRepository.UpdateAsync(post);
    }

    public async Task SoftDeleteAsync(int id)
    {
        var post = await _postRepository.GetByIdAsync(id);
        if (post == null)
            throw new EntityNotFoundException(nameof(Post), id);

        // Get media before soft delete
        var media = await _postMediaRepository.GetByPostIdAsync(id);

        post.SoftDelete();
        await _postRepository.UpdateAsync(post);

        // Delete media files from storage
        foreach (var item in media)
        {
            try
            {
                await _eventMediaStorageService.DeleteMediaAsync(item.Url);
            }
            catch
            {
                // Log but don't fail - media cleanup is secondary
            }
        }

        // Remove media records (cascade delete will handle this, but explicit is clearer)
        await _postMediaRepository.DeleteByPostIdAsync(id);
    }

    public async Task UpdateLastActivityAsync(int id)
    {
        var post = await _postRepository.GetByIdOrThrowAsync(id);

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
