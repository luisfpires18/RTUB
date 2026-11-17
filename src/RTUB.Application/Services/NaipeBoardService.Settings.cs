// Naipe board: instrument settings and pictures.
using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Services;

public sealed partial class NaipeBoardService
{
    public async Task<EventResult<IReadOnlyList<NaipeTypeSettingDto>>> GetSettingsAsync(ClaimsPrincipal user)
    {
        if (SettingsRefusal(user) is { } refused)
        {
            return refused;
        }

        await _naipes.InitializeTypeConfigsAsync();
        return EventResult<IReadOnlyList<NaipeTypeSettingDto>>.Ok(await SettingsAsync());
    }

    public async Task<EventResult<IReadOnlyList<NaipeTypeSettingDto>>> UpdateSettingAsync(int id, NaipeTypeSettingInput input, ClaimsPrincipal user)
    {
        var (setting, refused) = await SettingAsync(id, user);
        if (refused is not null)
        {
            return refused;
        }

        if (input.SortOrder is not { } sortOrder || sortOrder < 0 || sortOrder > MaxTypeSortOrder)
        {
            return EventResult<IReadOnlyList<NaipeTypeSettingDto>>.Invalid("sortOrder", $"A ordem deve estar entre 0 e {MaxTypeSortOrder}.");
        }

        await _naipes.UpdateTypeConfigAsync(id, setting.PictureUrl, input.IsVisible, sortOrder);
        return EventResult<IReadOnlyList<NaipeTypeSettingDto>>.Ok(await SettingsAsync());
    }

    public async Task<EventResult<IReadOnlyList<NaipeTypeSettingDto>>> SetPictureAsync(int id, NaipePictureUpload picture, ClaimsPrincipal user)
    {
        var (_, refused) = await SettingAsync(id, user);
        if (refused is not null)
        {
            return refused;
        }

        if (picture.Length <= 0 || !PictureTypes.Contains(picture.ContentType, StringComparer.OrdinalIgnoreCase) || !LooksLikeImage(picture.Content))
        {
            return EventResult<IReadOnlyList<NaipeTypeSettingDto>>.Invalid("picture", "A imagem tem de ser JPEG, PNG ou WebP.");
        }

        if (picture.Length > MaxPictureBytes)
        {
            return EventResult<IReadOnlyList<NaipeTypeSettingDto>>.Invalid("picture", "A imagem não pode exceder 5 MB.");
        }

        // The old path: stored under the instrument's typeconfig folder; the previous picture is deleted.
        await _naipes.UploadTypeConfigPictureAsync(id, picture.Content,
            string.IsNullOrWhiteSpace(picture.FileName) ? "naipe.jpg" : picture.FileName, picture.ContentType.ToLowerInvariant());
        return EventResult<IReadOnlyList<NaipeTypeSettingDto>>.Ok(await SettingsAsync());
    }

    public async Task<EventResult<IReadOnlyList<NaipeTypeSettingDto>>> RemovePictureAsync(int id, ClaimsPrincipal user)
    {
        var (setting, refused) = await SettingAsync(id, user);
        if (refused is not null)
        {
            return refused;
        }

        // As before, "Remover imagem" clears the reference only (the stored file stays).
        await _naipes.UpdateTypeConfigAsync(id, null, setting.IsVisible, setting.SortOrder);
        return EventResult<IReadOnlyList<NaipeTypeSettingDto>>.Ok(await SettingsAsync());
    }
}
