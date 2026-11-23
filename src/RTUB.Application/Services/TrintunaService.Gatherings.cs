// Trintuna: create, edit, delete and settle a gathering; input validation and the poll option diff.
using System.Globalization;
using System.Security.Claims;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

public sealed partial class TrintunaService
{
    public const int MaxPlace = 200;
    public const int MaxDescription = 2000;
    public const int MaxPollDates = 12;

    /// <summary>Tries to save a new gathering when another create took the same number first (unique index).</summary>
    internal const int NumberTries = 5;

    public async Task<EventResult<GatheringDto>> CreateAsync(GatheringInput input, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var (refused, listed) = await SeeAsync<GatheringDto>(db, user);
        if (refused is not null)
        {
            return refused;
        }

        if (!TrintunaAuthorization.CanManage(user))
        {
            return EventResult<GatheringDto>.Fail(EventResultStatus.Forbidden);
        }

        var (errors, dates) = Validate(input);
        await CheckOwnerFieldsAsync(db, input, errors);
        if (errors.Count > 0)
        {
            return new EventResult<GatheringDto>(EventResultStatus.Invalid, Errors: errors);
        }

        var gathering = new Gathering
        {
            Place = input.Place!.Trim(),
            Description = Clean(input.Description),
            OrganizerUserId = Clean(input.OrganizerUserId),
            PollOptions = dates.Select(d => new GatheringPollOption { Date = d }).ToList(),
        };
        db.Gatherings.Add(gathering);
        // The next number is highest + 1. SQLite has one writer: a create that loses the race hits the unique index
        // (constraint, code 19) and takes the next number on its next try.
        for (var attempt = 1; ; attempt++)
        {
            gathering.Number = (await db.Gatherings.MaxAsync(g => (int?)g.Number) ?? 0) + 1;
            try
            {
                await db.SaveChangesAsync();
                break;
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19 })
            {
                if (attempt == NumberTries)
                {
                    return EventResult<GatheringDto>.Fail(EventResultStatus.Conflict);
                }
            }
        }

        return EventResult<GatheringDto>.Ok(await LoadAsync(db, gathering.Id, user, listed));
    }

    public async Task<EventResult<GatheringDto>> UpdateAsync(int id, GatheringInput input, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var (refused, listed) = await SeeAsync<GatheringDto>(db, user);
        if (refused is not null)
        {
            return refused;
        }

        var gathering = await db.Gatherings.Include(g => g.PollOptions).FirstOrDefaultAsync(g => g.Id == id);
        if (gathering is null)
        {
            return EventResult<GatheringDto>.Fail(EventResultStatus.NotFound);
        }

        if (!TrintunaAuthorization.CanEdit(user, listed, gathering.OrganizerUserId))
        {
            return EventResult<GatheringDto>.Fail(EventResultStatus.Forbidden);
        }

        // The organizer edits place, description and dates; the organizer stays as stored. The number never changes.
        var owner = TrintunaAuthorization.CanManage(user);
        var effective = owner ? input : input with { OrganizerUserId = gathering.OrganizerUserId };
        var (errors, dates) = Validate(effective);
        if (owner)
        {
            await CheckOwnerFieldsAsync(db, effective, errors);
        }

        var kept = gathering.PollOptions.Select(o => o.Date).ToHashSet();
        if (PollClosed(gathering) && !kept.SetEquals(dates))
        {
            errors["pollDates"] = new[] { "A data já está marcada: as datas possíveis já não mudam." };
        }

        if (errors.Count > 0)
        {
            return new EventResult<GatheringDto>(EventResultStatus.Invalid, Errors: errors);
        }

        gathering.Place = effective.Place!.Trim();
        gathering.Description = Clean(effective.Description);
        gathering.OrganizerUserId = Clean(effective.OrganizerUserId);
        // Kept dates keep their votes; a removed date takes its votes with it (cascade).
        db.GatheringPollOptions.RemoveRange(gathering.PollOptions.Where(o => !dates.Contains(o.Date)));
        foreach (var date in dates.Where(d => !kept.Contains(d)))
        {
            gathering.PollOptions.Add(new GatheringPollOption { Date = date });
        }

        await db.SaveChangesAsync();
        return EventResult<GatheringDto>.Ok(await LoadAsync(db, gathering.Id, user, listed));
    }

    public async Task<EventResult<bool>> DeleteAsync(int id, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var (refused, _) = await SeeAsync<bool>(db, user);
        if (refused is not null)
        {
            return refused;
        }

        if (!TrintunaAuthorization.CanManage(user))
        {
            return EventResult<bool>.Fail(EventResultStatus.Forbidden);
        }

        var gathering = await db.Gatherings.FirstOrDefaultAsync(g => g.Id == id);
        if (gathering is null)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        db.Gatherings.Remove(gathering);
        await db.SaveChangesAsync();
        return EventResult<bool>.Ok(true);
    }

    public async Task<EventResult<GatheringDto>> SettleAsync(int id, GatheringOptionInput input, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var (refused, listed) = await SeeAsync<GatheringDto>(db, user);
        if (refused is not null)
        {
            return refused;
        }

        var gathering = await db.Gatherings.Include(g => g.PollOptions).FirstOrDefaultAsync(g => g.Id == id);
        if (gathering is null)
        {
            return EventResult<GatheringDto>.Fail(EventResultStatus.NotFound);
        }

        if (!TrintunaAuthorization.CanEdit(user, listed, gathering.OrganizerUserId))
        {
            return EventResult<GatheringDto>.Fail(EventResultStatus.Forbidden);
        }

        if (PollClosed(gathering))
        {
            return EventResult<GatheringDto>.Fail(EventResultStatus.Closed);
        }

        var option = gathering.PollOptions.FirstOrDefault(o => o.Id == input.OptionId);
        if (option is null)
        {
            return EventResult<GatheringDto>.Invalid("optionId", "Escolha uma das datas possíveis deste almoço.");
        }

        gathering.Date = option.Date;
        await db.SaveChangesAsync();
        return EventResult<GatheringDto>.Ok(await LoadAsync(db, gathering.Id, user, listed));
    }

    /// <summary>Place, description and poll dates; the parsed dates (local, 00:00) in order.</summary>
    private static (Dictionary<string, string[]> Errors, List<DateTime> Dates) Validate(GatheringInput input)
    {
        var errors = new Dictionary<string, string[]>();
        var place = input.Place?.Trim() ?? string.Empty;
        if (place.Length == 0)
        {
            errors["place"] = new[] { "Indique o local." };
        }
        else if (place.Length > MaxPlace)
        {
            errors["place"] = new[] { $"O local tem no máximo {MaxPlace} caracteres." };
        }

        if ((input.Description?.Trim().Length ?? 0) > MaxDescription)
        {
            errors["description"] = new[] { $"A descrição tem no máximo {MaxDescription} caracteres." };
        }

        var dates = new List<DateTime>();
        foreach (var text in input.PollDates ?? Array.Empty<string>())
        {
            if (!DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                errors["pollDates"] = new[] { "Há uma data possível inválida." };
            }
            else if (dates.Contains(date))
            {
                errors["pollDates"] = new[] { "Há datas possíveis repetidas." };
            }
            else
            {
                dates.Add(date);
            }
        }

        if (dates.Count > MaxPollDates)
        {
            errors["pollDates"] = new[] { $"No máximo {MaxPollDates} datas possíveis." };
        }

        dates.Sort();
        return (errors, dates);
    }

    /// <summary>Owner-only field: an organizer who can see the page (<see cref="Viewers"/>, as GET /organizers lists).</summary>
    private static async Task CheckOwnerFieldsAsync(ApplicationDbContext db, GatheringInput input, Dictionary<string, string[]> errors)
    {
        if (Clean(input.OrganizerUserId) is not { } organizer)
        {
            return;
        }

        if (!await Viewers(db).AnyAsync(u => u.Id == organizer))
        {
            errors["organizerUserId"] = new[] { "O organizador tem de estar na lista de acesso." };
        }
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
