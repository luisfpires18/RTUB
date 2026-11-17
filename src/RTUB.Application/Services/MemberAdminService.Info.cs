// Member admin: the member info card (read and update).
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

public sealed partial class MemberAdminService
{
    // ---------- the Mod's limited edit (task 076) ----------

    public async Task<EventResult<MemberInfoDto>> GetInfoAsync(string id, ClaimsPrincipal user)
    {
        var (member, refused) = await InfoMemberAsync<MemberInfoDto>(id, user);
        return refused ?? EventResult<MemberInfoDto>.Ok(await InfoDtoAsync(member, user));
    }

    /// <summary>
    /// Cidade, Contacto, the Leitão / Caloiro / Tuno dates and the Padrinho - nothing else can be sent. The form's own checks
    /// (the entity's annotations, the month/year range, the padrinho list, nobody their own padrinho); a field that does
    /// not apply to the member must stay empty; a complete date pair only Owner or the Magister may change (refused here,
    /// not silently ignored); a save over a newer version (<c>Stamp</c>) is a Conflict. Audited by the context as any
    /// member change (actor and fields).
    /// </summary>
    public async Task<EventResult<MemberInfoDto>> UpdateInfoAsync(string id, MemberInfoInput input, ClaimsPrincipal user)
    {
        var (member, refused) = await InfoMemberAsync<MemberInfoDto>(id, user);
        if (refused is not null)
        {
            return refused;
        }

        if (string.IsNullOrEmpty(input.Stamp) || input.Stamp != member.ConcurrencyStamp)
        {
            return EventResult<MemberInfoDto>.Fail(EventResultStatus.Conflict);
        }

        var errors = new Dictionary<string, string[]>();
        var probe = new ApplicationUser { FirstName = "x", LastName = "x", Nickname = "x", PhoneNumber = input.PhoneNumber, City = input.City };
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(probe, new ValidationContext(probe), results, validateAllProperties: true);
        foreach (var result in results)
        {
            foreach (var name in result.MemberNames.Where(n => n is nameof(ApplicationUser.PhoneNumber) or nameof(ApplicationUser.City)))
            {
                errors.TryAdd(name == nameof(ApplicationUser.City) ? MemberInfoRules.City : MemberInfoRules.Phone, new[] { result.ErrorMessage! });
            }
        }

        var applicable = MemberInfoRules.Applicable(member.Categories);
        var canEditDates = await CanEditDatesAsync(user);
        foreach (var (key, year, month, storedYear, storedMonth) in new[]
                 {
                     (MemberInfoRules.Leitao, input.YearLeitao, input.MonthLeitao, member.YearLeitao, member.MonthLeitao),
                     (MemberInfoRules.Caloiro, input.YearCaloiro, input.MonthCaloiro, member.YearCaloiro, member.MonthCaloiro),
                     (MemberInfoRules.Tuno, input.YearTuno, input.MonthTuno, member.YearTuno, member.MonthTuno),
                 })
        {
            var changed = year != storedYear || month != storedMonth;
            if (!applicable.Contains(key) && changed)
            {
                errors.TryAdd(key, new[] { "Esta data não se aplica a este membro." });
            }
            else if (year is < MinYear || year > DateTime.Now.Year || month is < 1 or > 12)
            {
                errors.TryAdd(key, new[] { $"Escolha um mês e um ano entre {MinYear} e {DateTime.Now.Year}." });
            }
            else if (changed && Complete(storedYear, storedMonth) && !canEditDates)
            {
                // No system role in member-facing text (UI rule): the lock is explained, not who holds the key.
                errors.TryAdd(key, new[] { "Esta data está fechada: não a podes alterar aqui." });
            }
        }

        var mentorId = string.IsNullOrEmpty(input.MentorId) ? null : input.MentorId;
        if (mentorId != member.MentorId)
        {
            if (!applicable.Contains(MemberInfoRules.Mentor) && mentorId is not null)
            {
                errors.TryAdd(MemberInfoRules.Mentor, new[] { "Este membro não tem padrinho." });
            }
            else if (mentorId == member.Id)
            {
                errors.TryAdd(MemberInfoRules.Mentor, new[] { "Um utilizador não pode ser o seu próprio padrinho." });
            }
            else if (mentorId is not null && !(await _users.FindByIdAsync(mentorId) is { } mentor && mentor.CanBeMentor()))
            {
                errors.TryAdd(MemberInfoRules.Mentor, new[] { "Escolha um padrinho da lista." });
            }
        }

        if (errors.Count > 0)
        {
            return Invalid<MemberInfoDto>(errors);
        }

        member.City = string.IsNullOrWhiteSpace(input.City) ? null : input.City.Trim();
        member.PhoneNumber = string.IsNullOrWhiteSpace(input.PhoneNumber) ? null : input.PhoneNumber.Trim();
        (member.YearLeitao, member.MonthLeitao) = (input.YearLeitao, input.MonthLeitao);
        (member.YearCaloiro, member.MonthCaloiro) = (input.YearCaloiro, input.MonthCaloiro);
        (member.YearTuno, member.MonthTuno) = (input.YearTuno, input.MonthTuno);
        member.MentorId = mentorId;

        // Identity checks the stamp again on save: a change between the read above and here is a Conflict too.
        var updated = await _users.UpdateAsync(member);
        if (!updated.Succeeded)
        {
            return updated.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.ConcurrencyFailure))
                ? EventResult<MemberInfoDto>.Fail(EventResultStatus.Conflict)
                : Invalid<MemberInfoDto>(new Dictionary<string, string[]> { [MemberInfoRules.Phone] = updated.Errors.Select(e => e.Description).ToArray() });
        }

        return EventResult<MemberInfoDto>.Ok(await InfoDtoAsync(member, user));
    }

    private static EventResult<T>? InfoRefusal<T>(ClaimsPrincipal user) =>
        !MembersAuthorization.IsMember(user) ? EventResult<T>.Fail(EventResultStatus.SignInRequired)
        : !MembersAuthorization.CanEditInfo(user) ? EventResult<T>.Fail(EventResultStatus.Forbidden)
        : null;

    /// <summary>A member the limited edit covers (<see cref="MemberInfoRules.InScope"/>: not expelled, Leitão / Caloiro / Tuno).</summary>
    private async Task<(ApplicationUser Member, EventResult<T>? Refused)> InfoMemberAsync<T>(string id, ClaimsPrincipal user)
    {
        if (InfoRefusal<T>(user) is { } refused)
        {
            return (null!, refused);
        }

        return await _users.FindByIdAsync(id) is { } member && (MemberInfoRules.InScope(member) || MembersAuthorization.CanManage(user))
            ? (member, null)
            : (null!, EventResult<T>.Fail(EventResultStatus.NotFound));
    }

    private async Task<MemberInfoDto> InfoDtoAsync(ApplicationUser m, ClaimsPrincipal user)
    {
        string? mentorName = null;
        if (!string.IsNullOrEmpty(m.MentorId) && await _users.FindByIdAsync(m.MentorId) is { } mentor)
        {
            mentorName = mentor.GetDisplayName();
        }

        var instruments = await InstrumentsAsync(m.Id);
        var canEditDates = await CanEditDatesAsync(user);
        var who = GovernanceService.ToMember(m.Nickname, m.FirstName, m.LastName, null);
        return new MemberInfoDto(m.Id, who.DisplayName, m.City, m.PhoneNumber, m.YearLeitao, m.MonthLeitao, m.YearCaloiro, m.MonthCaloiro,
            m.YearTuno, m.MonthTuno, m.MentorId, mentorName, instruments,
            MemberInfoRules.Applicable(m.Categories), MemberInfoRules.Missing(m, instruments.Count > 0),
            new MemberLockedDatesDto(
                !canEditDates && Complete(m.YearLeitao, m.MonthLeitao),
                !canEditDates && Complete(m.YearCaloiro, m.MonthCaloiro),
                !canEditDates && Complete(m.YearTuno, m.MonthTuno)),
            m.ConcurrencyStamp ?? "");
    }
}
