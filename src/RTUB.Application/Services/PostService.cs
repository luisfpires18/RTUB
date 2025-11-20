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

    public PostService(IPostRepository postRepository)
    {
        _postRepository = postRepository;
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

        return await _postRepository.AddAsync(post);
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
}
