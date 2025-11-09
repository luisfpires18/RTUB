using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;


namespace RTUB.Application.Services;

/// <summary>
/// Enrollment service implementation
/// Contains business logic for enrollment operations
/// Follows Single Responsibility and Dependency Inversion principles
/// </summary>
public class EnrollmentService : IEnrollmentService
{
    private readonly ApplicationDbContext _context;

    public EnrollmentService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Enrollment?> GetEnrollmentByIdAsync(int id)
    {
        return await _context.Enrollments.FindAsync(id);
    }

    public async Task<IEnumerable<Enrollment>> GetAllEnrollmentsAsync()
    {
        return await _context.Enrollments.ToListAsync();
    }

    public async Task<IEnumerable<Enrollment>> GetEnrollmentsByEventIdAsync(int eventId)
    {
        return await _context.Enrollments
            .Where(e => e.EventId == eventId)
            .Include(e => e.User)
            .ToListAsync();
    }

    public async Task<IEnumerable<Enrollment>> GetEnrollmentsByUserIdAsync(string userId)
    {
        return await _context.Enrollments
            .Where(e => e.UserId == userId)
            .Include(e => e.Event)
            .ToListAsync();
    }

    public async Task<Enrollment> CreateEnrollmentAsync(string userId, int eventId, InstrumentType? instrument = null, string? notes = null, bool willAttend = true)
    {
        var enrollment = Enrollment.Create(userId, eventId);
        enrollment.Instrument = instrument;
        enrollment.Notes = notes;
        enrollment.WillAttend = willAttend;
        _context.Enrollments.Add(enrollment);
        await _context.SaveChangesAsync();
        return enrollment;
    }

    public async Task DeleteEnrollmentAsync(int id)
    {
        var enrollment = await _context.Enrollments.FindAsync(id);
        if (enrollment == null)
            throw new InvalidOperationException($"Enrollment with ID {id} not found");

        _context.Enrollments.Remove(enrollment);
        await _context.SaveChangesAsync();
    }
}
