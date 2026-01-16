using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for Question operations
/// </summary>
public interface IQuestionService
{
    /// <summary>
    /// Gets all questions with pagination and includes replies
    /// </summary>
    Task<IEnumerable<Question>> GetAllWithRepliesAsync(int page, int pageSize, string? searchTerm = null);

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
    /// <param name="title">Question title</param>
    /// <param name="content">Question content</param>
    /// <param name="authorId">Author user ID</param>
    /// <param name="assignedPosition">Position of the assigned member</param>
    /// <param name="assignedMemberId">Assigned member user ID</param>
    /// <param name="baseUrl">Base URL for notification links (e.g., from Navigation.BaseUri)</param>
    Task<Question> CreateAsync(string title, string content, string authorId, Position assignedPosition, string assignedMemberId, string baseUrl);

    /// <summary>
    /// Adds a reply to a question
    /// </summary>
    /// <param name="questionId">Question ID</param>
    /// <param name="content">Reply content</param>
    /// <param name="authorId">Author user ID</param>
    /// <param name="baseUrl">Base URL for notification links (e.g., from Navigation.BaseUri)</param>
    Task<QuestionReply> AddReplyAsync(int questionId, string content, string authorId, string baseUrl);

    /// <summary>
    /// Deletes a question (soft delete). Only the author can delete their own questions.
    /// </summary>
    Task<bool> DeleteAsync(int questionId, string requestingUserId);

    /// <summary>
    /// Closes a question. Only the author can close their own questions.
    /// </summary>
    Task<bool> CloseAsync(int questionId, string requestingUserId);

    /// <summary>
    /// Sends a manual notification reminder for a question
    /// </summary>
    /// <param name="questionId">Question ID</param>
    /// <param name="requestingUserId">Requesting user ID</param>
    /// <param name="baseUrl">Base URL for notification links (e.g., from Navigation.BaseUri)</param>
    Task SendManualReminderAsync(int questionId, string requestingUserId, string baseUrl);

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

    /// <summary>
    /// Gets all members with Órgãos Sociais positions for the single dropdown
    /// Returns tuple of (member, group, position)
    /// </summary>
    Task<IEnumerable<(ApplicationUser Member, OrgaoSocialGroup Group, Position Position)>> GetAllOrgaoSocialMembersAsync();
}
