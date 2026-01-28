using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for validating request dates
/// Extracted from Request.razor to improve separation of concerns
/// </summary>
public interface IRequestValidationService
{
    /// <summary>
    /// Validates request dates and returns error messages if invalid
    /// </summary>
    /// <param name="request">The request to validate</param>
    /// <returns>Tuple containing (dateValidationError, endDateValidationError). Both will be empty if validation passes.</returns>
    (string dateValidationError, string endDateValidationError) ValidateRequestDates(Request request);
}
