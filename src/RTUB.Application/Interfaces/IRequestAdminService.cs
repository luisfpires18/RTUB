using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// The React /requests ("Gestão de Pedidos", task 031; was the Blazor Requests.razor): the performance requests sent
/// through the public /request form, for the members, and their answer (Admin, Owner).
/// </summary>
public interface IRequestAdminService
{
    /// <summary>
    /// <paramref name="fiscalYear"/>: null = the current year, "" = every year, or one of the listed years.
    /// <paramref name="status"/>: "" (all), "pending", "confirmed" or "rejected".
    /// </summary>
    Task<EventResult<RequestsDto>> GetAsync(string? fiscalYear, string? search, string? status, ClaimsPrincipal user);

    Task<EventResult<RequestApprovedDto>> ApproveAsync(int id, ClaimsPrincipal user);

    Task<EventResult<RequestItemDto>> RejectAsync(int id, ClaimsPrincipal user);

    Task<EventResult<bool>> DeleteAsync(int id, ClaimsPrincipal user);
}
