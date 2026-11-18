using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// The React /inventory (docs/react-inventory.md).
/// Same data and rules, through <see cref="IInstrumentService"/>, now enforced server-side
/// (<see cref="InventoryAuthorization"/>):
/// - who manages: Mod, Admin, Owner and every Leitão; everyone else reads;
/// - the list: every instrument by name; search over name, type (as stored) and brand, case-insensitive; type and
///   condition filters; the counters over every instrument (total, then the first three conditions present);
/// - create: name (≤ 100) and type (an <see cref="InstrumentType"/>) required, brand and serial number ≤ 100, location
///   ≤ 200, maintenance notes ≤ 500, condition one of <see cref="InstrumentCondition"/> (default Bom);
/// - edit: the same fields except the type, which an edit never changes;
/// - image: the original (images/…/instruments) and its cropped 1:1 thumbnail (…/instruments/thumbnails) together;
///   a new image deletes the previous image and thumbnail first;
/// - delete: a hard delete, which also deletes the image (not the thumbnail).
/// No schema change.
/// </summary>
public sealed partial class InstrumentInventoryService : IInstrumentInventoryService
{
    public const long MaxImageBytes = 10 * 1024 * 1024;
    private static readonly string[] ImageTypes = { "image/webp", "image/jpeg", "image/png" };

    private readonly IInstrumentService _instruments;
    private readonly IImageStorageService _storage;

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;

    public InstrumentInventoryService(IInstrumentService instruments, IImageStorageService storage, IDbContextFactory<ApplicationDbContext> contexts)
    {
        _instruments = instruments;
        _storage = storage;
        _contexts = contexts;
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
            await CanManageAsync(user)));
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
        if (await RefusalAsync<InstrumentCreatedDto>(user) is { } refused)
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
        if (await RefusalAsync<InstrumentDetailDto>(user) is { } refused)
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
        if (await RefusalAsync<bool>(user) is { } refused)
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
}
