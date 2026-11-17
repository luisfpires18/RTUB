// Member admin: edit form, mentor search, create and update.
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

public sealed partial class MemberAdminService
{
    public async Task<EventResult<MemberEditDto>> GetForEditAsync(string id, ClaimsPrincipal user)
    {
        if (Refusal<MemberEditDto>(user) is { } refused)
        {
            return refused;
        }

        return await _users.FindByIdAsync(id) is { } member
            ? EventResult<MemberEditDto>.Ok(await EditDtoAsync(member, user))
            : EventResult<MemberEditDto>.Fail(EventResultStatus.NotFound);
    }

    public async Task<EventResult<IReadOnlyList<MentorCandidateDto>>> SearchMentorsAsync(string? query, string? excludeId, ClaimsPrincipal user)
    {
        if (InfoRefusal<IReadOnlyList<MentorCandidateDto>>(user) is { } refused)
        {
            return refused;
        }

        // As the old padrinho search: Tunos and above, by first name, matched on name or nickname, the member edited left out.
        var eligible = (await AllUsersAsync()).Where(u => u.CanBeMentor()).OrderBy(u => u.FirstName).ThenBy(u => u.LastName);
        return EventResult<IReadOnlyList<MentorCandidateDto>>.Ok(_mentors.FilterMentors(eligible, query?.Trim() ?? "", excludeId)
            .Take(MentorLimit)
            .Select(u =>
            {
                var who = GovernanceService.ToMember(u.Nickname, u.FirstName, u.LastName, u.ImageUrl);
                return new MentorCandidateDto(u.Id, who.DisplayName, who.FullName, who.AvatarUrl);
            })
            .ToList());
    }

    public async Task<EventResult<MemberCreatedDto>> CreateAsync(MemberInput input, ClaimsPrincipal user)
    {
        // Hotfix 098: Mod creates members too; the account is always a plain Member, so nothing escalates.
        if (InfoRefusal<MemberCreatedDto>(user) is { } refused)
        {
            return refused;
        }

        var noNickname = input.NoNickname && input.Category == "Leitao";
        // "Sem alcunha": the nickname follows the email's local part.
        var nickname = noNickname && !string.IsNullOrEmpty(input.Email) ? input.Email.Split('@')[0] : input.Nickname;
        var errors = Validate(input with { Nickname = nickname });
        var instruments = new List<InstrumentType>();
        foreach (var i in input.Instruments ?? Array.Empty<MemberInstrumentInput>())
        {
            if (!Enum.TryParse<InstrumentType>(i.Instrument, out var type) || !Enum.IsDefined(type))
            {
                errors.TryAdd("instruments", new[] { "Instrumento inválido." });
            }
            else if (!instruments.Contains(type))
            {
                instruments.Add(type);
            }
        }

        if (!await MentorIsValidAsync(input))
        {
            errors.TryAdd("mentorId", new[] { "Escolha um padrinho da lista." });
        }

        if (errors.Count > 0)
        {
            return Invalid<MemberCreatedDto>(errors);
        }

        var member = new ApplicationUser
        {
            FirstName = input.FirstName,
            LastName = input.LastName,
            Nickname = nickname,
            PhoneNumber = input.PhoneNumber,
            Email = input.Email,
            City = input.City,
            Degree = input.Degree,
            DateOfBirth = input.DateOfBirth?.ToDateTime(TimeOnly.MinValue),
            UserName = UsernameHelper.NormalizeUsername(noNickname ? input.Email!.Split('@')[0] : nickname!),
            EmailConfirmed = true,
            YearLeitao = input.YearLeitao,
            MonthLeitao = input.MonthLeitao,
            YearCaloiro = input.YearCaloiro,
            MonthCaloiro = input.MonthCaloiro,
            YearTuno = input.YearTuno,
            MonthTuno = input.MonthTuno,
            MentorId = string.IsNullOrEmpty(input.MentorId) ? null : input.MentorId,
            Categories = Categories(input),
            RequirePasswordChange = true,
        };
        MemberActivityStatusEvaluator.ApplyInitialState(member, _time.GetUtcNow().UtcDateTime);
        ApplySpecialCategories(member, input);

        if (await _users.FindByNameAsync(member.UserName) is not null)
        {
            return EventResult<MemberCreatedDto>.Invalid("nickname",
                $"Já existe um utilizador com o nome de tuna '{member.Nickname}' (username: {member.UserName}).");
        }

        if (await _users.FindByEmailAsync(member.Email!) is not null)
        {
            return EventResult<MemberCreatedDto>.Invalid("email", $"Já existe um utilizador com o email '{member.Email}'.");
        }

        var password = PasswordGenerator.GeneratePassword();
        var created = await _users.CreateAsync(member, password);
        if (!created.Succeeded)
        {
            return Invalid<MemberCreatedDto>(IdentityErrors(created));
        }

        await _users.AddToRoleAsync(member, "Member");
        // The first instrument is the primary unless another was marked.
        var primary = (input.Instruments ?? Array.Empty<MemberInstrumentInput>())
            .FirstOrDefault(i => i.Primary && Enum.TryParse<InstrumentType>(i.Instrument, out _))?.Instrument;
        for (var n = 0; n < instruments.Count; n++)
        {
            var isPrimary = primary is null ? n == 0 : instruments[n].ToString() == primary;
            await _instruments.AddInstrumentAsync(member.Id, instruments[n], isPrimary);
        }

        try
        {
            await _email.SendWelcomeEmailAsync(member.UserName ?? "", member.Email ?? "", $"{member.FirstName} {member.LastName}",
                member.Nickname ?? "", password);
        }
        catch (Exception ex)
        {
            // The account exists either way; a retry would create it twice.
            _logger.LogError(ex, "Member {UserName} was created but the welcome email failed", member.UserName);
        }

        return EventResult<MemberCreatedDto>.Ok(new MemberCreatedDto(member.Id));
    }

    public async Task<EventResult<MemberEditDto>> UpdateAsync(string id, MemberInput input, ClaimsPrincipal user)
    {
        if (Refusal<MemberEditDto>(user) is { } refused)
        {
            return refused;
        }

        var member = await _users.FindByIdAsync(id);
        if (member is null)
        {
            return EventResult<MemberEditDto>.Fail(EventResultStatus.NotFound);
        }

        var errors = Validate(input);
        if (input.MentorId == id)
        {
            errors.TryAdd("mentorId", new[] { "Um utilizador não pode ser o seu próprio padrinho." });
        }
        else if (!await MentorIsValidAsync(input))
        {
            errors.TryAdd("mentorId", new[] { "Escolha um padrinho da lista." });
        }

        if (errors.Count > 0)
        {
            return Invalid<MemberEditDto>(errors);
        }

        member.FirstName = input.FirstName;
        member.LastName = input.LastName;
        member.Nickname = input.Nickname;
        member.PhoneNumber = input.PhoneNumber;
        member.Email = input.Email;
        member.City = input.City;
        member.DateOfBirth = input.DateOfBirth?.ToDateTime(TimeOnly.MinValue);
        member.Degree = input.Degree;

        // A complete date pair changes only for Owner or the current Magister; an incomplete one may be completed.
        var canEditDates = await CanEditDatesAsync(user);
        if (canEditDates || !Complete(member.YearLeitao, member.MonthLeitao))
        {
            (member.YearLeitao, member.MonthLeitao) = (input.YearLeitao, input.MonthLeitao);
        }

        if (canEditDates || !Complete(member.YearCaloiro, member.MonthCaloiro))
        {
            (member.YearCaloiro, member.MonthCaloiro) = (input.YearCaloiro, input.MonthCaloiro);
        }

        if (canEditDates || !Complete(member.YearTuno, member.MonthTuno))
        {
            (member.YearTuno, member.MonthTuno) = (input.YearTuno, input.MonthTuno);
        }

        member.MentorId = string.IsNullOrEmpty(input.MentorId) ? null : input.MentorId;
        ApplySpecialCategories(member, input);
        member.Categories = Categories(input);

        var updated = await _users.UpdateAsync(member);
        return updated.Succeeded
            ? EventResult<MemberEditDto>.Ok(await EditDtoAsync(member, user))
            : Invalid<MemberEditDto>(IdentityErrors(updated));
    }
}
