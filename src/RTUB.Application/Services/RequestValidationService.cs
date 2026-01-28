using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service for validating request dates
/// Extracted from Request.razor to improve separation of concerns
/// </summary>
public class RequestValidationService : IRequestValidationService
{
    /// <summary>
    /// Validates request dates and returns error messages if invalid
    /// </summary>
    /// <param name="request">The request to validate</param>
    /// <returns>Tuple containing (dateValidationError, endDateValidationError). Both will be empty if validation passes.</returns>
    public (string dateValidationError, string endDateValidationError) ValidateRequestDates(Request request)
    {
        string dateValidationError = string.Empty;
        string endDateValidationError = string.Empty;

        // Validate dates
        if (request.PreferredDate.Date < DateTime.Today)
        {
            dateValidationError = "A data não pode ser no passado.";
            return (dateValidationError, endDateValidationError);
        }

        if (request.IsDateRange)
        {
            if (request.PreferredEndDate == null)
            {
                endDateValidationError = "A data de fim é obrigatória para intervalo de datas.";
                return (dateValidationError, endDateValidationError);
            }

            if (request.PreferredEndDate.Value.Date < DateTime.Today)
            {
                endDateValidationError = "A data de fim não pode ser no passado.";
                return (dateValidationError, endDateValidationError);
            }

            if (request.PreferredEndDate.Value.Date < request.PreferredDate.Date)
            {
                endDateValidationError = "A data de fim deve ser posterior ou igual à data de início.";
                return (dateValidationError, endDateValidationError);
            }
        }

        return (dateValidationError, endDateValidationError);
    }
}
