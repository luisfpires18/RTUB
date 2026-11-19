using System.Security.Claims;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

// Meeting board: atas (read, editor, save).
public sealed partial class MeetingBoardService
{
    // ---------- atas ----------

    public async Task<EventResult<MeetingAtaViewDto>> GetAtaAsync(int id, ClaimsPrincipal user)
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

        return ReadableAta(access, context) is { } ata
            ? EventResult<MeetingAtaViewDto>.Ok(await AtaViewAsync(access, ata.Id))
            : EventResult<MeetingAtaViewDto>.Fail(EventResultStatus.NotFound);
    }

    public async Task<EventResult<MeetingAtaEditorDto>> GetAtaEditorAsync(int id, ClaimsPrincipal user)
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

        return context.CanWrite
            ? EventResult<MeetingAtaEditorDto>.Ok(await AtaEditorAsync(access, context.Meeting, context.Existing))
            : EventResult<MeetingAtaEditorDto>.Fail(EventResultStatus.Forbidden);
    }

    public async Task<EventResult<MeetingAtaEditorDto>> SaveAtaAsync(int id, MeetingAtaInput input, ClaimsPrincipal user)
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

        if (context.Existing?.Status == MeetingAtaStatus.Published)
        {
            return EventResult<MeetingAtaEditorDto>.Fail(EventResultStatus.Closed);
        }

        var meeting = context.Meeting;
        var existing = context.Existing is null ? null : await _atas.GetByIdWithDetailsAsync(context.Existing.Id);
        var setup = await AtaSetupAsync(access, meeting, existing);
        var errors = new Errors();

        var ataNumber = NullIfBlank(input.AtaNumber)?.Trim();
        if (ataNumber?.Length > MaxAtaNumberLength)
        {
            errors.Add("ataNumber", $"O número da Ata não pode exceder {MaxAtaNumberLength} caracteres.");
        }

        if (!TryDate(input.ActualStartTime, out var start))
        {
            errors.Add("actualStartTime", "A hora de início efetiva é obrigatória.");
        }

        DateTime? end = null;
        if (!string.IsNullOrWhiteSpace(input.ActualEndTime))
        {
            if (TryDate(input.ActualEndTime, out var parsedEnd))
            {
                end = parsedEnd;
            }
            else
            {
                errors.Add("actualEndTime", "Indique uma hora de término válida.");
            }
        }

        var location = input.Location?.Trim();
        if (string.IsNullOrEmpty(location))
        {
            errors.Add("location", "O local é obrigatório.");
        }
        else if (location.Length > MaxLocationLength)
        {
            errors.Add("location", $"O local não pode exceder {MaxLocationLength} caracteres.");
        }

        var quorumBasis = existing?.QuorumBasis ?? QuorumBases[0];
        if (MeetingAccess.IsAssembly(meeting.Type))
        {
            if (input.QuorumBasis is not null && QuorumBases.Contains(input.QuorumBasis))
            {
                quorumBasis = input.QuorumBasis;
            }
            else
            {
                errors.Add("quorumBasis", "Escolha a base do quórum.");
            }
        }

        var firstSecretary = NullIfBlank(input.FirstSecretaryId);
        if (firstSecretary is not null && setup.FirstOptions.All(o => o.Id != firstSecretary))
        {
            errors.Add("firstSecretaryId", "Escolha um secretário da lista.");
        }

        var secondSecretary = NullIfBlank(input.SecondSecretaryId);
        if (secondSecretary is not null && setup.SecondOptions.All(o => o.Id != secondSecretary))
        {
            errors.Add("secondSecretaryId", "Escolha um secretário da lista.");
        }

        var closingText = NullIfBlank(input.ClosingText);
        if (closingText?.Length > MaxClosingTextLength)
        {
            errors.Add("closingText", $"O texto de encerramento não pode exceder {MaxClosingTextLength} caracteres.");
        }

        var points = ReadAgendaPoints(input.AgendaPoints, errors);

        if (errors.Any)
        {
            return Invalid<MeetingAtaEditorDto>(errors);
        }

        try
        {
            if (existing is null)
            {
                await _atas.CreateAtaAsync(new MeetingAta
                {
                    MeetingId = meeting.Id,
                    AtaNumber = ataNumber,
                    ActualStartTime = start,
                    ActualEndTime = end,
                    Location = location!,
                    PresidentUserId = setup.PresidentId,
                    FirstSecretaryUserId = firstSecretary,
                    SecondSecretaryUserId = secondSecretary,
                    QuorumBasis = quorumBasis,
                    AttendeesPresent = "[]",
                    AttendeesAbsent = "[]",
                    Status = MeetingAtaStatus.Draft,
                    ClosingText = closingText,
                    AgendaPoints = points,
                });
            }
            else
            {
                // Only the form's fields change; status, PDF data and the attendance JSON stay as stored.
                await _atas.UpdateAtaAsync(new MeetingAta
                {
                    Id = existing.Id,
                    MeetingId = meeting.Id,
                    AtaNumber = ataNumber,
                    ActualStartTime = start,
                    ActualEndTime = end,
                    Location = location!,
                    PresidentUserId = setup.PresidentId,
                    FirstSecretaryUserId = firstSecretary,
                    SecondSecretaryUserId = secondSecretary,
                    QuorumBasis = quorumBasis,
                    AttendeesPresent = existing.AttendeesPresent,
                    AttendeesAbsent = existing.AttendeesAbsent,
                    Status = existing.Status,
                    ClosingText = closingText,
                    GeneratedAt = existing.GeneratedAt,
                    PdfStorageUrl = existing.PdfStorageUrl,
                    UpdatedAt = existing.UpdatedAt,
                    UpdatedBy = existing.UpdatedBy,
                    AgendaPoints = points,
                });
            }
        }
        catch (InvalidOperationException)
        {
            // Created or published by someone else in the meantime.
            return EventResult<MeetingAtaEditorDto>.Fail(EventResultStatus.Closed);
        }

        return EventResult<MeetingAtaEditorDto>.Ok(await AtaEditorAsync(access, meeting, await _atas.GetByMeetingIdAsync(meeting.Id)));
    }
}
