using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// The React /meetings (task 034, docs/react-meetings.md; was the Blazor page): meetings, participations, atas and
/// meeting requests. Every rule of the old page is decided here (<see cref="Helpers.MeetingAccess"/>), from the session:
/// visitors get <c>SignInRequired</c>, a Leitão <c>Forbidden</c> on everything, and a meeting the member does not see is
/// <c>NotFound</c>. Built on the existing meeting, participation, ata, request, PDF, storage, email and push services.
/// </summary>
public interface IMeetingBoardService
{
    // ---------- meetings ----------

    /// <summary><paramref name="fiscalYear"/>: "2025-2026", "all", or null for the current year (the old default).</summary>
    Task<EventResult<MeetingBoardDto>> GetBoardAsync(string? fiscalYear, string? search, ClaimsPrincipal user);

    Task<EventResult<MeetingCardDto>> GetMeetingAsync(int id, ClaimsPrincipal user);

    Task<EventResult<MeetingFormDto>> GetFormAsync(ClaimsPrincipal user);

    Task<EventResult<MeetingSavedDto>> CreateAsync(MeetingInput input, ClaimsPrincipal user);

    Task<EventResult<MeetingSavedDto>> UpdateAsync(int id, MeetingInput input, ClaimsPrincipal user);

    Task<EventResult<bool>> DeleteAsync(int id, ClaimsPrincipal user);

    Task<EventResult<MeetingCancelDraftDto>> GetCancelDraftAsync(int id, ClaimsPrincipal user);

    Task<EventResult<MeetingNoticeResultDto>> CancelAsync(int id, MeetingCancelInput input, ClaimsPrincipal user);

    Task<EventResult<MeetingCardDto>> UncancelAsync(int id, ClaimsPrincipal user);

    Task<EventResult<MeetingEmailDraftDto>> GetEmailDraftAsync(int id, ClaimsPrincipal user);

    Task<EventResult<MeetingEmailPreviewDto>> PreviewEmailAsync(int id, MeetingEmailPreviewInput input, ClaimsPrincipal user);

    Task<EventResult<MeetingNoticeResultDto>> SendEmailAsync(int id, MeetingEmailInput input, ClaimsPrincipal user);

    Task<EventResult<MeetingPushDraftDto>> GetPushDraftAsync(int id, ClaimsPrincipal user);

    Task<EventResult<MeetingNoticeResultDto>> SendPushAsync(int id, MeetingPushInput input, ClaimsPrincipal user, string baseUrl);

    // ---------- participations ----------

    /// <summary>The member's own "Vou" / "Não vou" (with a note) on an upcoming meeting.</summary>
    Task<EventResult<MeetingCardDto>> RespondAsync(int id, MeetingParticipationInput input, ClaimsPrincipal user);

    Task<EventResult<MeetingParticipantsDto>> GetParticipantsAsync(int id, ClaimsPrincipal user);

    Task<EventResult<MeetingParticipantsDto>> RemoveParticipationAsync(int id, int participationId, ClaimsPrincipal user);

    Task<EventResult<IReadOnlyList<MeetingCandidateDto>>> GetCandidatesAsync(int id, string? search, ClaimsPrincipal user);

    Task<EventResult<MeetingParticipantsDto>> AddParticipantAsync(int id, MeetingAddParticipantInput input, ClaimsPrincipal user);

    // ---------- atas ----------

    Task<EventResult<MeetingAtaViewDto>> GetAtaAsync(int id, ClaimsPrincipal user);

    Task<EventResult<MeetingAtaEditorDto>> GetAtaEditorAsync(int id, ClaimsPrincipal user);

    Task<EventResult<MeetingAtaEditorDto>> SaveAtaAsync(int id, MeetingAtaInput input, ClaimsPrincipal user);

    /// <summary>Publishes a saved draft: the PDF goes to Documentation (as before), then the ata is published.</summary>
    Task<EventResult<MeetingAtaEditorDto>> PublishAtaAsync(int id, ClaimsPrincipal user);

    Task<EventResult<MeetingAtaPdf>> GetAtaPdfAsync(int id, ClaimsPrincipal user);

    Task<EventResult<MeetingAtaViewDto>> ConfirmAtaAsync(int id, MeetingAtaConfirmationInput input, ClaimsPrincipal user);

    // ---------- meeting requests ----------

    Task<EventResult<MeetingRequestPageDto>> GetRequestsAsync(string? status, string? fiscalYear, int? page, int? pageSize, ClaimsPrincipal user);

    Task<EventResult<int>> ProposeAsync(MeetingRequestInput input, ClaimsPrincipal user);

    Task<EventResult<MeetingDraftDto>> AcceptRequestAsync(int requestId, ClaimsPrincipal user);

    Task<EventResult<bool>> RejectRequestAsync(int requestId, ClaimsPrincipal user);

    Task<EventResult<bool>> DeleteRequestAsync(int requestId, ClaimsPrincipal user);

    Task<EventResult<bool>> RemindRequestAsync(int requestId, ClaimsPrincipal user, string baseUrl);
}
