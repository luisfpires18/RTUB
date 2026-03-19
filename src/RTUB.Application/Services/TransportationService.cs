using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

public class TransportationService : ITransportationService
{
    private readonly ITransportationRepository _repository;
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public TransportationService(
        ITransportationRepository repository,
        IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _repository = repository;
        _contextFactory = contextFactory;
    }

    public Task<Transportation?> GetByPostIdAsync(int postId)
        => _repository.GetByPostIdAsync(postId);

    public async Task<Transportation> CreateForPostAsync(int postId, string vehicleDescription, int totalSeats, string? notes)
    {
        var transportation = Transportation.Create(postId, vehicleDescription, totalSeats, notes);
        await _repository.AddAsync(transportation);
        return transportation;
    }

    public async Task UpdateAsync(int id, string vehicleDescription, int totalSeats, string? notes)
    {
        using var context = _contextFactory.CreateDbContext();
        var transportation = await context.Set<Transportation>().FindAsync(id)
            ?? throw new KeyNotFoundException($"Transportation {id} not found");

        transportation.Update(vehicleDescription, totalSeats, notes);
        await context.SaveChangesAsync();
    }

    public async Task DeleteByPostIdAsync(int postId)
    {
        using var context = _contextFactory.CreateDbContext();
        var transportation = await context.Set<Transportation>()
            .FirstOrDefaultAsync(t => t.PostId == postId);
        if (transportation != null)
        {
            context.Set<Transportation>().Remove(transportation);
            await context.SaveChangesAsync();
        }
    }

    public async Task AddPassengerAsync(int transportationId, string userId)
    {
        using var context = _contextFactory.CreateDbContext();
        var transportation = await context.Set<Transportation>()
            .Include(t => t.Passengers)
            .FirstOrDefaultAsync(t => t.Id == transportationId)
            ?? throw new KeyNotFoundException($"Transportation {transportationId} not found");

        if (transportation.Passengers.Any(p => p.PassengerId == userId))
            throw new InvalidOperationException("Este membro já está registado nesta viatura.");

        if (transportation.Passengers.Count >= transportation.TotalSeats)
            throw new InvalidOperationException("Não há lugares disponíveis nesta viatura.");

        var passenger = TransportationPassenger.Create(transportationId, userId);
        await _repository.AddPassengerAsync(passenger);
    }

    public Task RemovePassengerAsync(int transportationId, string userId)
        => _repository.RemovePassengerAsync(transportationId, userId);

    public async Task<IEnumerable<ApplicationUser>> GetAllMembersAsync()
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.Users
            .AsNoTracking()
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToListAsync();
    }
}
