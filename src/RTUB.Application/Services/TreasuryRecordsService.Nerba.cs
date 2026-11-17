// Treasury records: Nerba events and orders.
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
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

public sealed partial class TreasuryRecordsService
{
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

    private async Task<TreasuryNerbaDto> NerbaAsync(ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var events = await db.Events.AsNoTracking().Where(e => e.Type == EventType.Nerba).ToListAsync();
        var orders = await db.NerbaOrders.AsNoTracking().Select(o => new { o.EventId, o.Stock, o.PricePerUnit }).ToListAsync();
        var today = PortugalTime.Today(_time);
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
}
