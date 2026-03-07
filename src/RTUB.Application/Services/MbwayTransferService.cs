using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing MBWAY transfers
/// </summary>
public class MbwayTransferService : IMbwayTransferService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public MbwayTransferService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <inheritdoc />
    public async Task<List<MbwayTransfer>> GetByFiscalYearAsync(int fiscalYearId, CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.MbwayTransfers
            .Where(mt => mt.FiscalYearId == fiscalYearId)
            .OrderByDescending(mt => mt.Date)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<MbwayTransfer>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.MbwayTransfers
            .Include(mt => mt.Member)
            .OrderByDescending(mt => mt.Date)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message)> AddTransferAsync(MbwayTransfer transfer, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(transfer.MemberUserId))
            return (false, "Membro é obrigatório.");

        try
        {
            using var context = _contextFactory.CreateDbContext();
            context.MbwayTransfers.Add(transfer);
            await context.SaveChangesAsync(cancellationToken);
            return (true, "Transferência MBWAY adicionada com sucesso.");
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao adicionar transferência: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message)> UpdateTransferAsync(MbwayTransfer transfer, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(transfer.MemberUserId))
            return (false, "Membro é obrigatório.");

        try
        {
            using var context = _contextFactory.CreateDbContext();
            var tracked = await context.MbwayTransfers.FindAsync(new object[] { transfer.Id }, cancellationToken);
            if (tracked == null)
                return (false, "Transferência não encontrada.");

            // Preserve audit fields that SetValues would overwrite with defaults
            var originalCreatedAt = tracked.CreatedAt;
            var originalCreatedBy = tracked.CreatedBy;
            context.Entry(tracked).CurrentValues.SetValues(transfer);
            tracked.CreatedAt = originalCreatedAt;
            tracked.CreatedBy = originalCreatedBy;
            await context.SaveChangesAsync(cancellationToken);
            return (true, "Transferência MBWAY atualizada com sucesso.");
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao atualizar transferência: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message)> DeleteTransferAsync(int transferId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var context = _contextFactory.CreateDbContext();
            var transfer = await context.MbwayTransfers.FindAsync(new object[] { transferId }, cancellationToken);
            if (transfer == null)
                return (false, "Transferência não encontrada.");

            context.MbwayTransfers.Remove(transfer);
            await context.SaveChangesAsync(cancellationToken);
            return (true, "Transferência MBWAY removida com sucesso.");
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao remover transferência: {ex.Message}");
        }
    }
}
