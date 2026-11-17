// Instruments: image upload and the shared inventory helpers.
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Extensions;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Application.Utilities;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

public sealed partial class InstrumentInventoryService
{
    public async Task<EventResult<InstrumentDetailDto>> SetImageAsync(int id, InstrumentImageUpload image, InstrumentImageUpload thumbnail, ClaimsPrincipal user)
    {
        if (await RefusalAsync<InstrumentDetailDto>(user) is { } refused)
        {
            return refused;
        }

        if (await _instruments.GetByIdAsync(id) is not { } instrument)
        {
            return EventResult<InstrumentDetailDto>.Fail(EventResultStatus.NotFound);
        }

        foreach (var (key, file) in new[] { ("image", image), ("thumbnail", thumbnail) })
        {
            if (file.Length <= 0 || !ImageTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase) || !LooksLikeImage(file.Content))
            {
                return EventResult<InstrumentDetailDto>.Invalid(key, "A imagem tem de ser WebP, JPEG ou PNG.");
            }

            if (file.Length > MaxImageBytes)
            {
                return EventResult<InstrumentDetailDto>.Invalid(key, "A imagem não pode exceder 10 MB.");
            }
        }

        // As the old save: the previous image and thumbnail are deleted, then both new ones are stored.
        var key2 = S3KeyNormalizer.NormalizeForS3Key(instrument.Name);
        if (!string.IsNullOrEmpty(instrument.ImageUrl))
        {
            await _storage.DeleteImageAsync(instrument.ImageUrl);
        }

        instrument.ImageUrl = await _storage.UploadImageAsync(image.Content, image.FileName, image.ContentType, "instruments", key2);
        if (!string.IsNullOrEmpty(instrument.ThumbnailUrl))
        {
            await _storage.DeleteImageAsync(instrument.ThumbnailUrl);
        }

        instrument.ThumbnailUrl = await _storage.UploadImageAsync(thumbnail.Content, "instrument-thumbnail.webp", thumbnail.ContentType,
            "instruments/thumbnails", key2);
        await _instruments.UpdateAsync(instrument);
        return EventResult<InstrumentDetailDto>.Ok(Detail((await _instruments.GetByIdAsync(id))!));
    }

    // ---------- helpers ----------

    private async Task<EventResult<T>?> RefusalAsync<T>(ClaimsPrincipal user) =>
        !InventoryAuthorization.IsMember(user) ? EventResult<T>.Fail(EventResultStatus.SignInRequired)
        : !await CanManageAsync(user) ? EventResult<T>.Fail(EventResultStatus.Forbidden)
        : null;

    /// <summary>Mod, Admin and Owner by role; a Leitão by category (keeping the inventory up to date is their job).</summary>
    private async Task<bool> CanManageAsync(ClaimsPrincipal user)
    {
        if (InventoryAuthorization.CanManage(user))
        {
            return true;
        }

        if (InventoryAuthorization.UserId(user) is not { } id)
        {
            return false;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id) is { } member && member.IsLeitao();
    }

    private static bool Contains(string? field, string search) =>
        !string.IsNullOrEmpty(field) && field.Contains(search, StringComparison.OrdinalIgnoreCase);

    /// <summary>The entity's own limits and messages; the type only when creating.</summary>
    private static Dictionary<string, string[]> Validate(InstrumentInput input, bool creating)
    {
        var errors = new Dictionary<string, string[]>();
        void Check(string key, string? value, int max, string tooLong)
        {
            if (value is not null && value.Length > max)
            {
                errors[key] = new[] { tooLong };
            }
        }

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            errors["name"] = new[] { "O nome é obrigatório" };
        }
        else
        {
            Check("name", input.Name, 100, "O nome não pode exceder 100 caracteres");
        }

        if (creating && (string.IsNullOrEmpty(input.Category) || !Enum.GetNames<InstrumentType>().Contains(input.Category)))
        {
            errors["category"] = new[] { "A categoria é obrigatória" };
        }

        if (string.IsNullOrEmpty(input.Condition) || !Enum.GetNames<InstrumentCondition>().Contains(input.Condition))
        {
            errors["condition"] = new[] { "A condição é obrigatória" };
        }

        Check("brand", input.Brand, 100, "A marca não pode exceder 100 caracteres");
        Check("serialNumber", input.SerialNumber, 100, "O número de série não pode exceder 100 caracteres");
        Check("location", input.Location, 200, "A localização não pode exceder 200 caracteres");
        Check("maintenanceNotes", input.MaintenanceNotes, 500, "As notas de manutenção não podem exceder 500 caracteres");
        if (input.LastMaintenanceDate is { } date && (date.Year < 1900 || date.Year > 2100))
        {
            errors["lastMaintenanceDate"] = new[] { "Data inválida." };
        }

        return errors;
    }

    /// <summary>Empty optional fields are stored as null, as the old form's blank inputs were.</summary>
    private static void Apply(Instrument instrument, InstrumentInput input)
    {
        static string? Blank(string? s) => string.IsNullOrEmpty(s) ? null : s;
        instrument.Name = input.Name!;
        instrument.Condition = Enum.Parse<InstrumentCondition>(input.Condition!);
        instrument.Brand = Blank(input.Brand);
        instrument.SerialNumber = Blank(input.SerialNumber);
        instrument.Location = Blank(input.Location);
        instrument.MaintenanceNotes = Blank(input.MaintenanceNotes);
        instrument.LastMaintenanceDate = input.LastMaintenanceDate?.ToDateTime(TimeOnly.MinValue);
    }

    private static string CategoryLabel(string category) =>
        Enum.TryParse<InstrumentType>(category, out var type) && Enum.IsDefined(type) ? StatusHelper.GetInstrumentDisplay(type) : category;

    private static InstrumentCardDto Card(Instrument i) => new(
        i.Id, i.Name, i.Category, CategoryLabel(i.Category), i.Condition.ToString(), InstrumentConditionHelper.GetDisplayName(i.Condition),
        i.Brand, i.Location, string.IsNullOrEmpty(i.ThumbnailSrc) ? null : i.ThumbnailSrc);

    private static InstrumentDetailDto Detail(Instrument i) => new(
        i.Id, i.Name, i.Category, CategoryLabel(i.Category), i.Condition.ToString(), InstrumentConditionHelper.GetDisplayName(i.Condition),
        i.Brand, i.SerialNumber, i.Location, i.LastMaintenanceDate?.ToString("yyyy-MM-dd"), i.MaintenanceNotes,
        string.IsNullOrEmpty(i.ImageUrl) ? null : i.ImageUrl, string.IsNullOrEmpty(i.ThumbnailUrl) ? null : i.ThumbnailUrl);

    /// <summary>The declared type is the browser's word; the first bytes must agree (WebP, JPEG or PNG).</summary>
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
