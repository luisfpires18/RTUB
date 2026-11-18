// My profile: instruments, photo, subscription, password and shared helpers.
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

public sealed partial class MyProfileService
{
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
            // The first instrument becomes the primary.
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
        // The previous photo is deleted and the new one stored in the member's profile folder.
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
