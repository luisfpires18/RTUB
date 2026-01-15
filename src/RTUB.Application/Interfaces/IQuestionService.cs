using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for Question operations
/// </summary>
public interface IQuestionService
{
    /// <summary>
    /// Gets all questions with pagination
    /// </summary>
    Task<IEnumerable<Question>> GetAllAsync(int page, int pageSize, string? searchTerm = null);

    /// <summary>
    /// Gets the total count of questions
    /// </summary>
    Task<int> GetCountAsync(string? searchTerm = null);

    /// <summary>
    /// Gets a question by ID with all replies
    /// </summary>
    Task<Question?> GetByIdWithRepliesAsync(int id);

    /// <summary>
    /// Gets a question by ID
    /// </summary>
    Task<Question?> GetByIdAsync(int id);

    /// <summary>
    /// Creates a new question
    /// </summary>
    Task<Question> CreateAsync(string content, string authorId, Position assignedPosition, string assignedMemberId);

    /// <summary>
    /// Adds a reply to a question
    /// </summary>
    Task<QuestionReply> AddReplyAsync(int questionId, string content, string authorId);

    /// <summary>
    /// Deletes a question (soft delete). Only the author can delete their own questions.
    /// </summary>
    Task<bool> DeleteAsync(int questionId, string requestingUserId);

    /// <summary>
    /// Sends a manual notification reminder for a question
    /// </summary>
    Task SendManualReminderAsync(int questionId, string requestingUserId);

    /// <summary>
    /// Checks if a user can answer a question (must be the assigned member)
    /// </summary>
    Task<bool> CanAnswerAsync(int questionId, string userId);

    /// <summary>
    /// Checks if a user has any of the Orgãos Sociais positions
    /// </summary>
    bool HasOrgaoSocialPosition(ApplicationUser user);

    /// <summary>
    /// Gets all members with a specific position
    /// </summary>
    Task<IEnumerable<ApplicationUser>> GetMembersWithPositionAsync(Position position);
}
