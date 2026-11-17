// My profile: read, personal and tuna data, mentor search, rank.
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

public sealed partial class MyProfileService
{
    public async Task<EventResult<MyProfileDto>> GetAsync(ClaimsPrincipal user)
    {
        var (me, refused) = await MeAsync<MyProfileDto>(user);
        return refused ?? await ProfileAsync(me, user);
    }

    public async Task<EventResult<MyProfileDto>> UpdatePersonalAsync(MyPersonalInput input, ClaimsPrincipal user)
    {
        var (me, refused) = await MeAsync<MyProfileDto>(user);
        if (refused is not null)
        {
            return refused;
        }

        var locked = me.IsOnlyLeitao();
        var nickname = locked ? me.Nickname : Clean(input.Nickname);
        var email = Clean(input.Email);
        var errors = Validate(new ApplicationUser
        {
            FirstName = Clean(input.FirstName),
            LastName = Clean(input.LastName),
            Nickname = nickname,
            Email = email,
            PhoneNumber = Clean(input.PhoneNumber),
            City = Clean(input.City),
        });

        if (locked)
        {
            // The nickname is the one already there; a Leitão may have none yet.
            errors.Remove("nickname");
            if (!string.Equals(Clean(input.Nickname), Clean(me.Nickname), StringComparison.Ordinal))
            {
                errors["nickname"] = new[] { "O nome de tuna só pode ser definido após a passagem a Caloiro." };
            }
        }

        if (input.DateOfBirth is { } birth && (birth.Year < 1900 || birth > DateOnly.FromDateTime(DateTime.Today)))
        {
            errors.TryAdd("dateOfBirth", new[] { "Data de nascimento inválida." });
        }

        if (!errors.ContainsKey("email") && email is not null && await _users.FindByEmailAsync(email) is { } other && other.Id != me.Id)
        {
            errors["email"] = new[] { "Este email já está associado a outra conta." };
        }

        if (errors.Count > 0)
        {
            return Invalid<MyProfileDto>(errors);
        }

        me.FirstName = Clean(input.FirstName);
        me.LastName = Clean(input.LastName);
        me.Nickname = nickname;
        me.Email = email;
        me.PhoneNumber = Clean(input.PhoneNumber);
        me.DateOfBirth = input.DateOfBirth?.ToDateTime(TimeOnly.MinValue);
        me.City = Clean(input.City);
        me.Degree = Clean(input.Degree);

        var updated = await _users.UpdateAsync(me);
        return updated.Succeeded ? await ProfileAsync(me, user) : Invalid<MyProfileDto>(IdentityErrors(updated));
    }

    public async Task<EventResult<MyProfileDto>> UpdateTunaAsync(MyTunaInput input, ClaimsPrincipal user)
    {
        var (me, refused) = await MeAsync<MyProfileDto>(user);
        if (refused is not null)
        {
            return refused;
        }

        var errors = new Dictionary<string, string[]>();
        var rules = Rules(me);
        var mentorId = me.MentorId;
        if (rules.ShowMentor)
        {
            mentorId = string.IsNullOrWhiteSpace(input.MentorId) ? null : input.MentorId.Trim();
            if (mentorId == me.Id)
            {
                errors["mentorId"] = new[] { "Um utilizador não pode ser o seu próprio padrinho." };
            }
            // A kept padrinho is not re-checked; a new one must be a Tuno or above.
            else if (mentorId is not null && mentorId != me.MentorId
                && (await _users.FindByIdAsync(mentorId) is not { } mentor || !mentor.CanBeMentor()))
            {
                errors["mentorId"] = new[] { "Escolha um padrinho da lista (um Tuno)." };
            }
        }

        var dates = new[]
        {
            ("leitao", rules.ShowLeitao, me.YearLeitao, me.MonthLeitao, input.YearLeitao, input.MonthLeitao),
            ("caloiro", rules.ShowCaloiro, me.YearCaloiro, me.MonthCaloiro, input.YearCaloiro, input.MonthCaloiro),
            ("tuno", rules.ShowTuno, me.YearTuno, me.MonthTuno, input.YearTuno, input.MonthTuno),
        };
        foreach (var (key, shown, year, month, newYear, newMonth) in dates)
        {
            if (!shown || (year == newYear && month == newMonth))
            {
                continue;
            }

            if (Complete(year, month))
            {
                errors[key] = new[] { LockedDateMessage };
            }
            else if (newYear is < MemberAdminService.MinYear || newYear > DateTime.Now.Year || newMonth is < 1 or > 12)
            {
                errors[key] = new[] { $"Escolha um mês e um ano entre {MemberAdminService.MinYear} e {DateTime.Now.Year}." };
            }
        }

        if (errors.Count > 0)
        {
            return Invalid<MyProfileDto>(errors);
        }

        me.MentorId = mentorId;
        // Only a shown, still-open pair changes (a locked pair equals its input here, so this keeps it).
        if (rules.ShowLeitao)
        {
            (me.YearLeitao, me.MonthLeitao) = (input.YearLeitao, input.MonthLeitao);
        }

        if (rules.ShowCaloiro)
        {
            (me.YearCaloiro, me.MonthCaloiro) = (input.YearCaloiro, input.MonthCaloiro);
        }

        if (rules.ShowTuno)
        {
            (me.YearTuno, me.MonthTuno) = (input.YearTuno, input.MonthTuno);
        }

        var updated = await _users.UpdateAsync(me);
        return updated.Succeeded ? await ProfileAsync(me, user) : Invalid<MyProfileDto>(IdentityErrors(updated));
    }

    public async Task<EventResult<IReadOnlyList<MentorCandidateDto>>> SearchMentorsAsync(string? query, ClaimsPrincipal user)
    {
        var (me, refused) = await MeAsync<IReadOnlyList<MentorCandidateDto>>(user);
        if (refused is not null)
        {
            return refused;
        }

        List<ApplicationUser> users;
        await using (var db = await _contexts.CreateDbContextAsync())
        {
            users = await db.Users.AsNoTracking().ToListAsync();
        }

        // As the old padrinho search: Tunos and above by first name, matched on name or nickname, never yourself.
        var eligible = users.Where(u => u.CanBeMentor() && u.Id != me.Id).OrderBy(u => u.FirstName).ThenBy(u => u.LastName);
        return EventResult<IReadOnlyList<MentorCandidateDto>>.Ok(_mentors.FilterMentors(eligible, query?.Trim() ?? "", me.Id)
            .Take(MentorLimit)
            .Select(u =>
            {
                var who = GovernanceService.ToMember(u.Nickname, u.FirstName, u.LastName, u.ImageUrl);
                return new MentorCandidateDto(u.Id, who.DisplayName, who.FullName, who.AvatarUrl);
            })
            .ToList());
    }

    private sealed record TunaRules(bool ShowMentor, bool ShowLeitao, bool ShowCaloiro, bool ShowTuno);

    /// <summary>
    /// What the "Tuna" form shows and saves, as the old page: no dates for a Fundador; Caloiro once past Leitão; Tuno
    /// once Tuno; a padrinho except for a Leitão, a Fundador or (as the member admin already did) a Tuno Honorário.
    /// </summary>
    private static TunaRules Rules(ApplicationUser me)
    {
        var fundador = me.IsFundador();
        return new TunaRules(
            (!me.IsLeitao() || me.IsCaloiro() || me.IsTuno()) && !fundador && !me.IsTunoHonorario(),
            !fundador,
            !fundador && !me.IsOnlyLeitao(),
            !fundador && me.IsTunoOrHigher());
    }

    private async Task<EventResult<MyProfileDto>> ProfileAsync(ApplicationUser me, ClaimsPrincipal user)
    {
        var detail = await _directory.GetMemberAsync(me.Id, user);
        if (detail.Status != EventResultStatus.Ok || detail.Value is null)
        {
            return EventResult<MyProfileDto>.Fail(detail.Status == EventResultStatus.Ok ? EventResultStatus.NotFound : detail.Status);
        }

        string? mentorName = null;
        string? mentorAvatar = null;
        if (!string.IsNullOrEmpty(me.MentorId) && await _users.FindByIdAsync(me.MentorId) is { } mentor)
        {
            mentorName = mentor.GetDisplayName();
            mentorAvatar = GovernanceService.ToMember(mentor.Nickname, mentor.FirstName, mentor.LastName, mentor.ImageUrl).AvatarUrl;
        }

        Dictionary<string, string> pictures;
        await using (var db = await _contexts.CreateDbContextAsync())
        {
            pictures = (await db.NaipeTypeConfigs.AsNoTracking().Where(c => c.PictureUrl != null).ToListAsync())
                .DistinctBy(c => c.InstrumentType).ToDictionary(c => c.InstrumentType.ToString(), c => c.PictureUrl!);
        }

        var rules = Rules(me);
        var options = Enum.GetValues<InstrumentType>()
            .Select(i => new MemberOptionDto(i.ToString(), StatusHelper.GetInstrumentDisplay(i)))
            .ToList();

        return EventResult<MyProfileDto>.Ok(new MyProfileDto(
            detail.Value,
            new MyPersonalDto(me.FirstName, me.LastName, me.Nickname, me.IsOnlyLeitao(), me.Email, me.PhoneNumber,
                me.DateOfBirth?.ToString("yyyy-MM-dd"), me.City, me.Degree),
            new MyTunaDto(rules.ShowMentor, me.MentorId, mentorName, mentorAvatar, rules.ShowLeitao, rules.ShowCaloiro, rules.ShowTuno,
                me.YearLeitao, me.MonthLeitao, me.YearCaloiro, me.MonthCaloiro, me.YearTuno, me.MonthTuno,
                new MemberLockedDatesDto(Complete(me.YearLeitao, me.MonthLeitao), Complete(me.YearCaloiro, me.MonthCaloiro),
                    Complete(me.YearTuno, me.MonthTuno))),
            await InstrumentsAsync(me.Id),
            options,
            pictures,
            me.Subscribed,
            me.RequirePasswordChange,
            await RankAsync(me.Id)));
    }

    private async Task<MyRankDto> RankAsync(string id)
    {
        var rank = await _ranking.GetRankProgressAsync(id);
        var next = rank.IsMaxLevel ? null : _rankingConfig.Value.Levels.FirstOrDefault(l => l.Level == rank.CurrentLevel + 1)?.Name;
        return new MyRankDto(rank.CurrentLevel, rank.CurrentRankName, rank.CurrentXp, rank.IsMaxLevel, rank.XpToNextLevel,
            rank.XpInCurrentLevel, rank.XpNeededForNextLevel, Math.Round(rank.ProgressPercentage, 1), next);
    }
}
