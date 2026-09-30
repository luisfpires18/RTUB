using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// The one submission path for public performance requests (React /portal/request via
/// POST /api/public/requests): validation, persistence, the admin push and the email to RTUB.
/// </summary>
public interface IPublicRequestService
{
    /// <summary>
    /// Validates and, if valid, submits the request. Returns validation errors keyed by
    /// <see cref="Core.Entities.Request"/> property name; an empty result means it was submitted.
    /// Failures after validation (storage, email) are thrown, exactly as before the extraction.
    /// </summary>
    Task<IReadOnlyDictionary<string, string[]>> SubmitAsync(PublicRequestSubmission submission);
}
