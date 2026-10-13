using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// The old Blazor /calotes, /mbway, /nerba and /nerba/event/{id} behind the React /treasury/calotes, /treasury/mbway and
/// /treasury/nerba[/{eventId}] (React track 024, docs/react-treasury.md). Same tables; the writes go through the old
/// services. Rules are server-side now (<see cref="TreasuryAuthorization"/>); three of these pages had no sign-in at all.
/// - calotes: treasury members see every debt of a fiscal year, Caloiros and Leitões only their own; Mod / Admin / Owner
///   add (member not a Leitão, amount &gt; 0, a new debt inherits the member's commitment date), edit, remove and set the
///   commitment date for all of a member's debts of the year;
/// - MBWay: every transfer, newest first, totals per recipient (never counted in report totals); Mod / Admin / Owner add,
///   Owner edits and deletes; the recipient is a member (the old service refused anything else);
/// - Nerba: events of type Nerba, upcoming then past; items per day of a multi-day event; Mod / Admin / Owner manage.
/// The member pickers return names and avatars (no e-mail, though it is still searched, as before).
/// </summary>
public sealed class TreasuryRecordsService : ITreasuryRecordsService
{
    public const int SearchLimit = 20;

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly IMemberDebtService _debts;
    private readonly IMbwayTransferService _transfers;
    private readonly INerbaOrderService _orders;

    public TreasuryRecordsService(IDbContextFactory<ApplicationDbContext> contexts, IMemberDebtService debts, IMbwayTransferService transfers,
        INerbaOrderService orders)
    {
        _contexts = contexts;
        _debts = debts;
        _transfers = transfers;
        _orders = orders;
    }

    // ---------------------------------------------------------------- calotes

    public async Task<EventResult<TreasuryCalotesDto>> GetCalotesAsync(string? fiscalYear, ClaimsPrincipal user)
    {
        if (await MemberAsync(user) is not { } me)
        {
            return EventResult<TreasuryCalotesDto>.Fail(EventResultStatus.SignInRequired);
        }

        return EventResult<TreasuryCalotesDto>.Ok(await CalotesAsync(fiscalYear, me, user));
    }

    public async Task<EventResult<TreasuryCalotesDto>> AddDebtAsync(TreasuryDebtInput input, ClaimsPrincipal user)
    {
        if (await RefusalAsync<TreasuryCalotesDto>(user, TreasuryAuthorization.CanManageRecords) is { } refused)
        {
            return refused;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        var start = GovernanceService.TryParseStartYear(input.FiscalYear);
        if (await db.FiscalYears.AsNoTracking().FirstOrDefaultAsync(f => f.StartYear == start) is not { } year)
        {
            return EventResult<TreasuryCalotesDto>.Invalid("fiscalYear", "Ano letivo não encontrado.");
        }

        if (await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == input.UserId) is not { } debtor || debtor.IsLeitao())
        {
            return EventResult<TreasuryCalotesDto>.Invalid("userId", "Por favor, selecione um membro.");
        }

        if (DebtError(input) is { } error)
        {
            return EventResult<TreasuryCalotesDto>.Invalid(error.Item1, error.Item2);
        }

        // A new debt joins the member's commitment of the year, as the old page did.
        var commitment = (await db.MemberDebts.AsNoTracking().Where(d => d.UserId == debtor.Id && d.FiscalYearId == year.Id)
            .Select(d => d.CompromisedUntil).ToListAsync()).Where(d => d.HasValue).Min();
        await _debts.AddDebtAsync(debtor.Id, input.Amount!.Value, Text(input.Description), year.Id, commitment);
        return EventResult<TreasuryCalotesDto>.Ok(await CalotesAsync(year.FiscalYearString, (await MemberAsync(user))!, user));
    }

    public async Task<EventResult<TreasuryCalotesDto>> UpdateDebtAsync(int id, TreasuryDebtInput input, ClaimsPrincipal user) =>
        await ChangeDebtAsync(id, user, async debt =>
        {
            if (DebtError(input) is { } error)
            {
                return error;
            }

            await _debts.UpdateDebtAsync(id, input.Amount!.Value, Text(input.Description), debt.CompromisedUntil);
            return null;
        });

    public async Task<EventResult<TreasuryCalotesDto>> DeleteDebtAsync(int id, ClaimsPrincipal user) =>
        await ChangeDebtAsync(id, user, async _ =>
        {
            await _debts.DeleteDebtAsync(id);
            return null;
        });

    public async Task<EventResult<TreasuryCalotesDto>> SetCommitmentAsync(TreasuryCommitmentInput input, ClaimsPrincipal user)
    {
        if (await RefusalAsync<TreasuryCalotesDto>(user, TreasuryAuthorization.CanManageRecords) is { } refused)
        {
            return refused;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        var start = GovernanceService.TryParseStartYear(input.FiscalYear);
        var debts = await db.MemberDebts.Include(d => d.FiscalYear)
            .Where(d => d.UserId == input.UserId && d.FiscalYear.StartYear == start).ToListAsync();
        if (debts.Count == 0)
        {
            return EventResult<TreasuryCalotesDto>.Fail(EventResultStatus.NotFound);
        }

        foreach (var debt in debts)
        {
            debt.CompromisedUntil = input.Until?.Date;
        }

        await db.SaveChangesAsync();
        return EventResult<TreasuryCalotesDto>.Ok(await CalotesAsync(debts[0].FiscalYear.FiscalYearString, (await MemberAsync(user))!, user));
    }

    public async Task<EventResult<IReadOnlyList<TreasuryPersonDto>>> SearchMembersAsync(string? search, bool forDebts, ClaimsPrincipal user)
    {
        if (await RefusalAsync<IReadOnlyList<TreasuryPersonDto>>(user, TreasuryAuthorization.CanManageRecords) is { } refused)
        {
            return refused;
        }

        var words = EventParticipantsAdminService.Fold(search).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return EventResult<IReadOnlyList<TreasuryPersonDto>>.Ok(Array.Empty<TreasuryPersonDto>());
        }

        await using var db = await _contexts.CreateDbContextAsync();
        // ponytail: every account in memory (~100), as the old pages loaded every user; page it if it grows.
        var members = await db.Users.AsNoTracking().ToListAsync();
        return EventResult<IReadOnlyList<TreasuryPersonDto>>.Ok(members
            .Where(u => !forDebts || !u.IsLeitao())
            .Select(u => (u, who: Person(u, withId: true), text: EventParticipantsAdminService.Fold($"{u.UserName} {u.Nickname} {u.FirstName} {u.LastName} {u.Email}")))
            .Where(x => words.All(w => x.text.Contains(w)))
            .OrderBy(x => x.who.DisplayName, StringComparer.Create(CultureInfo.GetCultureInfo("pt-PT"), true))
            .Take(SearchLimit)
            .Select(x => x.who)
            .ToList());
    }

    // ---------------------------------------------------------------- MBWay

    public async Task<EventResult<TreasuryTransfersDto>> GetTransfersAsync(ClaimsPrincipal user)
    {
        if (await RefusalAsync<TreasuryTransfersDto>(user) is { } refused)
        {
            return refused;
        }

        return EventResult<TreasuryTransfersDto>.Ok(await TransfersAsync(user));
    }

    public async Task<EventResult<TreasuryTransfersDto>> AddTransferAsync(TreasuryTransferInput input, ClaimsPrincipal user)
    {
        if (await RefusalAsync<TreasuryTransfersDto>(user, TreasuryAuthorization.CanManageRecords) is { } refused)
        {
            return refused;
        }

        if (await TransferAsync(input, new MbwayTransfer()) is { } invalid)
        {
            return invalid;
        }

        return EventResult<TreasuryTransfersDto>.Ok(await TransfersAsync(user));
    }

    public async Task<EventResult<TreasuryTransfersDto>> UpdateTransferAsync(int id, TreasuryTransferInput input, ClaimsPrincipal user)
    {
        if (await RefusalAsync<TreasuryTransfersDto>(user, TreasuryAuthorization.CanEditTransfers) is { } refused)
        {
            return refused;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await db.MbwayTransfers.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id) is not { } existing)
        {
            return EventResult<TreasuryTransfersDto>.Fail(EventResultStatus.NotFound);
        }

        if (await TransferAsync(input, new MbwayTransfer { Id = id, FiscalYearId = existing.FiscalYearId }) is { } invalid)
        {
            return invalid;
        }

        return EventResult<TreasuryTransfersDto>.Ok(await TransfersAsync(user));
    }

    public async Task<EventResult<TreasuryTransfersDto>> DeleteTransferAsync(int id, ClaimsPrincipal user)
    {
        if (await RefusalAsync<TreasuryTransfersDto>(user, TreasuryAuthorization.CanEditTransfers) is { } refused)
        {
            return refused;
        }

        var (ok, _) = await _transfers.DeleteTransferAsync(id);
        return ok ? EventResult<TreasuryTransfersDto>.Ok(await TransfersAsync(user)) : EventResult<TreasuryTransfersDto>.Fail(EventResultStatus.NotFound);
    }

    // ---------------------------------------------------------------- Nerba

    public async Task<EventResult<TreasuryNerbaDto>> GetNerbaAsync(ClaimsPrincipal user)
    {
        if (await RefusalAsync<TreasuryNerbaDto>(user) is { } refused)
        {
            return refused;
        }

        return EventResult<TreasuryNerbaDto>.Ok(await NerbaAsync(user));
    }

    public async Task<EventResult<TreasuryNerbaDto>> DeleteNerbaOrdersAsync(int eventId, ClaimsPrincipal user)
    {
        if (await RefusalAsync<TreasuryNerbaDto>(user, TreasuryAuthorization.CanManageRecords) is { } refused)
        {
            return refused;
        }

        if (await NerbaEventAsync(eventId) is null)
        {
            return EventResult<TreasuryNerbaDto>.Fail(EventResultStatus.NotFound);
        }

        await _orders.DeleteByEventIdAsync(eventId);
        return EventResult<TreasuryNerbaDto>.Ok(await NerbaAsync(user));
    }

    public async Task<EventResult<TreasuryNerbaOrdersDto>> GetNerbaOrdersAsync(int eventId, ClaimsPrincipal user)
    {
        if (await RefusalAsync<TreasuryNerbaOrdersDto>(user) is { } refused)
        {
            return refused;
        }

        return await NerbaOrdersAsync(eventId, user) is { } orders
            ? EventResult<TreasuryNerbaOrdersDto>.Ok(orders)
            : EventResult<TreasuryNerbaOrdersDto>.Fail(EventResultStatus.NotFound);
    }

    public async Task<EventResult<TreasuryNerbaOrdersDto>> AddNerbaOrderAsync(int eventId, TreasuryNerbaOrderInput input, ClaimsPrincipal user) =>
        await ChangeOrderAsync(eventId, null, input, user);

    public async Task<EventResult<TreasuryNerbaOrdersDto>> UpdateNerbaOrderAsync(int id, TreasuryNerbaOrderInput input, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var eventId = await db.NerbaOrders.AsNoTracking().Where(o => o.Id == id).Select(o => (int?)o.EventId).SingleOrDefaultAsync();
        return await ChangeOrderAsync(eventId ?? 0, id, input, user);
    }

    public async Task<EventResult<TreasuryNerbaOrdersDto>> DeleteNerbaOrderAsync(int id, ClaimsPrincipal user)
    {
        if (await RefusalAsync<TreasuryNerbaOrdersDto>(user, TreasuryAuthorization.CanManageRecords) is { } refused)
        {
            return refused;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await db.NerbaOrders.AsNoTracking().Where(o => o.Id == id).Select(o => (int?)o.EventId).SingleOrDefaultAsync() is not { } eventId)
        {
            return EventResult<TreasuryNerbaOrdersDto>.Fail(EventResultStatus.NotFound);
        }

        await _orders.DeleteOrderAsync(id);
        return await NerbaOrdersAsync(eventId, user) is { } orders
            ? EventResult<TreasuryNerbaOrdersDto>.Ok(orders)
            : EventResult<TreasuryNerbaOrdersDto>.Fail(EventResultStatus.NotFound);
    }

    // ---------------------------------------------------------------- helpers

    private async Task<TreasuryCalotesDto> CalotesAsync(string? fiscalYear, ApplicationUser me, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var years = (await db.FiscalYears.AsNoTracking().OrderByDescending(f => f.StartYear).ToListAsync()).Select(f => f.FiscalYearString).ToList();
        var current = FiscalYearHelper.GetCurrentFiscalYearString();
        var selected = years.Contains(fiscalYear ?? "") ? fiscalYear : years.Contains(current) ? current : years.FirstOrDefault();
        var all = TreasuryAuthorization.CanView(me, user);
        var manage = all && TreasuryAuthorization.CanManageRecords(user);

        var start = GovernanceService.TryParseStartYear(selected);
        var debts = selected is null
            ? new List<MemberDebt>()
            : await db.MemberDebts.AsNoTracking().Include(d => d.User)
                .Where(d => d.FiscalYear.StartYear == start && (all || d.UserId == me.Id)).ToListAsync();

        var groups = debts.GroupBy(d => d.UserId)
            .Select(g => new TreasuryDebtGroupDto(Person(g.First().User, manage), g.Sum(d => d.AmountOwed), g.Min(d => d.CompromisedUntil),
                g.OrderByDescending(d => d.AmountOwed).ThenBy(d => d.Id).Select(d => new TreasuryDebtDto(d.Id, d.AmountOwed, d.Description)).ToList()))
            .OrderByDescending(g => g.Total).ThenBy(g => g.Member.DisplayName, StringComparer.Create(CultureInfo.GetCultureInfo("pt-PT"), true))
            .ToList();
        return new TreasuryCalotesDto(years, selected, all ? "all" : "own", debts.Sum(d => d.AmountOwed), groups, manage);
    }

    private async Task<EventResult<TreasuryCalotesDto>> ChangeDebtAsync(int id, ClaimsPrincipal user, Func<MemberDebt, Task<(string, string)?>> change)
    {
        if (await RefusalAsync<TreasuryCalotesDto>(user, TreasuryAuthorization.CanManageRecords) is { } refused)
        {
            return refused;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await db.MemberDebts.AsNoTracking().Include(d => d.FiscalYear).FirstOrDefaultAsync(d => d.Id == id) is not { } debt)
        {
            return EventResult<TreasuryCalotesDto>.Fail(EventResultStatus.NotFound);
        }

        if (await change(debt) is { } error)
        {
            return EventResult<TreasuryCalotesDto>.Invalid(error.Item1, error.Item2);
        }

        return EventResult<TreasuryCalotesDto>.Ok(await CalotesAsync(debt.FiscalYear.FiscalYearString, (await MemberAsync(user))!, user));
    }

    private static (string, string)? DebtError(TreasuryDebtInput input)
    {
        if (input.Amount is not { } amount || amount <= 0 || decimal.Round(amount, 2) != amount)
        {
            return ("amount", "O montante deve ser maior que zero, com até duas casas decimais.");
        }

        return input.Description?.Trim().Length > 500 ? ("description", "A descrição não pode exceder 500 caracteres.") : null;
    }

    private async Task<TreasuryTransfersDto> TransfersAsync(ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var transfers = await db.MbwayTransfers.AsNoTracking().Include(t => t.Member).ToListAsync();
        var edit = TreasuryAuthorization.CanEditTransfers(user);
        return new TreasuryTransfersDto(
            transfers.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id)
                .Select(t => new TreasuryTransferDto(t.Id, t.Date, t.Amount, t.TransferTo, t.TransferFrom, t.Phone, t.Description, t.CreatedBy, t.UpdatedBy,
                    t.UpdatedAt, edit && t.Member is not null ? Person(t.Member, withId: true) : null))
                .ToList(),
            transfers.GroupBy(t => t.TransferTo).OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new TreasuryTransferTotalDto(g.Key, g.Sum(t => t.Amount))).ToList(),
            TreasuryAuthorization.CanManageRecords(user),
            edit);
    }

    /// <summary>Validates and saves through the old service; returns the refusal, or null when saved.</summary>
    private async Task<EventResult<TreasuryTransfersDto>?> TransferAsync(TreasuryTransferInput input, MbwayTransfer transfer)
    {
        static EventResult<TreasuryTransfersDto> Invalid(string field, string message) => EventResult<TreasuryTransfersDto>.Invalid(field, message);

        if (input.Date is null)
        {
            return Invalid("date", "A data é obrigatória.");
        }

        if (input.Amount is not { } amount || amount <= 0 || decimal.Round(amount, 2) != amount)
        {
            return Invalid("amount", "O montante deve ser superior a 0€, com até duas casas decimais.");
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == input.MemberUserId) is not { } member)
        {
            return Invalid("memberUserId", "Escolha o membro que recebeu a transferência.");
        }

        if (input.TransferFrom?.Trim().Length > 200)
        {
            return Invalid("transferFrom", "O remetente não pode exceder 200 caracteres.");
        }

        if (input.Phone?.Trim().Length > 20)
        {
            return Invalid("phone", "O telefone não pode exceder 20 caracteres.");
        }

        if (input.Description?.Trim().Length > 500)
        {
            return Invalid("description", "A descrição não pode exceder 500 caracteres.");
        }

        transfer.Date = input.Date.Value.Date;
        transfer.Amount = amount;
        transfer.MemberUserId = member.Id;
        transfer.TransferTo = string.IsNullOrWhiteSpace(member.Nickname) ? member.UserName ?? "" : member.Nickname; // as the old picker
        transfer.TransferFrom = Text(input.TransferFrom);
        transfer.Phone = Text(input.Phone);
        transfer.Description = Text(input.Description);
        var (ok, message) = transfer.Id > 0 ? await _transfers.UpdateTransferAsync(transfer) : await _transfers.AddTransferAsync(transfer);
        return ok ? null : Invalid("form", message);
    }

    private async Task<TreasuryNerbaDto> NerbaAsync(ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var events = await db.Events.AsNoTracking().Where(e => e.Type == EventType.Nerba).ToListAsync();
        var orders = await db.NerbaOrders.AsNoTracking().Select(o => new { o.EventId, o.Stock, o.PricePerUnit }).ToListAsync();
        var today = DateTime.Today;
        var items = events.Select(e => new TreasuryNerbaSummaryDto(NerbaEvent(e), orders.Count(o => o.EventId == e.Id),
            orders.Where(o => o.EventId == e.Id).Sum(o => o.Stock * o.PricePerUnit))).ToList();
        return new TreasuryNerbaDto(
            items.Where(i => (i.Event.EndDate ?? i.Event.Date).Date >= today).OrderBy(i => i.Event.Date).ToList(),
            items.Where(i => (i.Event.EndDate ?? i.Event.Date).Date < today).OrderByDescending(i => i.Event.Date).ToList(),
            TreasuryAuthorization.CanManageRecords(user));
    }

    private async Task<Event?> NerbaEventAsync(int eventId)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        return await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId && e.Type == EventType.Nerba);
    }

    private static List<DateTime> Days(Event e)
    {
        var days = new List<DateTime>();
        if (e.EndDate is { } end && end.Date > e.Date.Date)
        {
            for (var d = e.Date.Date; d <= end.Date; d = d.AddDays(1))
            {
                days.Add(d);
            }
        }

        return days;
    }

    private async Task<TreasuryNerbaOrdersDto?> NerbaOrdersAsync(int eventId, ClaimsPrincipal user)
    {
        if (await NerbaEventAsync(eventId) is not { } e)
        {
            return null;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        var orders = await db.NerbaOrders.AsNoTracking().Where(o => o.EventId == eventId).ToListAsync();
        return new TreasuryNerbaOrdersDto(NerbaEvent(e), Days(e),
            orders.OrderBy(o => o.Item, StringComparer.Create(CultureInfo.GetCultureInfo("pt-PT"), true)).ThenBy(o => o.Id)
                .Select(o => new TreasuryNerbaOrderDto(o.Id, o.Item, o.Type, o.Stock, o.PricePerUnit, o.Stock * o.PricePerUnit, o.OrderDate)).ToList(),
            TreasuryAuthorization.CanManageRecords(user));
    }

    private async Task<EventResult<TreasuryNerbaOrdersDto>> ChangeOrderAsync(int eventId, int? id, TreasuryNerbaOrderInput input, ClaimsPrincipal user)
    {
        static EventResult<TreasuryNerbaOrdersDto> Invalid(string field, string message) => EventResult<TreasuryNerbaOrdersDto>.Invalid(field, message);

        if (await RefusalAsync<TreasuryNerbaOrdersDto>(user, TreasuryAuthorization.CanManageRecords) is { } refused)
        {
            return refused;
        }

        if (await NerbaEventAsync(eventId) is not { } e)
        {
            return EventResult<TreasuryNerbaOrdersDto>.Fail(EventResultStatus.NotFound);
        }

        var item = input.Item?.Trim() ?? "";
        if (item.Length is 0 or > 200)
        {
            return Invalid("item", item.Length == 0 ? "O item é obrigatório." : "O item não pode exceder 200 caracteres.");
        }

        if (input.Type?.Trim().Length > 100)
        {
            return Invalid("type", "O tipo não pode exceder 100 caracteres.");
        }

        if (input.Stock is not > 0)
        {
            return Invalid("stock", "A quantidade deve ser pelo menos 1.");
        }

        if (input.PricePerUnit is not { } price || price < 0 || decimal.Round(price, 2) != price)
        {
            return Invalid("pricePerUnit", "O preço por unidade não pode ser negativo e tem até duas casas decimais.");
        }

        // A multi-day event files each item under one of its days (the first by default, as the old form); one day: none.
        var days = Days(e);
        DateTime? day = days.Count == 0 ? null : input.OrderDate?.Date ?? days[0];
        if (day is { } d && !days.Contains(d))
        {
            return Invalid("orderDate", "Escolha um dos dias do evento.");
        }

        await using var db = await _contexts.CreateDbContextAsync();
        var order = id is { } existing ? await db.NerbaOrders.AsNoTracking().FirstAsync(o => o.Id == existing) : new NerbaOrder { EventId = eventId };
        order.Item = item;
        order.Type = Text(input.Type);
        order.Stock = input.Stock.Value;
        order.PricePerUnit = price;
        order.OrderDate = day;
        var (ok, message) = id is null ? await _orders.AddOrderAsync(order) : await _orders.UpdateOrderAsync(order);
        if (!ok)
        {
            return Invalid("form", message);
        }

        return EventResult<TreasuryNerbaOrdersDto>.Ok((await NerbaOrdersAsync(eventId, user))!);
    }

    private static TreasuryNerbaEventDto NerbaEvent(Event e) =>
        new(e.Id, e.Name, e.Date, e.EndDate, string.IsNullOrWhiteSpace(e.Location) ? null : e.Location);

    private static TreasuryPersonDto Person(ApplicationUser u, bool withId)
    {
        var who = GovernanceService.ToMember(u.Nickname, u.FirstName, u.LastName, u.ImageUrl);
        return new TreasuryPersonDto(withId ? u.Id : null, who.DisplayName, who.FullName, who.AvatarUrl);
    }

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<ApplicationUser?> MemberAsync(ClaimsPrincipal user)
    {
        if (TreasuryAuthorization.UserId(user) is not { } id)
        {
            return null;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
    }

    /// <summary>401 for visitors, 403 unless a treasury member (and, if given, <paramref name="allowed"/>).</summary>
    private async Task<EventResult<T>?> RefusalAsync<T>(ClaimsPrincipal user, Func<ClaimsPrincipal, bool>? allowed = null)
    {
        if (await MemberAsync(user) is not { } me)
        {
            return EventResult<T>.Fail(EventResultStatus.SignInRequired);
        }

        return TreasuryAuthorization.CanView(me, user) && (allowed is null || allowed(user)) ? null : EventResult<T>.Fail(EventResultStatus.Forbidden);
    }
}
