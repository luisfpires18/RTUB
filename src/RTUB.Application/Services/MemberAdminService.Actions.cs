// Member admin: instruments, nickname, expel, reminder and delete.
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
    public async Task<EventResult<IReadOnlyList<MemberInstrumentDto>>> AddInstrumentAsync(string id, string? instrument, ClaimsPrincipal user)
    {
        if (await InfoMemberAsync<IReadOnlyList<MemberInstrumentDto>>(id, user) is { Refused: { } refused })
        {
            return refused;
        }

        if (!Enum.TryParse<InstrumentType>(instrument, out var type) || !Enum.IsDefined(type))
        {
            return EventResult<IReadOnlyList<MemberInstrumentDto>>.Invalid("instrument", "Instrumento inválido.");
        }

        var current = (await _instruments.GetMemberInstrumentsAsync(id)).ToList();
        if (current.Any(i => i.InstrumentType == type))
        {
            return EventResult<IReadOnlyList<MemberInstrumentDto>>.Invalid("instrument", "Este instrumento já está adicionado.");
        }

        try
        {
            await _instruments.AddInstrumentAsync(id, type, current.Count == 0);
        }
        catch (InvalidOperationException)
        {
            return EventResult<IReadOnlyList<MemberInstrumentDto>>.Invalid("instrument", "Este instrumento já está adicionado.");
        }

        return EventResult<IReadOnlyList<MemberInstrumentDto>>.Ok(await InstrumentsAsync(id));
    }

    public async Task<EventResult<IReadOnlyList<MemberInstrumentDto>>> RemoveInstrumentAsync(string id, int instrumentId, ClaimsPrincipal user)
    {
        if (await InfoMemberAsync<IReadOnlyList<MemberInstrumentDto>>(id, user) is { Refused: { } refused })
        {
            return refused;
        }

        var current = (await _instruments.GetMemberInstrumentsAsync(id)).ToList();
        if (current.FirstOrDefault(i => i.Id == instrumentId) is not { } removed)
        {
            return EventResult<IReadOnlyList<MemberInstrumentDto>>.Fail(EventResultStatus.NotFound);
        }

        await _instruments.RemoveInstrumentAsync(instrumentId);
        if (removed.IsPrimary && current.FirstOrDefault(i => i.Id != instrumentId) is { } next)
        {
            await _instruments.SetPrimaryInstrumentAsync(next.Id, id);
        }

        return EventResult<IReadOnlyList<MemberInstrumentDto>>.Ok(await InstrumentsAsync(id));
    }

    public async Task<EventResult<IReadOnlyList<MemberInstrumentDto>>> SetPrimaryInstrumentAsync(string id, int instrumentId, ClaimsPrincipal user)
    {
        if (await InfoMemberAsync<IReadOnlyList<MemberInstrumentDto>>(id, user) is { Refused: { } refused })
        {
            return refused;
        }

        if ((await _instruments.GetMemberInstrumentsAsync(id)).All(i => i.Id != instrumentId))
        {
            return EventResult<IReadOnlyList<MemberInstrumentDto>>.Fail(EventResultStatus.NotFound);
        }

        await _instruments.SetPrimaryInstrumentAsync(instrumentId, id);
        return EventResult<IReadOnlyList<MemberInstrumentDto>>.Ok(await InstrumentsAsync(id));
    }

    public async Task<EventResult<bool>> SetNicknameAsync(string id, MemberNicknameInput input, ClaimsPrincipal user)
    {
        var (member, refused) = await MemberAsync<bool>(id, user);
        if (refused is not null)
        {
            return refused;
        }

        if (!member.IsLeitao())
        {
            return EventResult<bool>.Fail(EventResultStatus.Forbidden);
        }

        var form = new NicknameFormModel { Nickname = input.Nickname };
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(form, new ValidationContext(form), results, validateAllProperties: true))
        {
            return EventResult<bool>.Invalid("nickname", results[0].ErrorMessage!);
        }

        var oldUsername = member.UserName ?? "";
        var newUsername = UsernameHelper.NormalizeUsername(input.Nickname!);
        if (await _users.FindByNameAsync(newUsername) is { } taken && taken.Id != member.Id)
        {
            return EventResult<bool>.Invalid("nickname", $"Já existe um utilizador com o username '{newUsername}'.");
        }

        member.Nickname = input.Nickname;
        member.UserName = newUsername;
        var updated = await _users.UpdateAsync(member);
        if (!updated.Succeeded)
        {
            return EventResult<bool>.Invalid("nickname", updated.Errors.First().Description);
        }

        try
        {
            await _email.SendUsernameChangedEmailAsync(member.Email ?? "", $"{member.FirstName} {member.LastName}", member.Nickname!,
                oldUsername, newUsername);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The nickname of {UserName} changed but the email failed", newUsername);
        }

        return EventResult<bool>.Ok(true);
    }

    public async Task<EventResult<bool>> SetExpelledAsync(string id, bool expelled, ClaimsPrincipal user)
    {
        var (member, refused) = await MemberAsync<bool>(id, user);
        if (refused is not null)
        {
            return refused;
        }

        // The old page offered expel / reactivate on Leitões only.
        if (!member.IsLeitao())
        {
            return EventResult<bool>.Fail(EventResultStatus.Forbidden);
        }

        member.IsExpelled = expelled;
        var updated = await _users.UpdateAsync(member);
        if (!updated.Succeeded)
        {
            return EventResult<bool>.Invalid("member", updated.Errors.First().Description);
        }

        _logger.LogInformation("Member {UserName} was {Action} by an admin", member.UserName, expelled ? "expelled" : "reactivated");
        return EventResult<bool>.Ok(true);
    }

    public async Task<EventResult<bool>> SendReminderAsync(string id, ClaimsPrincipal user)
    {
        var (member, refused) = await MemberAsync<bool>(id, user);
        if (refused is not null)
        {
            return refused;
        }

        // Only to a retired member two months into the three that bring them back, with nothing this month yet.
        if (!InActiveList(member) || !MemberDirectoryService.Encourage(await StatusAsync(id)))
        {
            return EventResult<bool>.Fail(EventResultStatus.Forbidden);
        }

        await _push.SendToUserAsync(id, _pushFactory.CreateMemberActivityReminderNotification(member.UserName, id));
        _logger.LogInformation("Sent a reminder push to member {UserName}", member.UserName);
        return EventResult<bool>.Ok(true);
    }

    public async Task<EventResult<bool>> DeleteAsync(string id, ClaimsPrincipal user)
    {
        var (member, refused) = await MemberAsync<bool>(id, user);
        if (refused is not null)
        {
            return refused;
        }

        if (!member.IsLeitao() && !MembersAuthorization.CanDeleteAnyMember(user))
        {
            return EventResult<bool>.Fail(EventResultStatus.Forbidden);
        }

        return await _profiles.DeleteMemberWithRelatedDataAsync(id)
            ? EventResult<bool>.Ok(true)
            : EventResult<bool>.Invalid("member", "Não foi possível eliminar o membro.");
    }
}
