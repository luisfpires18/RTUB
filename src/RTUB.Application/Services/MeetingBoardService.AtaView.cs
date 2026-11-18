using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

// Meeting board: ata view and editor models.
public sealed partial class MeetingBoardService
{
    // ---------- atas: view and editor ----------

    private async Task<MeetingAtaViewDto> AtaViewAsync(MeetingAccess access, int ataId)
    {
        var ata = await _atas.GetByIdWithDetailsAsync(ataId) ?? throw new InvalidOperationException($"Ata {ataId} not found.");
        var meeting = ata.Meeting;
        var published = ata.Status == MeetingAtaStatus.Published;
        var present = meeting.Participations.Where(p => p.WillAttend && p.User != null).ToList();

        IReadOnlyList<string> confirmed = Array.Empty<string>();
        MeetingAtaMyConfirmationDto? mine = null;
        if (published)
        {
            confirmed = (await _confirmations.GetConfirmationsByAtaIdAsync(ata.Id))
                .Where(c => c.IsConfirmed == true && c.User != null)
                .Select(c => FullName(c.User))
                .ToList();

            if (present.Any(p => p.UserId == access.UserId))
            {
                var own = await _confirmations.GetUserConfirmationAsync(ata.Id, access.UserId);
                mine = new MeetingAtaMyConfirmationDto(own?.IsConfirmed, own?.Notes);
            }
        }

        var isCouncil = meeting.Type == MeetingType.ConselhoVeteranos;
        return new MeetingAtaViewDto(
            meeting.Id,
            published ? "published" : "draft",
            Kind(meeting.Type),
            TypeLabel(meeting.Type),
            ata.AtaNumber,
            Format(meeting.Date),
            ata.Location,
            ata.PresidentUser is null ? null : FullName(ata.PresidentUser),
            ata.FirstSecretaryUser is null ? null : FullName(ata.FirstSecretaryUser),
            isCouncil || ata.SecondSecretaryUser is null ? null : FullName(ata.SecondSecretaryUser),
            present.Select(p => FullName(p.User!)).ToList(),
            confirmed,
            mine,
            ata.AgendaPoints.OrderBy(p => p.PointNumber).Select(PointDto).ToList(),
            ata.ClosingText);
    }

    private sealed record AtaSetup(
        string PresidentId,
        string? PresidentName,
        string? DefaultFirstSecretaryId,
        string? DefaultSecondSecretaryId,
        IReadOnlyList<MeetingPersonOptionDto> FirstOptions,
        IReadOnlyList<MeetingPersonOptionDto> SecondOptions,
        IReadOnlyList<string> Present);

    /// <summary>
    /// The ata form's people: the president is the current holder of the position (CV: Presidente do CV; AG:
    /// Presidente da Mesa), else whoever is saved (a new ata: the writer). CV secretaries come from those who said
    /// "Vou"; AG secretaries are the Mesa's current holders. A secretary already saved stays selectable.
    /// </summary>
    private async Task<AtaSetup> AtaSetupAsync(MeetingAccess access, Meeting meeting, MeetingAta? ata)
    {
        var users = await UsersAsync();
        var participants = (await _participations.GetParticipationsByMeetingIdAsync(meeting.Id))
            .Where(p => p.WillAttend && p.User != null)
            .ToList();
        var isAssembly = MeetingAccess.IsAssembly(meeting.Type);
        var isCouncil = meeting.Type == MeetingType.ConselhoVeteranos;

        var holder = isCouncil ? Holder(users, Position.PresidenteConselhoVeteranos)
            : isAssembly ? Holder(users, Position.PresidenteMesaAssembleia)
            : null;
        var firstHolder = isAssembly ? Holder(users, Position.PrimeiroSecretarioMesaAssembleia) : null;
        var secondHolder = isAssembly ? Holder(users, Position.SegundoSecretarioMesaAssembleia) : null;
        var presidentId = holder?.Id ?? ata?.PresidentUserId ?? access.UserId;

        var firstOptions = new List<MeetingPersonOptionDto>();
        var secondOptions = new List<MeetingPersonOptionDto>();
        if (isCouncil)
        {
            firstOptions.AddRange(participants.Select(p => new MeetingPersonOptionDto(p.UserId, p.User!.Nickname ?? p.User.FirstName ?? string.Empty)));
        }
        else if (isAssembly)
        {
            if (firstHolder is not null)
            {
                firstOptions.Add(new MeetingPersonOptionDto(firstHolder.Id, $"{firstHolder.Nickname ?? firstHolder.FirstName} (Cargo atual)"));
            }

            if (secondHolder is not null)
            {
                secondOptions.Add(new MeetingPersonOptionDto(secondHolder.Id, $"{secondHolder.Nickname ?? secondHolder.FirstName} (Cargo atual)"));
            }
        }

        KeepSaved(firstOptions, ata?.FirstSecretaryUserId, users);
        KeepSaved(secondOptions, ata?.SecondSecretaryUserId, users);

        return new AtaSetup(
            presidentId,
            users.FirstOrDefault(u => u.Id == presidentId) is { } president ? president.Nickname ?? president.FirstName : null,
            ata is null ? (isAssembly ? firstHolder?.Id : null) : ata.FirstSecretaryUserId,
            ata is null ? (isAssembly ? secondHolder?.Id : null) : ata.SecondSecretaryUserId,
            firstOptions,
            secondOptions,
            participants.Select(p => FullName(p.User!)).ToList());
    }

    private static void KeepSaved(List<MeetingPersonOptionDto> options, string? savedId, List<ApplicationUser> users)
    {
        if (string.IsNullOrEmpty(savedId) || options.Any(o => o.Id == savedId))
        {
            return;
        }

        var saved = users.FirstOrDefault(u => u.Id == savedId);
        options.Insert(0, new MeetingPersonOptionDto(savedId, saved is null ? "(membro removido)" : saved.Nickname ?? saved.FirstName ?? string.Empty));
    }

    private async Task<MeetingAtaEditorDto> AtaEditorAsync(MeetingAccess access, Meeting meeting, MeetingAta? stored)
    {
        var ata = stored is null ? null : await _atas.GetByIdWithDetailsAsync(stored.Id);
        var setup = await AtaSetupAsync(access, meeting, ata);
        return new MeetingAtaEditorDto(
            ata?.Id,
            ata?.Status == MeetingAtaStatus.Published ? "published" : "draft",
            Kind(meeting.Type),
            TypeLabel(meeting.Type),
            meeting.Title,
            Format(meeting.Date),
            ata?.AtaNumber,
            Format(ata?.ActualStartTime ?? meeting.Date),
            ata?.ActualEndTime is { } end ? Format(end) : null,
            ata?.Location ?? meeting.Location ?? string.Empty,
            ata?.QuorumBasis ?? QuorumBases[0],
            setup.PresidentName,
            setup.DefaultFirstSecretaryId,
            setup.DefaultSecondSecretaryId,
            setup.FirstOptions,
            setup.SecondOptions,
            setup.Present,
            ata?.AgendaPoints.OrderBy(p => p.PointNumber).Select(PointDto).ToList() ?? new List<MeetingAgendaPointDto>(),
            ata?.ClosingText,
            ata?.GeneratedAt is { } generated ? Format(generated) : null);
    }

    private static List<MeetingAtaAgendaPoint> ReadAgendaPoints(IReadOnlyList<MeetingAgendaPointInput>? input, Errors errors)
    {
        var points = new List<MeetingAtaAgendaPoint>();
        if (input is null)
        {
            return points;
        }

        if (input.Count > MaxAgendaPoints)
        {
            errors.Add("agendaPoints", $"No máximo {MaxAgendaPoints} pontos.");
            return points;
        }

        for (var i = 0; i < input.Count; i++)
        {
            var point = input[i];
            var key = $"agendaPoints[{i}]";
            var title = point.Title?.Trim();
            if (string.IsNullOrEmpty(title))
            {
                errors.Add(key, "Cada ponto precisa de um título.");
                continue;
            }

            if (title.Length > MaxAgendaTitleLength
                || point.Discussion?.Length > MaxAgendaTextLength
                || point.Decision?.Length > MaxAgendaTextLength)
            {
                errors.Add(key, $"Título até {MaxAgendaTitleLength} caracteres; discussão e deliberação até {MaxAgendaTextLength}.");
                continue;
            }

            if (!ValidVotes(point.VotesFor) || !ValidVotes(point.VotesAgainst) || !ValidVotes(point.VotesAbstain))
            {
                errors.Add(key, "Os votos têm de ser números entre 0 e 100000.");
                continue;
            }

            var result = NullIfBlank(point.Result);
            if (result is not null && !VoteResults.Contains(result))
            {
                errors.Add(key, "O resultado é Aprovado ou Rejeitado.");
                continue;
            }

            points.Add(new MeetingAtaAgendaPoint
            {
                PointNumber = i + 1,
                Title = title,
                DiscussionSummary = NullIfBlank(point.Discussion),
                DecisionText = NullIfBlank(point.Decision),
                VotesFor = point.VotesFor,
                VotesAgainst = point.VotesAgainst,
                VotesAbstain = point.VotesAbstain,
                VoteResult = result,
            });
        }

        return points;
    }

    private static bool ValidVotes(int? votes) => votes is null or (>= 0 and <= MaxVotes);

    private static MeetingAgendaPointDto PointDto(MeetingAtaAgendaPoint p) =>
        new(p.PointNumber, p.Title, p.DiscussionSummary, p.DecisionText, p.VotesFor, p.VotesAgainst, p.VotesAbstain, p.VoteResult);

    private static string AtaFileName(Meeting meeting) => $"Ata_{meeting.Type}_{meeting.Date:yyyyMMdd}.pdf";
}
