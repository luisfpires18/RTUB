using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for Bet entity
/// Provides bet-specific data access operations
/// </summary>
public class BetRepository : Repository<Bet>, IBetRepository
{
    public BetRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Bet>> GetFutureBetsAsync()
    {
        // Using local server time for consistency with Events
        var now = DateTime.Now;
        return await _dbSet
            .AsNoTracking()
            .Where(b => b.DateTime > now && !b.IsCancelled)
            .OrderBy(b => b.DateTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<Bet>> GetPastBetsAsync()
    {
        // Using local server time for consistency with Events
        var now = DateTime.Now;
        return await _dbSet
            .AsNoTracking()
            .Where(b => b.DateTime <= now)
            .OrderByDescending(b => b.DateTime)
            .ToListAsync();
    }

    public async Task<Bet?> GetBetWithOptionsAsync(int id)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(b => b.Options)
                .ThenInclude(o => o.MemberA)
            .Include(b => b.Options)
                .ThenInclude(o => o.MemberB)
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<Bet?> GetBetWithDetailsAsync(int id)
    {
        // Not using AsNoTracking() because the Bet entity will be modified in ResolveBetAsync
        return await _dbSet
            .Include(b => b.Options)
                .ThenInclude(o => o.MemberA)
            .Include(b => b.Options)
                .ThenInclude(o => o.MemberB)
            .Include(b => b.UserBets)
                .ThenInclude(ub => ub.User)
            .Include(b => b.UserBets)
                .ThenInclude(ub => ub.BetOption)
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task DeleteByIdDirectAsync(int id)
    {
        // Use raw SQL to delete the bet and all related entities in correct order
        // This bypasses EF Core entirely and handles FK constraints properly
        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        
        try
        {
            if (!wasOpen)
                await connection.OpenAsync();
            
            using var transaction = await connection.BeginTransactionAsync();
            
            try
            {
                // Step 1: Delete UserBets (they reference BetOptions)
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = (System.Data.Common.DbTransaction)transaction;
                    cmd.CommandText = "DELETE FROM \"UserBets\" WHERE \"BetId\" = @id";
                    var param = cmd.CreateParameter();
                    param.ParameterName = "@id";
                    param.Value = id;
                    cmd.Parameters.Add(param);
                    await cmd.ExecuteNonQueryAsync();
                }
                
                // Step 2: Delete BetOptions
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = (System.Data.Common.DbTransaction)transaction;
                    cmd.CommandText = "DELETE FROM \"BetOptions\" WHERE \"BetId\" = @id";
                    var param = cmd.CreateParameter();
                    param.ParameterName = "@id";
                    param.Value = id;
                    cmd.Parameters.Add(param);
                    await cmd.ExecuteNonQueryAsync();
                }
                
                // Step 3: Delete BetComments
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = (System.Data.Common.DbTransaction)transaction;
                    cmd.CommandText = "DELETE FROM \"BetComments\" WHERE \"BetId\" = @id";
                    var param = cmd.CreateParameter();
                    param.ParameterName = "@id";
                    param.Value = id;
                    cmd.Parameters.Add(param);
                    await cmd.ExecuteNonQueryAsync();
                }
                
                // Step 4: Finally delete the Bet
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = (System.Data.Common.DbTransaction)transaction;
                    cmd.CommandText = "DELETE FROM \"Bets\" WHERE \"Id\" = @id";
                    var param = cmd.CreateParameter();
                    param.ParameterName = "@id";
                    param.Value = id;
                    cmd.Parameters.Add(param);
                    await cmd.ExecuteNonQueryAsync();
                }
                
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        finally
        {
            if (!wasOpen && connection.State == System.Data.ConnectionState.Open)
                await connection.CloseAsync();
        }
    }
}
