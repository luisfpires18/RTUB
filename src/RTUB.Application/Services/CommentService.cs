using Microsoft.AspNetCore.Components.Forms;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services;

/// <summary>
/// Comment service implementation using Repository pattern
/// Now depends on ICommentRepository abstraction instead of concrete DbContext
/// </summary>
public class CommentService : ICommentService
{
    private readonly ICommentRepository _commentRepository;
    private readonly ICommentImageRepository _commentImageRepository;
    private readonly IEventMediaStorageService _eventMediaStorageService;
    private readonly IPostRepository _postRepository;
    private readonly IDiscussionRepository _discussionRepository;
    private readonly IPostService _postService;

    public CommentService(
        ICommentRepository commentRepository,
        ICommentImageRepository commentImageRepository,
        IEventMediaStorageService eventMediaStorageService,
        IPostRepository postRepository,
        IDiscussionRepository discussionRepository,
        IPostService postService)
    {
        _commentRepository = commentRepository;
        _commentImageRepository = commentImageRepository;
        _eventMediaStorageService = eventMediaStorageService;
        _postRepository = postRepository;
        _discussionRepository = discussionRepository;
        _postService = postService;
    }

    public async Task<Comment?> GetByIdAsync(int id)
    {
        return await _commentRepository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<Comment>> GetByPostIdAsync(int postId, int page = 1, int pageSize = 50)
    {
        return await _commentRepository.GetByPostIdAsync(postId, page, pageSize);
    }

    public async Task<int> GetCountByPostIdAsync(int postId)
    {
        return await _commentRepository.GetCountByPostIdAsync(postId);
    }

    public async Task<Comment> CreateAsync(int postId, string authorId, string body, string? mentionsJson = null, IReadOnlyList<IBrowserFile>? imageFiles = null)
    {
        var comment = Comment.Create(postId, authorId, body);
        if (!string.IsNullOrWhiteSpace(mentionsJson))
        {
            comment.SetMentions(mentionsJson);
        }

        var createdComment = await _commentRepository.AddAsync(comment);

        // Upload and save images if provided
        if (imageFiles != null && imageFiles.Count > 0)
        {
            var post = await _postRepository.GetByIdAsync(postId);
            if (post != null)
            {
                var discussion = await _discussionRepository.GetByIdAsync(post.DiscussionId);
                var eventId = discussion?.EventId ?? 0;
                await UploadCommentImagesAsync(createdComment.Id, imageFiles, eventId);
            }
        }

        // Update post's last activity timestamp
        await _postService.UpdateLastActivityAsync(postId);

        return createdComment;
    }

    private async Task UploadCommentImagesAsync(int commentId, IReadOnlyList<IBrowserFile> files, int eventId)
    {
        int sortOrder = 0;
        foreach (var file in files)
        {
            // Copy to MemoryStream to make it seekable (required for S3 checksum calculation)
            using var browserStream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024); // 10MB max for comment images
            using var memoryStream = new MemoryStream();
            await browserStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            var url = await _eventMediaStorageService.UploadImageAsync(
                memoryStream, file.Name, file.ContentType, eventId, "comment");

            var image = CommentImage.Create(commentId, url, file.ContentType, file.Size, sortOrder++);
            await _commentImageRepository.AddAsync(image);
        }
    }

    public async Task UpdateAsync(int id, string body, string? mentionsJson = null)
    {
        var comment = await _commentRepository.GetByIdAsync(id);
        if (comment == null)
            throw new EntityNotFoundException(nameof(Comment), id);

        comment.Edit(body);
        if (!string.IsNullOrWhiteSpace(mentionsJson))
        {
            comment.SetMentions(mentionsJson);
        }

        await _commentRepository.UpdateAsync(comment);
    }

    public async Task SoftDeleteAsync(int id)
    {
        var comment = await _commentRepository.GetByIdAsync(id);
        if (comment == null)
            throw new EntityNotFoundException(nameof(Comment), id);

        // Get images before soft delete
        var images = await _commentImageRepository.GetByCommentIdAsync(id);

        comment.SoftDelete();
        await _commentRepository.UpdateAsync(comment);

        // Delete image files from storage
        foreach (var image in images)
        {
            try
            {
                await _eventMediaStorageService.DeleteMediaAsync(image.Url);
            }
            catch
            {
                // Log but don't fail - media cleanup is secondary
            }
        }

        // Remove image records
        await _commentImageRepository.DeleteByCommentIdAsync(id);
    }
}
