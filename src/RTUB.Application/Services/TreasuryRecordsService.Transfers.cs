// Treasury records: MBWay transfers.
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

public sealed partial class TreasuryRecordsService
{
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

    /// <summary>Validates and saves through the transfer service; returns the refusal, or null when saved.</summary>
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
        transfer.TransferTo = string.IsNullOrWhiteSpace(member.Nickname) ? member.UserName ?? "" : member.Nickname; // as the picker shows it
        transfer.TransferFrom = Text(input.TransferFrom);
        transfer.Phone = Text(input.Phone);
        transfer.Description = Text(input.Description);
        var (ok, message) = transfer.Id > 0 ? await _transfers.UpdateTransferAsync(transfer) : await _transfers.AddTransferAsync(transfer);
        return ok ? null : Invalid("form", message);
    }
}
