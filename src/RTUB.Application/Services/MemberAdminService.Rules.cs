// Member admin: validation, permission and DTO helpers.
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

public sealed partial class MemberAdminService
{
    // ---------- helpers ----------

    private static EventResult<T>? Refusal<T>(ClaimsPrincipal user) =>
        !user.IsMember() ? EventResult<T>.Fail(EventResultStatus.SignInRequired)
        : !MembersAuthorization.CanManage(user) ? EventResult<T>.Fail(EventResultStatus.Forbidden)
        : null;

    private async Task<(ApplicationUser Member, EventResult<T>? Refused)> MemberAsync<T>(string id, ClaimsPrincipal user)
    {
        if (Refusal<T>(user) is { } refused)
        {
            return (null!, refused);
        }

        return await _users.FindByIdAsync(id) is { } member ? (member, null) : (null!, EventResult<T>.Fail(EventResultStatus.NotFound));
    }

    private static EventResult<T> Invalid<T>(Dictionary<string, string[]> errors) => new(EventResultStatus.Invalid, Errors: errors);

    /// <summary>The form's checks: the entity's own annotations, the category, and the month/year pickers' range.</summary>
    private static Dictionary<string, string[]> Validate(MemberInput input)
    {
        var errors = new Dictionary<string, string[]>();
        var probe = new ApplicationUser
        {
            FirstName = input.FirstName,
            LastName = input.LastName,
            Nickname = input.Nickname,
            PhoneNumber = input.PhoneNumber,
            Email = input.Email,
            City = input.City,
        };
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(probe, new ValidationContext(probe), results, validateAllProperties: true);
        foreach (var result in results)
        {
            foreach (var name in result.MemberNames)
            {
                errors.TryAdd(char.ToLowerInvariant(name[0]) + name[1..], new[] { result.ErrorMessage! });
            }
        }

        if (string.IsNullOrEmpty(input.Category) || !BaseCategories.ContainsKey(input.Category))
        {
            errors.TryAdd("category", new[] { "Categoria é obrigatória." });
        }

        if (input.Category == "Tuno" && input.Fundador && input.Honorario)
        {
            errors.TryAdd("category", new[] { "Escolha Fundador ou Tuno Honorário, não os dois." });
        }

        if (input.DateOfBirth is { } birth && (birth.Year < 1900 || birth > DateOnly.FromDateTime(DateTime.Today)))
        {
            errors.TryAdd("dateOfBirth", new[] { "Data de nascimento inválida." });
        }

        // Incomplete pairs exist in older data and stay allowed; only impossible values are refused.
        foreach (var (key, year, month) in new[] { ("leitao", input.YearLeitao, input.MonthLeitao),
                     ("caloiro", input.YearCaloiro, input.MonthCaloiro), ("tuno", input.YearTuno, input.MonthTuno) })
        {
            if (year is < MinYear || year > DateTime.Now.Year || month is < 1 or > 12)
            {
                errors.TryAdd(key, new[] { $"Escolha um mês e um ano entre {MinYear} e {DateTime.Now.Year}." });
            }
        }

        return errors;
    }

    /// <summary>A padrinho that is kept must exist and be Tuno or above.</summary>
    private async Task<bool> MentorIsValidAsync(MemberInput input)
    {
        if (string.IsNullOrEmpty(input.MentorId) || (input.Category == "Tuno" && (input.Fundador || input.Honorario)))
        {
            return true;
        }

        return await _users.FindByIdAsync(input.MentorId) is { } mentor && mentor.CanBeMentor();
    }

    private static List<MemberCategory> Categories(MemberInput input)
    {
        var category = BaseCategories.GetValueOrDefault(input.Category ?? "", MemberCategory.Leitao);
        var categories = new List<MemberCategory> { category };
        if (category == MemberCategory.Tuno && input.Fundador)
        {
            categories.Add(MemberCategory.Fundador);
        }

        if (category == MemberCategory.Tuno && input.Honorario)
        {
            categories.Add(MemberCategory.TunoHonorario);
        }

        return categories;
    }

    /// <summary>Fundador: Tuno since December 1991. Both Fundador and Tuno Honorário: no Leitão/Caloiro dates, no padrinho.</summary>
    private static void ApplySpecialCategories(ApplicationUser member, MemberInput input)
    {
        var fundador = input.Category == "Tuno" && input.Fundador;
        var honorario = input.Category == "Tuno" && input.Honorario;
        if (fundador)
        {
            (member.YearTuno, member.MonthTuno) = (1991, 12);
        }

        if (fundador || honorario)
        {
            (member.YearLeitao, member.MonthLeitao, member.YearCaloiro, member.MonthCaloiro) = (null, null, null, null);
            member.MentorId = null;
        }
    }

    private static Dictionary<string, string[]> IdentityErrors(IdentityResult result) =>
        result.Errors
            .GroupBy(e => e.Code.Contains("UserName", StringComparison.Ordinal) ? "nickname" : "email")
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());

    private static bool Complete(int? year, int? month) => year.HasValue && month.HasValue;

    /// <summary>
    /// Owner, or an Admin who is this fiscal year's Magister. The Admin check matters: a Mod reaches the
    /// limited edit, and a Magister who is a Caloiro holds Mod, not Admin - holding the position alone is not enough.
    /// </summary>
    private async Task<bool> CanEditDatesAsync(ClaimsPrincipal user)
    {
        if (user.IsInRole("Owner"))
        {
            return true;
        }

        if (!MembersAuthorization.CanManage(user))
        {
            return false;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var current = FiscalYearHelper.GetCurrentFiscalYearStartYear();
        return (await _assignments.GetAllRoleAssignmentsAsync())
            .Any(a => a.UserId == userId && a.Position == Position.Magister && a.StartYear == current);
    }

    private async Task<MemberEditDto> EditDtoAsync(ApplicationUser m, ClaimsPrincipal user)
    {
        var category = m.Categories.FirstOrDefault() switch
        {
            MemberCategory.Leitao => "Leitao",
            MemberCategory.Caloiro => "Caloiro",
            MemberCategory.Tuno => "Tuno",
            _ => "",
        };
        string? mentorName = null;
        if (!string.IsNullOrEmpty(m.MentorId) && await _users.FindByIdAsync(m.MentorId) is { } mentor)
        {
            mentorName = mentor.GetDisplayName();
        }

        var canEditDates = await CanEditDatesAsync(user);
        return new MemberEditDto(m.Id, m.FirstName, m.LastName, m.Nickname, m.PhoneNumber, m.Email, m.City, m.Degree,
            m.DateOfBirth?.ToString("yyyy-MM-dd"), category,
            m.Categories.Contains(MemberCategory.Fundador), m.Categories.Contains(MemberCategory.TunoHonorario),
            m.YearLeitao, m.MonthLeitao, m.YearCaloiro, m.MonthCaloiro, m.YearTuno, m.MonthTuno, m.MentorId, mentorName,
            await InstrumentsAsync(m.Id),
            new MemberLockedDatesDto(
                !canEditDates && Complete(m.YearLeitao, m.MonthLeitao),
                !canEditDates && Complete(m.YearCaloiro, m.MonthCaloiro),
                !canEditDates && Complete(m.YearTuno, m.MonthTuno)));
    }

    private async Task<IReadOnlyList<MemberInstrumentDto>> InstrumentsAsync(string id) =>
        (await _instruments.GetMemberInstrumentsAsync(id))
            .Select(i => new MemberInstrumentDto(i.Id, i.InstrumentType.ToString(), StatusHelper.GetInstrumentDisplay(i.InstrumentType), i.IsPrimary))
            .ToList();

    private async Task<List<ApplicationUser>> AllUsersAsync()
    {
        await using var db = await _contexts.CreateDbContextAsync();
        return await db.Users.AsNoTracking().ToListAsync();
    }

    private async Task<MemberStatusResult?> StatusAsync(string id) =>
        (await _status.GetMemberStatusesBatchAsync(new List<string> { id })).GetValueOrDefault(id);

    private static bool InActiveList(ApplicationUser u) => !u.IsLeitao() && !u.IsTunoHonorario() && u.IsEffectiveMember();
}
