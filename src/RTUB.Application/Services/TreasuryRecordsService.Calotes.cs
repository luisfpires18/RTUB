// Treasury records: calotes debts (list, add, update, member search).
using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

public sealed partial class TreasuryRecordsService
{
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

        // A new debt joins the member's commitment of the year.
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
        // ponytail: every account in memory (~100); page it if it grows.
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
}
