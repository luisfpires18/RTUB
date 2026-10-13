using System.Security.Claims;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Application.Utilities;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// The old Blazor /inventory ("Instrumentos") behind the React /inventory (React track 020, docs/react-inventory.md).
/// Same data and rules, through <see cref="IInstrumentService"/>, now enforced server-side
/// (<see cref="InventoryAuthorization"/>; the old page only hid buttons):
/// - the list: every instrument by name; search over name, type (as stored) and brand, case-insensitive; type and
///   condition filters; the counters over every instrument (total, then the first three conditions present);
/// - create: name (≤ 100) and type (an <see cref="InstrumentType"/>) required, brand and serial number ≤ 100, location
///   ≤ 200, maintenance notes ≤ 500, condition one of <see cref="InstrumentCondition"/> (default Bom);
/// - edit: the same fields except the type, which the old edit never changed;
/// - image: the original (images/…/instruments) and its cropped 1:1 thumbnail (…/instruments/thumbnails) together;
///   a new image deletes the previous image and thumbnail first;
/// - delete: the old hard delete, which also deletes the image (not the thumbnail, as before).
/// No schema change.
/// </summary>
public sealed class InstrumentInventoryService : IInstrumentInventoryService
{
    public const long MaxImageBytes = 10 * 1024 * 1024;
    private static readonly string[] ImageTypes = { "image/webp", "image/jpeg", "image/png" };

    private readonly IInstrumentService _instruments;
    private readonly IImageStorageService _storage;

    public InstrumentInventoryService(IInstrumentService instruments, IImageStorageService storage)
    {
        _instruments = instruments;
        _storage = storage;
    }

    public async Task<EventResult<InstrumentListDto>> GetAsync(InstrumentQuery query, ClaimsPrincipal user)
    {
        if (!InventoryAuthorization.IsMember(user))
        {
            return EventResult<InstrumentListDto>.Fail(EventResultStatus.SignInRequired);
        }

        if (!string.IsNullOrEmpty(query.Category) && !Enum.GetNames<InstrumentType>().Contains(query.Category))
        {
            return EventResult<InstrumentListDto>.Invalid("category", "Tipo inválido.");
        }

        InstrumentCondition? condition = null;
        if (!string.IsNullOrEmpty(query.Condition))
        {
            if (!Enum.TryParse<InstrumentCondition>(query.Condition, out var parsed) || !Enum.IsDefined(parsed) || int.TryParse(query.Condition, out _))
            {
                return EventResult<InstrumentListDto>.Invalid("condition", "Condição inválida.");
            }

            condition = parsed;
        }

        var all = (await _instruments.GetAllAsync()).ToList();
        var search = query.Search?.Trim() ?? "";
        var shown = all
            .Where(i => search == "" || Contains(i.Name, search) || Contains(i.Category, search) || Contains(i.Brand, search))
            .Where(i => string.IsNullOrEmpty(query.Category) || i.Category == query.Category)
            .Where(i => condition is null || i.Condition == condition)
            .Select(Card)
            .ToList();

        var stats = (await _instruments.GetConditionStatsAsync())
            .OrderBy(s => s.Key)
            .Take(3)
            .Select(s => new InstrumentStatDto(s.Key.ToString(), InstrumentConditionHelper.GetDisplayName(s.Key), s.Value))
            .ToList();

        return EventResult<InstrumentListDto>.Ok(new InstrumentListDto(
            all.Count, stats, shown,
            Enum.GetValues<InstrumentType>().Select(t => new MemberOptionDto(t.ToString(), StatusHelper.GetInstrumentDisplay(t))).ToList(),
            Enum.GetValues<InstrumentCondition>().Select(c => new MemberOptionDto(c.ToString(), InstrumentConditionHelper.GetDisplayName(c))).ToList(),
            InventoryAuthorization.CanManage(user)));
    }

    public async Task<EventResult<InstrumentDetailDto>> GetByIdAsync(int id, ClaimsPrincipal user)
    {
        if (!InventoryAuthorization.IsMember(user))
        {
            return EventResult<InstrumentDetailDto>.Fail(EventResultStatus.SignInRequired);
        }

        return await _instruments.GetByIdAsync(id) is { } i
            ? EventResult<InstrumentDetailDto>.Ok(Detail(i))
            : EventResult<InstrumentDetailDto>.Fail(EventResultStatus.NotFound);
    }

    public async Task<EventResult<InstrumentCreatedDto>> CreateAsync(InstrumentInput input, ClaimsPrincipal user)
    {
        if (Refusal<InstrumentCreatedDto>(user) is { } refused)
        {
            return refused;
        }

        var errors = Validate(input, creating: true);
        if (errors.Count > 0)
        {
            return new EventResult<InstrumentCreatedDto>(EventResultStatus.Invalid, Errors: errors);
        }

        var instrument = Instrument.CreateEmpty();
        instrument.Category = input.Category!;
        Apply(instrument, input);
        var created = await _instruments.CreateAsync(instrument);
        return EventResult<InstrumentCreatedDto>.Ok(new InstrumentCreatedDto(created.Id));
    }

    public async Task<EventResult<InstrumentDetailDto>> UpdateAsync(int id, InstrumentInput input, ClaimsPrincipal user)
    {
        if (Refusal<InstrumentDetailDto>(user) is { } refused)
        {
            return refused;
        }

        if (await _instruments.GetByIdAsync(id) is not { } instrument)
        {
            return EventResult<InstrumentDetailDto>.Fail(EventResultStatus.NotFound);
        }

        var errors = Validate(input, creating: false);
        if (errors.Count > 0)
        {
            return new EventResult<InstrumentDetailDto>(EventResultStatus.Invalid, Errors: errors);
        }

        Apply(instrument, input);
        await _instruments.UpdateAsync(instrument);
        return EventResult<InstrumentDetailDto>.Ok(Detail((await _instruments.GetByIdAsync(id))!));
    }

    public async Task<EventResult<bool>> DeleteAsync(int id, ClaimsPrincipal user)
    {
        if (Refusal<bool>(user) is { } refused)
        {
            return refused;
        }

        if (await _instruments.GetByIdAsync(id) is null)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        await _instruments.DeleteAsync(id);
        return EventResult<bool>.Ok(true);
    }

    public async Task<EventResult<InstrumentDetailDto>> SetImageAsync(int id, InstrumentImageUpload image, InstrumentImageUpload thumbnail, ClaimsPrincipal user)
    {
        if (Refusal<InstrumentDetailDto>(user) is { } refused)
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

    private static EventResult<T>? Refusal<T>(ClaimsPrincipal user) =>
        !InventoryAuthorization.IsMember(user) ? EventResult<T>.Fail(EventResultStatus.SignInRequired)
        : !InventoryAuthorization.CanManage(user) ? EventResult<T>.Fail(EventResultStatus.Forbidden)
        : null;

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
