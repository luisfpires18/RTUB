using System.ComponentModel.DataAnnotations;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Extracted verbatim from the handler of the retired Blazor /request page (React track 003): the
/// entity's data annotations (what that page's EditForm enforced), the date rules of
/// <see cref="IRequestValidationService"/>, then create, optional date range and the RTUB email.
/// </summary>
public class PublicRequestService : IPublicRequestService
{
    private readonly IRequestService _requestService;
    private readonly IRequestValidationService _validationService;
    private readonly IEmailNotificationService _emailNotificationService;

    public PublicRequestService(
        IRequestService requestService,
        IRequestValidationService validationService,
        IEmailNotificationService emailNotificationService)
    {
        _requestService = requestService;
        _validationService = validationService;
        _emailNotificationService = emailNotificationService;
    }

    public async Task<IReadOnlyDictionary<string, string[]>> SubmitAsync(PublicRequestSubmission submission)
    {
        var request = new Request
        {
            Name = submission.Name ?? string.Empty,
            Email = submission.Email ?? string.Empty,
            Phone = submission.Phone ?? string.Empty,
            EventType = submission.EventType ?? string.Empty,
            PreferredDate = submission.PreferredDate,
            IsDateRange = submission.IsDateRange,
            PreferredEndDate = submission.PreferredEndDate,
            Location = submission.Location ?? string.Empty,
            // Requests.Message is NOT NULL; the page always sent "" for "no message".
            Message = submission.Message ?? string.Empty
        };

        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return errors;
        }

        var created = await _requestService.CreateRequestAsync(
            request.Name, request.Email, request.Phone, request.EventType,
            request.PreferredDate, request.Location, request.Message);

        // As before: an end date is stored whenever one was supplied.
        if (request.PreferredEndDate.HasValue)
        {
            await _requestService.SetRequestDateRangeAsync(created.Id, request.PreferredEndDate.Value);
        }

        await _emailNotificationService.SendNewRequestNotificationAsync(
            created.Id, request.Name, request.Email, request.Phone, request.EventType,
            request.PreferredDate, request.PreferredEndDate, request.Location, request.Message,
            created.CreatedAt);

        return errors;
    }

    private Dictionary<string, string[]> Validate(Request request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);

        var errors = results
            .SelectMany(r => r.MemberNames.Select(member => (member, message: r.ErrorMessage ?? string.Empty)))
            .GroupBy(e => e.member)
            .ToDictionary(g => g.Key, g => g.Select(e => e.message).ToArray());

        // Every error in one answer: field annotations and date rules together.
        var (dateError, endDateError) = _validationService.ValidateRequestDates(request);
        if (dateError.Length > 0)
        {
            errors[nameof(Request.PreferredDate)] = new[] { dateError };
        }

        if (endDateError.Length > 0)
        {
            errors[nameof(Request.PreferredEndDate)] = new[] { endDateError };
        }

        return errors;
    }
}
