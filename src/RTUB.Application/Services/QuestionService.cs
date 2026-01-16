using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// Service implementation for Question operations
/// </summary>
public class QuestionService : IQuestionService
{
    private readonly IQuestionRepository _questionRepository;
    private readonly IQuestionReplyRepository _replyRepository;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IPushNotificationFactory _pushNotificationFactory;
    private readonly WebPushOptions _webPushOptions;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<QuestionService> _logger;

    public QuestionService(
        IQuestionRepository questionRepository,
        IQuestionReplyRepository replyRepository,
        IPushNotificationService pushNotificationService,
        IPushNotificationFactory pushNotificationFactory,
        IOptions<WebPushOptions> webPushOptions,
        UserManager<ApplicationUser> userManager,
        ILogger<QuestionService> logger)
    {
        _questionRepository = questionRepository;
        _replyRepository = replyRepository;
        _pushNotificationService = pushNotificationService;
        _pushNotificationFactory = pushNotificationFactory;
        _webPushOptions = webPushOptions.Value;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<IEnumerable<Question>> GetAllAsync(int page, int pageSize, string? searchTerm = null)
    {
        return await _questionRepository.GetAllAsync(page, pageSize, searchTerm);
    }

    public async Task<IEnumerable<Question>> GetAllWithRepliesAsync(int page, int pageSize, string? searchTerm = null)
    {
        return await _questionRepository.GetAllWithRepliesAsync(page, pageSize, searchTerm);
    }

    public async Task<int> GetCountAsync(string? searchTerm = null)
    {
        return await _questionRepository.GetCountAsync(searchTerm);
    }

    public async Task<Question?> GetByIdWithRepliesAsync(int id)
    {
        return await _questionRepository.GetByIdWithRepliesAsync(id);
    }

    public async Task<Question?> GetByIdAsync(int id)
    {
        return await _questionRepository.GetByIdAsync(id);
    }

    public async Task<Question> CreateAsync(string title, string content, string authorId, Position assignedPosition, string assignedMemberId, string baseUrl)
    {
        // Load users first so audit log can resolve their nicknames
        var author = await _userManager.FindByIdAsync(authorId);
        var assignedMember = await _userManager.FindByIdAsync(assignedMemberId);
        var authorName = author?.Nickname ?? author?.UserName ?? "Membro";

        var question = Question.Create(title, content, authorId, assignedPosition, assignedMemberId);
        
        // Set navigation properties for audit log display name resolution
        if (author != null) question.Author = author;
        if (assignedMember != null) question.AssignedMember = assignedMember;
        
        await _questionRepository.AddAsync(question);

        // Send notification to assigned member using factory with absolute URL
        var effectiveBaseUrl = GetEffectiveBaseUrl(baseUrl);
        var notification = _pushNotificationFactory.CreateNewQuestionNotification(title, authorName, question.Id, effectiveBaseUrl);

        await _pushNotificationService.SendToUserAsync(assignedMemberId, notification);
        question.UpdateLastNotificationSent();
        await _questionRepository.UpdateAsync(question);

        var assignedMemberName = assignedMember?.Nickname ?? assignedMember?.UserName ?? "Membro";
        _logger.LogInformation(
            "Question '{QuestionTitle}' created by {AuthorName} assigned to {AssignedMemberName}",
            question.Title, authorName, assignedMemberName);

        return question;
    }

    public async Task<QuestionReply> AddReplyAsync(int questionId, string content, string authorId, string baseUrl)
    {
        var question = await _questionRepository.GetByIdAsync(questionId);
        if (question == null)
        {
            throw new InvalidOperationException($"Question {questionId} not found");
        }

        var isFromAssignedMember = authorId == question.AssignedMemberId;
        var reply = QuestionReply.Create(questionId, content, authorId, isFromAssignedMember);
        await _replyRepository.AddAsync(reply);

        var effectiveBaseUrl = GetEffectiveBaseUrl(baseUrl);

        // Update question status based on who replied
        if (isFromAssignedMember)
        {
            question.MarkAsAnswered();
            var member = await _userManager.FindByIdAsync(authorId);
            var memberName = member?.Nickname ?? member?.UserName ?? "Membro";
            _logger.LogInformation(
                "Question '{QuestionTitle}' answered by assigned member {MemberName}",
                question.Title, memberName);

            // Notify the question author
            var notification = _pushNotificationFactory.CreateQuestionAnsweredNotification(content, reply.Id, effectiveBaseUrl);
            await _pushNotificationService.SendToUserAsync(question.AuthorId, notification);
        }
        else if (authorId == question.AuthorId)
        {
            question.MarkAsInDiscussion();
            
            // Notify the assigned member
            var author = await _userManager.FindByIdAsync(authorId);
            var authorName = author?.Nickname ?? author?.UserName ?? "Membro";
            
            _logger.LogInformation(
                "Question '{QuestionTitle}' user {AuthorName} replied, now in discussion",
                question.Title, authorName);

            // Build the reply preview for the notification body
            var replyPreview = $"{authorName} respondeu à sua resposta: {content}";
            var notification = _pushNotificationFactory.CreateQuestionReplyNotification(replyPreview, reply.Id, effectiveBaseUrl);
            await _pushNotificationService.SendToUserAsync(question.AssignedMemberId, notification);
        }

        await _questionRepository.UpdateAsync(question);
        return reply;
    }

    public async Task<bool> DeleteAsync(int questionId, string requestingUserId)
    {
        var question = await _questionRepository.GetByIdAsync(questionId);
        if (question == null)
        {
            return false;
        }

        // Only the author can delete their own questions
        if (question.AuthorId != requestingUserId)
        {
            var requestingUser = await _userManager.FindByIdAsync(requestingUserId);
            var requestingUserName = requestingUser?.Nickname ?? requestingUser?.UserName ?? "Unknown";
            var ownerUser = await _userManager.FindByIdAsync(question.AuthorId);
            var ownerName = ownerUser?.Nickname ?? ownerUser?.UserName ?? "Unknown";
            _logger.LogWarning(
                "User {UserName} attempted to delete question '{QuestionTitle}' owned by {OwnerName}",
                requestingUserName, question.Title, ownerName);
            return false;
        }

        var author = await _userManager.FindByIdAsync(requestingUserId);
        var authorName = author?.Nickname ?? author?.UserName ?? "Unknown";
        await _questionRepository.SoftDeleteAsync(questionId);
        _logger.LogInformation(
            "Question '{QuestionTitle}' deleted by author {AuthorName}",
            question.Title, authorName);
        return true;
    }

    public async Task SendManualReminderAsync(int questionId, string requestingUserId, string baseUrl)
    {
        var question = await _questionRepository.GetByIdAsync(questionId);
        if (question == null)
        {
            throw new InvalidOperationException($"Question {questionId} not found");
        }

        // Only the author can send manual reminders
        if (question.AuthorId != requestingUserId)
        {
            throw new UnauthorizedAccessException("Only the question author can send reminders");
        }

        var author = await _userManager.FindByIdAsync(requestingUserId);
        var authorName = author?.Nickname ?? author?.UserName ?? "Membro";

        var effectiveBaseUrl = GetEffectiveBaseUrl(baseUrl);
        var notification = _pushNotificationFactory.CreateQuestionReminderNotification(question.Title, authorName, questionId, effectiveBaseUrl);

        await _pushNotificationService.SendToUserAsync(question.AssignedMemberId, notification);
        question.UpdateLastNotificationSent();
        await _questionRepository.UpdateAsync(question);

        _logger.LogInformation(
            "Manual reminder sent for question '{QuestionTitle}' by author {AuthorName}",
            question.Title, authorName);
    }

    public async Task<bool> CanAnswerAsync(int questionId, string userId)
    {
        var question = await _questionRepository.GetByIdAsync(questionId);
        if (question == null)
        {
            return false;
        }

        return question.AssignedMemberId == userId;
    }

    public bool HasOrgaoSocialPosition(ApplicationUser user)
    {
        if (user.Positions == null || !user.Positions.Any())
        {
            return false;
        }

        var orgaoSocialPositions = OrgaoSocialHelper.GetAllOrgaoSocialPositions().ToHashSet();
        return user.Positions.Any(p => orgaoSocialPositions.Contains(p));
    }

    public async Task<IEnumerable<ApplicationUser>> GetMembersWithPositionAsync(Position position)
    {
        var allUsers = await _userManager.Users
            .AsNoTracking()
            .ToListAsync();

        return allUsers.Where(u => u.Positions != null && u.Positions.Contains(position));
    }

    public async Task<bool> CloseAsync(int questionId, string requestingUserId)
    {
        var question = await _questionRepository.GetByIdAsync(questionId);
        if (question == null)
        {
            return false;
        }

        // Only the author can close their own questions
        if (question.AuthorId != requestingUserId)
        {
            var requestingUser = await _userManager.FindByIdAsync(requestingUserId);
            var requestingUserName = requestingUser?.Nickname ?? requestingUser?.UserName ?? "Unknown";
            var ownerUser = await _userManager.FindByIdAsync(question.AuthorId);
            var ownerName = ownerUser?.Nickname ?? ownerUser?.UserName ?? "Unknown";
            _logger.LogWarning(
                "User {UserName} attempted to close question '{QuestionTitle}' owned by {OwnerName}",
                requestingUserName, question.Title, ownerName);
            return false;
        }

        var author = await _userManager.FindByIdAsync(requestingUserId);
        var authorName = author?.Nickname ?? author?.UserName ?? "Unknown";
        question.Close();
        await _questionRepository.UpdateAsync(question);
        _logger.LogInformation(
            "Question '{QuestionTitle}' closed by author {AuthorName}",
            question.Title, authorName);
        return true;
    }

    public async Task<IEnumerable<(ApplicationUser Member, OrgaoSocialGroup Group, Position Position)>> GetAllOrgaoSocialMembersAsync()
    {
        var allUsers = await _userManager.Users
            .AsNoTracking()
            .ToListAsync();

        var result = new List<(ApplicationUser Member, OrgaoSocialGroup Group, Position Position)>();

        // Iterate through users once and check their positions
        foreach (var user in allUsers)
        {
            if (user.Positions == null || !user.Positions.Any())
                continue;

            foreach (var position in user.Positions)
            {
                var group = OrgaoSocialHelper.GetGroupForPosition(position);
                if (group.HasValue)
                {
                    result.Add((user, group.Value, position));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the effective base URL for building absolute notification URLs.
    /// Uses the passed baseUrl (from Navigation.BaseUri) if provided, otherwise falls back to config.
    /// </summary>
    private string GetEffectiveBaseUrl(string? passedBaseUrl)
    {
        // Prefer the passed base URL (from Navigation.BaseUri in Blazor)
        if (!string.IsNullOrWhiteSpace(passedBaseUrl))
        {
            return passedBaseUrl.TrimEnd('/');
        }

        // Fall back to configured base URL (for background service scenarios)
        return _webPushOptions.GetEffectiveBaseUrl();
    }
}
