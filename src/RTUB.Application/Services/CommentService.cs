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
    private readonly IPostService _postService;

    public CommentService(ICommentRepository commentRepository, IPostService postService)
    {
        _commentRepository = commentRepository;
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

    public async Task<Comment> CreateAsync(int postId, string authorId, string body, string? mentionsJson = null)
    {
        var comment = Comment.Create(postId, authorId, body);
        if (!string.IsNullOrWhiteSpace(mentionsJson))
        {
            comment.SetMentions(mentionsJson);
        }

        var createdComment = await _commentRepository.AddAsync(comment);

        // Update post's last activity timestamp
        await _postService.UpdateLastActivityAsync(postId);

        return createdComment;
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

        comment.SoftDelete();
        await _commentRepository.UpdateAsync(comment);
    }
}
