using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

public class TransportationRepository : Repository<Transportation>, ITransportationRepository
{
    public TransportationRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<Transportation?> GetByPostIdAsync(int postId)
    {
        using var context = CreateContext();
        return await context.Set<Transportation>()
            .AsNoTracking()
            .Include(t => t.Passengers)
                .ThenInclude(p => p.Passenger)
            .FirstOrDefaultAsync(t => t.PostId == postId);
    }

    public async Task AddPassengerAsync(TransportationPassenger passenger)
    {
        using var context = CreateContext();
        context.Set<TransportationPassenger>().Add(passenger);
        await context.SaveChangesAsync();
    }

    public async Task RemovePassengerAsync(int transportationId, string passengerId)
    {
        using var context = CreateContext();
        var passenger = await context.Set<TransportationPassenger>()
            .FirstOrDefaultAsync(p => p.TransportationId == transportationId && p.PassengerId == passengerId);
        if (passenger != null)
        {
            context.Set<TransportationPassenger>().Remove(passenger);
            await context.SaveChangesAsync();
        }
    }
}
