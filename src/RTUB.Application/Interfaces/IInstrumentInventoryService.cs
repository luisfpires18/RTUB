using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>The React /inventory ("Instrumentos", React track 020; was the Blazor Inventory.razor).</summary>
public interface IInstrumentInventoryService
{
    Task<EventResult<InstrumentListDto>> GetAsync(InstrumentQuery query, ClaimsPrincipal user);

    Task<EventResult<InstrumentDetailDto>> GetByIdAsync(int id, ClaimsPrincipal user);

    Task<EventResult<InstrumentCreatedDto>> CreateAsync(InstrumentInput input, ClaimsPrincipal user);

    Task<EventResult<InstrumentDetailDto>> UpdateAsync(int id, InstrumentInput input, ClaimsPrincipal user);

    Task<EventResult<bool>> DeleteAsync(int id, ClaimsPrincipal user);

    /// <summary>The original image and its cropped thumbnail, both required, as the old form.</summary>
    Task<EventResult<InstrumentDetailDto>> SetImageAsync(int id, InstrumentImageUpload image, InstrumentImageUpload thumbnail, ClaimsPrincipal user);
}
