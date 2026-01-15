using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for Question operations
/// </summary>
public interface IQuestionRepository
{
    /// <summary>
    /// Gets all non-deleted questions with pagination, ordered by creation date descending
    /// </summary>
    Task<IEnumerable<Question>> GetAllAsync(int page, int pageSize, string? searchTerm = null);

    /// <summary>
    /// Gets all non-deleted questions with pagination and replies loaded
    /// </summary>
    Task<IEnumerable<Question>> GetAllWithRepliesAsync(int page, int pageSize, string? searchTerm = null);

    /// <summary>
    /// Gets the total count of non-deleted questions
    /// </summary>
    Task<int> GetCountAsync(string? searchTerm = null);

    /// <summary>
    /// Gets a question by ID with author, assigned member and replies loaded
    /// </summary>
    Task<Question?> GetByIdWithRepliesAsync(int id);

    /// <summary>
    /// Gets a question by ID
    /// </summary>
    Task<Question?> GetByIdAsync(int id);

    /// <summary>
    /// Gets all questions assigned to a specific member
    /// </summary>
    Task<IEnumerable<Question>> GetByAssignedMemberIdAsync(string memberId);

    /// <summary>
    /// Gets all questions posted by a specific user
    /// </summary>
    Task<IEnumerable<Question>> GetByAuthorIdAsync(string authorId);

    /// <summary>
    /// Gets all unanswered questions that are not awaiting user reply
    /// Used for sending notification reminders
    /// </summary>
    Task<IEnumerable<Question>> GetUnansweredQuestionsForNotificationAsync();

    /// <summary>
    /// Adds a new question
    /// </summary>
    Task<Question> AddAsync(Question question);

    /// <summary>
    /// Updates an existing question
    /// </summary>
    Task UpdateAsync(Question question);

    /// <summary>
    /// Deletes a question (soft delete)
    /// </summary>
    Task SoftDeleteAsync(int id);
}
