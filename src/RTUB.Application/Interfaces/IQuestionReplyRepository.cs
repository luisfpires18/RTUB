using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for QuestionReply operations
/// </summary>
public interface IQuestionReplyRepository
{
    /// <summary>
    /// Gets all non-deleted replies for a question, ordered by creation date
    /// </summary>
    Task<IEnumerable<QuestionReply>> GetByQuestionIdAsync(int questionId);

    /// <summary>
    /// Gets a reply by ID
    /// </summary>
    Task<QuestionReply?> GetByIdAsync(int id);

    /// <summary>
    /// Adds a new reply
    /// </summary>
    Task<QuestionReply> AddAsync(QuestionReply reply);

    /// <summary>
    /// Updates an existing reply
    /// </summary>
    Task UpdateAsync(QuestionReply reply);

    /// <summary>
    /// Deletes a reply (soft delete)
    /// </summary>
    Task SoftDeleteAsync(int id);
}
