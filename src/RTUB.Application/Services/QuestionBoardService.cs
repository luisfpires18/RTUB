using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// The React /questions
/// (docs/react-requests-questions.md). Data through <see cref="IQuestionService"/> (which sends
/// the push notifications), rules enforced server-side (<see cref="QuestionsAuthorization"/>):
/// - the lists: open and closed questions, 10 a page by default, by latest activity, searched over title, content and
///   both members' names, filtered by recipient;
/// - ask: a title (1-100) and a question (10-5000), to a member who holds the chosen Órgãos Sociais position now;
/// - reply (1-5000), taking turns, never on a closed question; close, delete (soft) and remind: the author only.
/// No schema change.
/// </summary>
public sealed partial class QuestionBoardService : IQuestionBoardService
{
    public const int MaxTitleLength = 100;
    public const int MinContentLength = 10;
    public const int MaxContentLength = 5000;
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 50;

    private readonly IQuestionService _questions;
    private readonly IDbContextFactory<ApplicationDbContext> _contexts;

    public QuestionBoardService(IQuestionService questions, IDbContextFactory<ApplicationDbContext> contexts)
    {
        _questions = questions;
        _contexts = contexts;
    }

    public async Task<EventResult<QuestionPageDto>> GetPageAsync(bool closed, string? search, string? recipientId, int page, int pageSize, ClaimsPrincipal user)
    {
        if (QuestionsAuthorization.UserId(user) is not { } me)
        {
            return EventResult<QuestionPageDto>.Fail(EventResultStatus.SignInRequired);
        }

        page = Math.Max(1, page);
        pageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
        var q = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var recipient = string.IsNullOrWhiteSpace(recipientId) ? null : recipientId.Trim();

        var items = await _questions.GetAllWithRepliesAsync(page, pageSize, q, closed, recipient);
        var total = await _questions.GetCountAsync(q, closed, recipient);

        return EventResult<QuestionPageDto>.Ok(new QuestionPageDto(items.Select(x => Summary(x, me)).ToList(), total, page, pageSize));
    }

    public async Task<EventResult<IReadOnlyList<QuestionRecipientOptionDto>>> GetRecipientsAsync(ClaimsPrincipal user)
    {
        if (!QuestionsAuthorization.IsMember(user))
        {
            return EventResult<IReadOnlyList<QuestionRecipientOptionDto>>.Fail(EventResultStatus.SignInRequired);
        }

        return EventResult<IReadOnlyList<QuestionRecipientOptionDto>>.Ok(await RecipientsAsync());
    }

    public async Task<EventResult<QuestionDetailDto>> GetAsync(int id, ClaimsPrincipal user)
    {
        if (QuestionsAuthorization.UserId(user) is not { } me)
        {
            return EventResult<QuestionDetailDto>.Fail(EventResultStatus.SignInRequired);
        }

        return await DetailAsync(id, me);
    }

    public async Task<EventResult<QuestionDetailDto>> AskAsync(QuestionInput input, ClaimsPrincipal user)
    {
        if (QuestionsAuthorization.UserId(user) is not { } me)
        {
            return EventResult<QuestionDetailDto>.Fail(EventResultStatus.SignInRequired);
        }

        var title = input.Title?.Trim() ?? "";
        var content = input.Content?.Trim() ?? "";
        var errors = new Dictionary<string, string[]>();
        if (title.Length == 0)
        {
            errors["title"] = new[] { "Escreva um título." };
        }
        else if (title.Length > MaxTitleLength)
        {
            errors["title"] = new[] { $"O título tem no máximo {MaxTitleLength} caracteres." };
        }

        if (content.Length < MinContentLength)
        {
            errors["content"] = new[] { $"A pergunta tem pelo menos {MinContentLength} caracteres." };
        }
        else if (content.Length > MaxContentLength)
        {
            errors["content"] = new[] { $"A pergunta tem no máximo {MaxContentLength} caracteres." };
        }

        // The recipient must hold the chosen position now.
        var recipients = await RecipientsAsync();
        var chosen = recipients.FirstOrDefault(r => r.Id == input.RecipientId && r.Position == input.Position);
        if (chosen is null || !Enum.TryParse<Position>(chosen.Position, out var position))
        {
            errors["recipientId"] = new[] { "Escolha um membro dos Órgãos Sociais." };
            position = default;
        }

        if (errors.Count > 0)
        {
            return new EventResult<QuestionDetailDto>(EventResultStatus.Invalid, Errors: errors);
        }

        var created = await _questions.CreateAsync(title, content, me, position, chosen!.Id);
        return await DetailAsync(created.Id, me);
    }

    public async Task<EventResult<QuestionDetailDto>> ReplyAsync(int id, QuestionReplyInput input, ClaimsPrincipal user)
    {
        if (QuestionsAuthorization.UserId(user) is not { } me)
        {
            return EventResult<QuestionDetailDto>.Fail(EventResultStatus.SignInRequired);
        }

        if (await _questions.GetByIdAsync(id) is not { } question)
        {
            return EventResult<QuestionDetailDto>.Fail(EventResultStatus.NotFound);
        }

        if (question.Status == QuestionStatus.Closed)
        {
            return EventResult<QuestionDetailDto>.Invalid("content", "Esta pergunta foi fechada; já não aceita respostas.");
        }

        if (!QuestionsAuthorization.CanReply(question, me))
        {
            return EventResult<QuestionDetailDto>.Fail(EventResultStatus.Forbidden);
        }

        var content = input.Content?.Trim() ?? "";
        if (content.Length == 0)
        {
            return EventResult<QuestionDetailDto>.Invalid("content", "Escreva uma resposta.");
        }

        if (content.Length > MaxContentLength)
        {
            return EventResult<QuestionDetailDto>.Invalid("content", $"A resposta tem no máximo {MaxContentLength} caracteres.");
        }

        await _questions.AddReplyAsync(id, content, me);
        return await DetailAsync(id, me);
    }

    public async Task<EventResult<QuestionDetailDto>> CloseAsync(int id, ClaimsPrincipal user)
    {
        if (QuestionsAuthorization.UserId(user) is not { } me)
        {
            return EventResult<QuestionDetailDto>.Fail(EventResultStatus.SignInRequired);
        }

        if (await _questions.GetByIdAsync(id) is not { } question)
        {
            return EventResult<QuestionDetailDto>.Fail(EventResultStatus.NotFound);
        }

        if (!QuestionsAuthorization.IsAuthor(question, me))
        {
            return EventResult<QuestionDetailDto>.Fail(EventResultStatus.Forbidden);
        }

        if (question.Status != QuestionStatus.Closed)
        {
            await _questions.CloseAsync(id, me);
        }

        return await DetailAsync(id, me);
    }
}
