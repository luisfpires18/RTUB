using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

// Meeting board: atas (publish, PDF, confirm).
public sealed partial class MeetingBoardService
{
    public async Task<EventResult<MeetingAtaEditorDto>> PublishAtaAsync(int id, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingAtaEditorDto>.Fail(refusal);
        }

        var (context, closed) = await AtaContextAsync(access, id);
        if (context is null)
        {
            return EventResult<MeetingAtaEditorDto>.Fail(closed);
        }

        if (!context.CanWrite)
        {
            return EventResult<MeetingAtaEditorDto>.Fail(EventResultStatus.Forbidden);
        }

        // Only a saved draft is published (the old "Publicar" appeared once the ata had been saved).
        if (context.Existing is null || context.Existing.Status == MeetingAtaStatus.Published)
        {
            return EventResult<MeetingAtaEditorDto>.Fail(EventResultStatus.Closed);
        }

        var meeting = context.Meeting;
        var details = await _atas.GetByIdWithDetailsAsync(context.Existing.Id);
        if (details is null)
        {
            return EventResult<MeetingAtaEditorDto>.Fail(EventResultStatus.NotFound);
        }

        details.Meeting ??= meeting;
        var pdf = _pdf.GenerateAtaPdf(details);

        // As before: the PDF goes to Documentation in the current fiscal year's folder; a storage failure is logged and
        // does not stop the publication.
        var fiscalYear = FiscalYearHelper.GetCurrentFiscalYearString();
        var folderName = meeting.Type == MeetingType.ConselhoVeteranos ? $"Atas CV {fiscalYear}"
            : MeetingAccess.IsAssembly(meeting.Type) ? $"Atas AG {fiscalYear}"
            : $"Atas {fiscalYear}";
        var folderPath = $"docs/{_environment.EnvironmentName}/{fiscalYear}/{folderName}/";
        try
        {
            await _documents.CreateFolderAsync(folderPath);
            using var stream = new MemoryStream(pdf);
            await _documents.UploadDocumentAsync(folderPath, AtaFileName(meeting), stream, "application/pdf");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create ATA folder or upload PDF to {FolderPath}, but ATA was published", folderPath);
        }

        _trees.Forget(folderPath); // the ata folder is shown in Documentation (#226 F01), even half written

        try
        {
            await _atas.PublishAtaAsync(context.Existing.Id);
        }
        catch (InvalidOperationException)
        {
            return EventResult<MeetingAtaEditorDto>.Fail(EventResultStatus.Closed);
        }

        return EventResult<MeetingAtaEditorDto>.Ok(await AtaEditorAsync(access, meeting, await _atas.GetByMeetingIdAsync(meeting.Id)));
    }

    public async Task<EventResult<MeetingAtaPdf>> GetAtaPdfAsync(int id, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingAtaPdf>.Fail(refusal);
        }

        var (context, closed) = await AtaContextAsync(access, id);
        if (context is null)
        {
            return EventResult<MeetingAtaPdf>.Fail(closed);
        }

        // Writers download their draft from the editor; everyone else only a published ata they may read (A1).
        var ata = context.CanWrite ? context.Existing : ReadableAta(access, context);
        if (ata is null)
        {
            return EventResult<MeetingAtaPdf>.Fail(EventResultStatus.NotFound);
        }

        var details = await _atas.GetByIdWithDetailsAsync(ata.Id);
        if (details is null)
        {
            return EventResult<MeetingAtaPdf>.Fail(EventResultStatus.NotFound);
        }

        details.Meeting ??= context.Meeting;
        return EventResult<MeetingAtaPdf>.Ok(new MeetingAtaPdf(_pdf.GenerateAtaPdf(details), AtaFileName(details.Meeting)));
    }

    public async Task<EventResult<MeetingAtaViewDto>> ConfirmAtaAsync(int id, MeetingAtaConfirmationInput input, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingAtaViewDto>.Fail(refusal);
        }

        var (context, closed) = await AtaContextAsync(access, id);
        if (context is null)
        {
            return EventResult<MeetingAtaViewDto>.Fail(closed);
        }

        var ata = ReadableAta(access, context);
        if (ata is null)
        {
            return EventResult<MeetingAtaViewDto>.Fail(EventResultStatus.NotFound);
        }

        if (ata.Status != MeetingAtaStatus.Published)
        {
            return EventResult<MeetingAtaViewDto>.Fail(EventResultStatus.Closed);
        }

        // Only someone who attended ("Vou") confirms or refuses, as the old view and the confirmation service required.
        var participation = await _participations.GetParticipationByMeetingAndUserAsync(context.Meeting.Id, access.UserId);
        if (participation?.WillAttend != true)
        {
            return EventResult<MeetingAtaViewDto>.Fail(EventResultStatus.Forbidden);
        }

        if (input.Confirm)
        {
            await _confirmations.ConfirmAtaAsync(ata.Id, access.UserId);
        }
        else
        {
            await _confirmations.RefuseAtaAsync(ata.Id, access.UserId);
        }

        return EventResult<MeetingAtaViewDto>.Ok(await AtaViewAsync(access, ata.Id));
    }
}
