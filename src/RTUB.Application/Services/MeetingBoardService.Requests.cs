using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

// Meeting board: meeting requests.
public sealed partial class MeetingBoardService
{
    // ---------- meeting requests ----------

    public async Task<EventResult<MeetingRequestPageDto>> GetRequestsAsync(
        string? status, string? fiscalYear, int? page, int? pageSize, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingRequestPageDto>.Fail(refusal);
        }

        if (!access.CanSeeRequests)
        {
            return EventResult<MeetingRequestPageDto>.Fail(EventResultStatus.Forbidden);
        }

        RequestStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<RequestStatus>(status, ignoreCase: false, out var parsed) || !RequestFilters.Contains(parsed) || int.TryParse(status, out _))
            {
                return EventResult<MeetingRequestPageDto>.Invalid("status", "Escolha um estado da lista.");
            }

            filter = parsed;
        }

        if (!TryFiscalYear(fiscalYear, out _, out var range))
        {
            return EventResult<MeetingRequestPageDto>.Invalid("fy", "Escolha um ano da lista.");
        }

        var size = pageSize is { } requested && RequestPageSizes.Contains(requested) ? requested : RequestPageSizes[0];
        var number = page is > 0 ? page.Value : 1;

        IEnumerable<MeetingRequest> requests = await _requests.GetPagedAsync(1, int.MaxValue, filter);
        if (range is { } r)
        {
            requests = requests.Where(x => x.ProposedDateTime >= r.Start && x.ProposedDateTime <= r.End);
        }

        var all = requests.OrderByDescending(x => x.ProposedDateTime).ToList();
        var items = all.Skip((number - 1) * size).Take(size).Select(x => RequestDto(access, x)).ToList();
        return EventResult<MeetingRequestPageDto>.Ok(new MeetingRequestPageDto(items, all.Count, number, size));
    }

    public async Task<EventResult<int>> ProposeAsync(MeetingRequestInput input, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<int>.Fail(refusal);
        }

        if (!TryType(input.Type, out var type))
        {
            return EventResult<int>.Invalid("type", "Escolha o tipo de reunião.");
        }

        // The page proposed CV, Direção and (as "Assembleia Geral") AGE; nothing proposed an AGO.
        var allowed = type switch
        {
            MeetingType.ConselhoVeteranos => access.CanProposeCv,
            MeetingType.ReuniaoDirecao => access.CanProposeDirecao,
            MeetingType.AssembleiaGeralExtraordinaria => access.CanProposeAg,
            _ => false,
        };
        if (!allowed)
        {
            return EventResult<int>.Fail(EventResultStatus.Forbidden);
        }

        var errors = new Errors();
        var title = RequiredText(input.Title, "title", "O título é obrigatório.", MaxTitleLength, "O título não pode exceder 200 caracteres.", errors);
        if (!TryDate(input.ProposedDate, out var proposed))
        {
            errors.Add("proposedDate", "A data proposta é obrigatória.");
        }

        var location = OptionalText(input.Location, "location", MaxLocationLength, "A localização não pode exceder 200 caracteres.", errors);
        var description = input.Description ?? string.Empty;
        if (string.IsNullOrWhiteSpace(description))
        {
            errors.Add("description", "A descrição é obrigatória.");
        }
        else if (description.Length > MaxDescriptionLength)
        {
            errors.Add("description", $"A descrição não pode exceder {MaxDescriptionLength} caracteres.");
        }

        if (errors.Any)
        {
            return Invalid<int>(errors);
        }

        // The old service push to Owners and the relevant president comes with it.
        var created = await _requests.CreateAsync(new MeetingRequest
        {
            RequestedMeetingType = type,
            Title = title!,
            ProposedDateTime = proposed,
            Location = location,
            Description = description,
            AuthorUserId = access.UserId,
            Status = RequestStatus.Pending,
        });
        return EventResult<int>.Ok(created.Id);
    }

    public async Task<EventResult<MeetingDraftDto>> AcceptRequestAsync(int requestId, ClaimsPrincipal user)
    {
        var (request, refusal) = await DecidableRequestAsync(requestId, user);
        if (request is null)
        {
            return EventResult<MeetingDraftDto>.Fail(refusal);
        }

        await _requests.UpdateStatusAsync(request.Id, RequestStatus.Confirmed);

        // The create form then opens prefilled with the request, as before.
        return EventResult<MeetingDraftDto>.Ok(new MeetingDraftDto(
            request.RequestedMeetingType.ToString(),
            request.Title,
            Format(request.ProposedDateTime),
            request.Location,
            request.Description));
    }

    public async Task<EventResult<bool>> RejectRequestAsync(int requestId, ClaimsPrincipal user)
    {
        var (request, refusal) = await DecidableRequestAsync(requestId, user);
        if (request is null)
        {
            return EventResult<bool>.Fail(refusal);
        }

        // The old service pushes the rejection to the author.
        await _requests.UpdateStatusAsync(request.Id, RequestStatus.Rejected);
        return EventResult<bool>.Ok(true);
    }

    public async Task<EventResult<bool>> DeleteRequestAsync(int requestId, ClaimsPrincipal user)
    {
        var (request, refusal) = await OwnRequestAsync(requestId, user);
        if (request is null)
        {
            return EventResult<bool>.Fail(refusal);
        }

        await _requests.DeleteAsync(request.Id);
        return EventResult<bool>.Ok(true);
    }

    public async Task<EventResult<bool>> RemindRequestAsync(int requestId, ClaimsPrincipal user)
    {
        var (request, refusal) = await OwnRequestAsync(requestId, user);
        if (request is null)
        {
            return EventResult<bool>.Fail(refusal);
        }

        if (request.Status != RequestStatus.Pending)
        {
            return EventResult<bool>.Fail(EventResultStatus.Closed);
        }

        return await _requests.SendReminderNotificationAsync(request.Id)
            ? EventResult<bool>.Ok(true)
            : EventResult<bool>.Invalid("notice", "Não foi possível enviar o lembrete.");
    }
}
