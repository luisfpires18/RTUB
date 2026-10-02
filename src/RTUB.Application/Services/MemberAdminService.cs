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

/// <summary>
/// The old Blazor /members/manage tools behind the React /members (React track 018, docs/react-members.md). Same
/// behaviour, rules now server-side (<see cref="MembersAuthorization"/>; the old page only hid buttons):
/// - create: username from the nickname (or, for a Leitão "sem alcunha", the email's local part), a generated password
///   the member must change, role Member, the categories, the instruments, and the welcome email;
/// - edit: everything but the username; a complete Leitão / Caloiro / Tuno date pair changes only for Owner or the
///   current Magister (others' changes to it are ignored, as before); Fundador = Tuno since December 1991, Fundador and
///   Tuno Honorário have no Leitão / Caloiro dates and no padrinho; nobody is their own padrinho;
/// - instruments one by one (the first is the primary; removing the primary promotes the next);
/// - Leitões: "Definir alcunha" (new nickname and username, email to the member), expel and reactivate;
/// - "Tornar ativo" and the push reminder, under the old list's conditions;
/// - delete: the old hard delete with related data; Owner any member, Admin Leitões only.
/// No schema change.
/// </summary>
public sealed class MemberAdminService : IMemberAdminService
{
    public const int MentorLimit = 20;
    public const int MinYear = 1990;
    private static readonly Dictionary<string, MemberCategory> BaseCategories = new()
    {
        ["Leitao"] = MemberCategory.Leitao,
        ["Caloiro"] = MemberCategory.Caloiro,
        ["Tuno"] = MemberCategory.Tuno,
    };

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IRoleAssignmentService _assignments;
    private readonly IMemberInstrumentService _instruments;
    private readonly IMemberMentorService _mentors;
    private readonly IMemberStatusService _status;
    private readonly IUserProfileService _profiles;
    private readonly IEmailNotificationService _email;
    private readonly IPushNotificationService _push;
    private readonly IPushNotificationFactory _pushFactory;
    private readonly ILogger<MemberAdminService> _logger;

    public MemberAdminService(
        IDbContextFactory<ApplicationDbContext> contexts,
        UserManager<ApplicationUser> users,
        IRoleAssignmentService assignments,
        IMemberInstrumentService instruments,
        IMemberMentorService mentors,
        IMemberStatusService status,
        IUserProfileService profiles,
        IEmailNotificationService email,
        IPushNotificationService push,
        IPushNotificationFactory pushFactory,
        ILogger<MemberAdminService> logger)
    {
        _contexts = contexts;
        _users = users;
        _assignments = assignments;
        _instruments = instruments;
        _mentors = mentors;
        _status = status;
        _profiles = profiles;
        _email = email;
        _push = push;
        _pushFactory = pushFactory;
        _logger = logger;
    }

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
        if (Refusal<IReadOnlyList<MentorCandidateDto>>(user) is { } refused)
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
        if (Refusal<MemberCreatedDto>(user) is { } refused)
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

    public async Task<EventResult<IReadOnlyList<MemberInstrumentDto>>> AddInstrumentAsync(string id, string? instrument, ClaimsPrincipal user)
    {
        if (await MemberAsync<IReadOnlyList<MemberInstrumentDto>>(id, user) is { Refused: { } refused })
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
        if (await MemberAsync<IReadOnlyList<MemberInstrumentDto>>(id, user) is { Refused: { } refused })
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
        if (await MemberAsync<IReadOnlyList<MemberInstrumentDto>>(id, user) is { Refused: { } refused })
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

    public async Task<EventResult<bool>> MakeActiveAsync(string id, ClaimsPrincipal user)
    {
        var (member, refused) = await MemberAsync<bool>(id, user);
        if (refused is not null)
        {
            return refused;
        }

        // The old active-members list: Caloiro, Tuno, Veterano, Tunossauro; retired, or on the three months back.
        var status = await StatusAsync(id);
        if (!InActiveList(member) || !((status?.IsRetired ?? member.IsRetired) || status?.ProgressTotalMonths == 3))
        {
            return EventResult<bool>.Fail(EventResultStatus.Forbidden);
        }

        await _status.ActivateMemberWithOverrideAsync(id);
        _logger.LogInformation("Member {UserName} was manually made active by an admin", member.UserName);
        return EventResult<bool>.Ok(true);
    }

    public async Task<EventResult<bool>> SendReminderAsync(string id, string baseUrl, ClaimsPrincipal user)
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

        await _push.SendToUserAsync(id, _pushFactory.CreateMemberActivityReminderNotification(member.UserName, id, baseUrl));
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

    // ---------- helpers ----------

    private static EventResult<T>? Refusal<T>(ClaimsPrincipal user) =>
        !MembersAuthorization.IsMember(user) ? EventResult<T>.Fail(EventResultStatus.SignInRequired)
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

    /// <summary>The old form's checks: the entity's own annotations, the category, and the month/year pickers' range.</summary>
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

        // Incomplete pairs exist in old data and stay allowed; only impossible values are refused.
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

    /// <summary>A padrinho that is kept must exist and be Tuno or above (the old list only offered those).</summary>
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

    /// <summary>Owner, or an Admin who is this fiscal year's Magister.</summary>
    private async Task<bool> CanEditDatesAsync(ClaimsPrincipal user)
    {
        if (user.IsInRole("Owner"))
        {
            return true;
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
