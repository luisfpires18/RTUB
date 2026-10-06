using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// The old Blazor /member/profile behind the React /profile (task 032, docs/react-member-area.md). The member edits
/// only their own profile, with the old page's fields and rules, now enforced here (the old page only hid inputs):
/// - Pessoal: names, nickname, email, phone, birth date, city, degree, with the entity's own checks; a member who is
///   only a Leitão cannot change the nickname (it comes at Caloiro); an email another account uses is refused;
/// - Tuna: the padrinho (a Tuno or above, never yourself; none for a Leitão, a Fundador or a Tuno Honorário) and the
///   Leitão / Caloiro / Tuno dates (none for a Fundador); a complete date pair is locked (the Magister corrects it);
/// - instruments one by one (the first is the primary; removing the primary promotes the next);
/// - the photo: WebP, JPEG or PNG up to 10 MB, stored by <see cref="IUserProfileService"/> as before (the old photo
///   is deleted, the new one goes to the member's profile folder);
/// - email notifications on / off; the password (at least 8 characters, confirmed), which clears
///   RequirePasswordChange.
/// Push notifications are not here (task 033). No schema change.
/// </summary>
public sealed class MyProfileService : IMyProfileService
{
    public const long MaxPhotoBytes = 10 * 1024 * 1024;
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 100;
    public const int MentorLimit = 20;
    public const string LockedDateMessage = "Não podes alterar as datas. Se estão em erro, contacta o Magister.";

    private static readonly string[] PhotoTypes = { "image/webp", "image/jpeg", "image/png" };

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IMemberDirectoryService _directory;
    private readonly IMemberInstrumentService _instruments;
    private readonly IMemberMentorService _mentors;
    private readonly IRankingService _ranking;
    private readonly IUserProfileService _profiles;
    private readonly IOptions<RankingConfiguration> _rankingConfig;

    public MyProfileService(
        IDbContextFactory<ApplicationDbContext> contexts,
        UserManager<ApplicationUser> users,
        IMemberDirectoryService directory,
        IMemberInstrumentService instruments,
        IMemberMentorService mentors,
        IRankingService ranking,
        IUserProfileService profiles,
        IOptions<RankingConfiguration> rankingConfig)
    {
        _contexts = contexts;
        _users = users;
        _directory = directory;
        _instruments = instruments;
        _mentors = mentors;
        _ranking = ranking;
        _profiles = profiles;
        _rankingConfig = rankingConfig;
    }

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

    public async Task<EventResult<IReadOnlyList<MemberInstrumentDto>>> AddInstrumentAsync(string? instrument, ClaimsPrincipal user)
    {
        var (me, refused) = await MeAsync<IReadOnlyList<MemberInstrumentDto>>(user);
        if (refused is not null)
        {
            return refused;
        }

        if (!Enum.TryParse<InstrumentType>(instrument, out var type) || !Enum.IsDefined(type))
        {
            return EventResult<IReadOnlyList<MemberInstrumentDto>>.Invalid("instrument", "Instrumento inválido.");
        }

        var current = (await _instruments.GetMemberInstrumentsAsync(me.Id)).ToList();
        if (current.Any(i => i.InstrumentType == type))
        {
            return EventResult<IReadOnlyList<MemberInstrumentDto>>.Invalid("instrument", "Este instrumento já está adicionado.");
        }

        try
        {
            // The first instrument becomes the primary, as before.
            await _instruments.AddInstrumentAsync(me.Id, type, current.Count == 0);
        }
        catch (InvalidOperationException)
        {
            return EventResult<IReadOnlyList<MemberInstrumentDto>>.Invalid("instrument", "Este instrumento já está adicionado.");
        }

        return EventResult<IReadOnlyList<MemberInstrumentDto>>.Ok(await InstrumentsAsync(me.Id));
    }

    public async Task<EventResult<IReadOnlyList<MemberInstrumentDto>>> RemoveInstrumentAsync(int instrumentId, ClaimsPrincipal user)
    {
        var (me, refused) = await MeAsync<IReadOnlyList<MemberInstrumentDto>>(user);
        if (refused is not null)
        {
            return refused;
        }

        var current = (await _instruments.GetMemberInstrumentsAsync(me.Id)).ToList();
        if (current.FirstOrDefault(i => i.Id == instrumentId) is not { } removed)
        {
            return EventResult<IReadOnlyList<MemberInstrumentDto>>.Fail(EventResultStatus.NotFound);
        }

        await _instruments.RemoveInstrumentAsync(instrumentId);
        if (removed.IsPrimary && current.FirstOrDefault(i => i.Id != instrumentId) is { } next)
        {
            await _instruments.SetPrimaryInstrumentAsync(next.Id, me.Id);
        }

        return EventResult<IReadOnlyList<MemberInstrumentDto>>.Ok(await InstrumentsAsync(me.Id));
    }

    public async Task<EventResult<IReadOnlyList<MemberInstrumentDto>>> SetPrimaryInstrumentAsync(int instrumentId, ClaimsPrincipal user)
    {
        var (me, refused) = await MeAsync<IReadOnlyList<MemberInstrumentDto>>(user);
        if (refused is not null)
        {
            return refused;
        }

        if ((await _instruments.GetMemberInstrumentsAsync(me.Id)).All(i => i.Id != instrumentId))
        {
            return EventResult<IReadOnlyList<MemberInstrumentDto>>.Fail(EventResultStatus.NotFound);
        }

        await _instruments.SetPrimaryInstrumentAsync(instrumentId, me.Id);
        return EventResult<IReadOnlyList<MemberInstrumentDto>>.Ok(await InstrumentsAsync(me.Id));
    }

    public async Task<EventResult<MyPhotoDto>> SetPhotoAsync(MyPhotoUpload photo, ClaimsPrincipal user)
    {
        var (me, refused) = await MeAsync<MyPhotoDto>(user);
        if (refused is not null)
        {
            return refused;
        }

        if (photo.Length <= 0 || !PhotoTypes.Contains(photo.ContentType, StringComparer.OrdinalIgnoreCase) || !LooksLikeImage(photo.Content))
        {
            return EventResult<MyPhotoDto>.Invalid("photo", "A foto tem de ser WebP, JPEG ou PNG.");
        }

        if (photo.Length > MaxPhotoBytes)
        {
            return EventResult<MyPhotoDto>.Invalid("photo", "A foto não pode exceder 10 MB.");
        }

        var extension = photo.ContentType.ToLowerInvariant() switch
        {
            "image/jpeg" => "jpg",
            "image/png" => "png",
            _ => "webp",
        };
        // The old path: the previous photo is deleted and the new one stored in the member's profile folder.
        await _profiles.UpdateProfilePictureAsync(me.Id, photo.Content, $"profile-picture.{extension}", photo.ContentType.ToLowerInvariant());
        var stored = await _users.FindByIdAsync(me.Id);
        return EventResult<MyPhotoDto>.Ok(new MyPhotoDto(stored?.ProfilePictureSrc ?? me.ProfilePictureSrc));
    }

    public async Task<EventResult<bool>> SetSubscribedAsync(bool subscribed, ClaimsPrincipal user)
    {
        var (me, refused) = await MeAsync<bool>(user);
        if (refused is not null)
        {
            return refused;
        }

        me.Subscribed = subscribed;
        var updated = await _users.UpdateAsync(me);
        return updated.Succeeded
            ? EventResult<bool>.Ok(subscribed)
            : EventResult<bool>.Invalid("subscribed", "Erro ao atualizar preferências de notificação.");
    }

    public async Task<EventResult<bool>> ChangePasswordAsync(MyPasswordInput input, ClaimsPrincipal user)
    {
        var (me, refused) = await MeAsync<bool>(user);
        if (refused is not null)
        {
            return refused;
        }

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrEmpty(input.CurrentPassword))
        {
            errors["currentPassword"] = new[] { "A palavra-passe atual é obrigatória." };
        }

        if (string.IsNullOrEmpty(input.NewPassword))
        {
            errors["newPassword"] = new[] { "A nova palavra-passe é obrigatória." };
        }
        else if (input.NewPassword.Length is < MinPasswordLength or > MaxPasswordLength)
        {
            errors["newPassword"] = new[] { $"A palavra-passe deve ter entre {MinPasswordLength} e {MaxPasswordLength} caracteres." };
        }
        else if (input.NewPassword != input.ConfirmPassword)
        {
            errors["confirmPassword"] = new[] { "A nova palavra-passe e a confirmação não coincidem." };
        }

        if (errors.Count > 0)
        {
            return Invalid<bool>(errors);
        }

        var changed = await _users.ChangePasswordAsync(me, input.CurrentPassword!, input.NewPassword!);
        if (!changed.Succeeded)
        {
            return changed.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch))
                ? EventResult<bool>.Invalid("currentPassword", "A palavra-passe atual não está correta.")
                : EventResult<bool>.Invalid("newPassword", string.Join(" ", changed.Errors.Select(e => e.Description)));
        }

        if (me.RequirePasswordChange)
        {
            me.RequirePasswordChange = false;
            await _users.UpdateAsync(me);
        }

        return EventResult<bool>.Ok(true);
    }

    // ---------- helpers ----------

    /// <summary>The signed-in member, freshly loaded (401 for a visitor, or when the account is gone).</summary>
    private async Task<(ApplicationUser Me, EventResult<T>? Refused)> MeAsync<T>(ClaimsPrincipal user)
    {
        if (!MembersAuthorization.IsMember(user) || user.FindFirstValue(ClaimTypes.NameIdentifier) is not { } id)
        {
            return (null!, EventResult<T>.Fail(EventResultStatus.SignInRequired));
        }

        return await _users.FindByIdAsync(id) is { } me ? (me, null) : (null!, EventResult<T>.Fail(EventResultStatus.SignInRequired));
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
        if (!string.IsNullOrEmpty(me.MentorId) && await _users.FindByIdAsync(me.MentorId) is { } mentor)
        {
            mentorName = mentor.GetDisplayName();
        }

        var rules = Rules(me);
        var options = Enum.GetValues<InstrumentType>()
            .Select(i => new MemberOptionDto(i.ToString(), StatusHelper.GetInstrumentDisplay(i)))
            .ToList();

        return EventResult<MyProfileDto>.Ok(new MyProfileDto(
            detail.Value,
            new MyPersonalDto(me.FirstName, me.LastName, me.Nickname, me.IsOnlyLeitao(), me.Email, me.PhoneNumber,
                me.DateOfBirth?.ToString("yyyy-MM-dd"), me.City, me.Degree),
            new MyTunaDto(rules.ShowMentor, me.MentorId, mentorName, rules.ShowLeitao, rules.ShowCaloiro, rules.ShowTuno,
                me.YearLeitao, me.MonthLeitao, me.YearCaloiro, me.MonthCaloiro, me.YearTuno, me.MonthTuno,
                new MemberLockedDatesDto(Complete(me.YearLeitao, me.MonthLeitao), Complete(me.YearCaloiro, me.MonthCaloiro),
                    Complete(me.YearTuno, me.MonthTuno))),
            await InstrumentsAsync(me.Id),
            options,
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

    private async Task<IReadOnlyList<MemberInstrumentDto>> InstrumentsAsync(string id) =>
        (await _instruments.GetMemberInstrumentsAsync(id))
            .Select(i => new MemberInstrumentDto(i.Id, i.InstrumentType.ToString(), StatusHelper.GetInstrumentDisplay(i.InstrumentType), i.IsPrimary))
            .ToList();

    /// <summary>The entity's own annotations (required names, nickname, email and phone; lengths; the email format).</summary>
    private static Dictionary<string, string[]> Validate(ApplicationUser probe)
    {
        var errors = new Dictionary<string, string[]>();
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(probe, new ValidationContext(probe), results, validateAllProperties: true);
        foreach (var result in results)
        {
            foreach (var name in result.MemberNames)
            {
                errors.TryAdd(char.ToLowerInvariant(name[0]) + name[1..], new[] { result.ErrorMessage! });
            }
        }

        return errors;
    }

    private static Dictionary<string, string[]> IdentityErrors(IdentityResult result) =>
        result.Errors
            .GroupBy(e => e.Code.Contains("UserName", StringComparison.Ordinal) ? "nickname" : "email")
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());

    private static EventResult<T> Invalid<T>(Dictionary<string, string[]> errors) => new(EventResultStatus.Invalid, Errors: errors);

    private static bool Complete(int? year, int? month) => year.HasValue && month.HasValue;

    /// <summary>Trimmed, or null when blank.</summary>
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool LooksLikeImage(Stream content)
    {
        if (!content.CanSeek)
        {
            return false;
        }

        var head = new byte[12];
        var read = content.Read(head, 0, head.Length);
        content.Position = 0;
        return read == 12 && (
            (head[0] == 'R' && head[1] == 'I' && head[2] == 'F' && head[3] == 'F' && head[8] == 'W' && head[9] == 'E' && head[10] == 'B' && head[11] == 'P')
            || (head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF)
            || (head[0] == 0x89 && head[1] == 'P' && head[2] == 'N' && head[3] == 'G'));
    }
}
