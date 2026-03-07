using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing Nerba orders
/// </summary>
public class NerbaOrderService : INerbaOrderService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public NerbaOrderService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <inheritdoc />
    public async Task<List<NerbaOrder>> GetByReportIdAsync(int reportId, CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.NerbaOrders
            .Where(no => no.ReportId == reportId)
            .Include(no => no.Event)
            .OrderByDescending(no => no.Id)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<NerbaOrder>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.NerbaOrders
            .Include(no => no.Event)
            .OrderByDescending(no => no.Id)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<NerbaOrder?> GetByIdAsync(int orderId, CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.NerbaOrders
            .Include(no => no.Event)
            .FirstOrDefaultAsync(no => no.Id == orderId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<NerbaOrder>> GetByEventIdAsync(int eventId, CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.NerbaOrders
            .Where(no => no.EventId == eventId)
            .Include(no => no.Event)
            .OrderByDescending(no => no.Id)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message)> AddOrderAsync(NerbaOrder order, CancellationToken cancellationToken = default)
    {
        if (order.EventId <= 0)
            return (false, "Evento é obrigatório.");

        try
        {
            using var context = _contextFactory.CreateDbContext();
            context.NerbaOrders.Add(order);
            await context.SaveChangesAsync(cancellationToken);
            return (true, "Encomenda Nerba adicionada com sucesso.");
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao adicionar encomenda: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message)> UpdateOrderAsync(NerbaOrder order, CancellationToken cancellationToken = default)
    {
        try
        {
            using var context = _contextFactory.CreateDbContext();
            var tracked = await context.NerbaOrders.FindAsync(new object[] { order.Id }, cancellationToken);
            if (tracked == null)
                return (false, "Encomenda não encontrada.");

            // Preserve audit fields that SetValues would overwrite with defaults
            var originalCreatedAt = tracked.CreatedAt;
            var originalCreatedBy = tracked.CreatedBy;
            context.Entry(tracked).CurrentValues.SetValues(order);
            tracked.CreatedAt = originalCreatedAt;
            tracked.CreatedBy = originalCreatedBy;
            await context.SaveChangesAsync(cancellationToken);
            return (true, "Encomenda Nerba atualizada com sucesso.");
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao atualizar encomenda: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message)> DeleteByEventIdAsync(int eventId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var context = _contextFactory.CreateDbContext();
            var orders = await context.NerbaOrders
                .Where(o => o.EventId == eventId)
                .ToListAsync(cancellationToken);
            context.NerbaOrders.RemoveRange(orders);
            await context.SaveChangesAsync(cancellationToken);
            return (true, $"{orders.Count} encomenda(s) removida(s) com sucesso.");
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao remover encomendas: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message)> DeleteOrderAsync(int orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var context = _contextFactory.CreateDbContext();
            var order = await context.NerbaOrders.FindAsync(new object[] { orderId }, cancellationToken);
            if (order == null)
                return (false, "Encomenda não encontrada.");

            context.NerbaOrders.Remove(order);
            await context.SaveChangesAsync(cancellationToken);
            return (true, "Encomenda Nerba removida com sucesso.");
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao remover encomenda: {ex.Message}");
        }
    }
}
