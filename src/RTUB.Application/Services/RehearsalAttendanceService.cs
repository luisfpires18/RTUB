using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;

namespace RTUB.Application.Services;

/// <summary>
/// Rehearsal attendance service implementation
/// Contains business logic for attendance tracking
/// </summary>
public class RehearsalAttendanceService : IRehearsalAttendanceService
{
    private readonly ApplicationDbContext _context;

    public RehearsalAttendanceService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RehearsalAttendance?> GetAttendanceByIdAsync(int id)
    {
        return await _context.RehearsalAttendances
            .Include(a => a.Rehearsal)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<IEnumerable<RehearsalAttendance>> GetAttendancesByRehearsalIdAsync(int rehearsalId)
    {
        return await _context.RehearsalAttendances
            .Where(a => a.RehearsalId == rehearsalId)
            .OrderBy(a => a.CheckedInAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<RehearsalAttendance>> GetAttendancesByUserIdAsync(string userId)
    {
        return await _context.RehearsalAttendances
            .Include(a => a.Rehearsal)
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.Rehearsal!.Date)
            .ToListAsync();
    }

    public async Task<RehearsalAttendance> MarkAttendanceAsync(int rehearsalId, string userId, bool willAttend = true, InstrumentType? instrument = null, string? notes = null)
    {
        // Check if attendance already exists
        var existing = await _context.RehearsalAttendances
            .FirstOrDefaultAsync(a => a.RehearsalId == rehearsalId && a.UserId == userId);
        
        if (existing != null)
        {
            // Update existing attendance
            existing.WillAttend = willAttend;
            if (instrument.HasValue)
                existing.UpdateInstrument(instrument);
            // Always update notes, even if empty (allows clearing notes)
            existing.Notes = notes;
            
            _context.RehearsalAttendances.Update(existing);
            await _context.SaveChangesAsync();
            return existing;
        }

        // Create new attendance (defaults to pending - Attended = false)
        var attendance = RehearsalAttendance.Create(rehearsalId, userId, instrument);
        attendance.WillAttend = willAttend;
        // Always set notes, even if empty
        attendance.Notes = notes;
        
        _context.RehearsalAttendances.Add(attendance);
        await _context.SaveChangesAsync();
        return attendance;
    }

    public async Task UpdateAttendanceAsync(int id, bool attended, InstrumentType? instrument = null)
    {
        var attendance = await _context.RehearsalAttendances.FindAsync(id);
        if (attendance == null)
            throw new InvalidOperationException($"Attendance with ID {id} not found");

        attendance.MarkAttendance(attended);
        if (instrument.HasValue)
            attendance.UpdateInstrument(instrument);
        
        _context.RehearsalAttendances.Update(attendance);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAttendanceAsync(int id)
    {
        var attendance = await _context.RehearsalAttendances.FindAsync(id);
        if (attendance == null)
            throw new InvalidOperationException($"Attendance with ID {id} not found");

        _context.RehearsalAttendances.Remove(attendance);
        await _context.SaveChangesAsync();
    }

    public async Task<int> GetUserAttendanceCountAsync(string userId, DateTime startDate, DateTime endDate)
    {
        return await _context.RehearsalAttendances
            .Include(a => a.Rehearsal)
            .Where(a => a.UserId == userId && 
                       a.Attended && 
                       a.Rehearsal!.Date >= startDate.Date && 
                       a.Rehearsal.Date <= endDate.Date)
            .CountAsync();
    }

    public async Task<Dictionary<string, int>> GetAttendanceStatsAsync(DateTime startDate, DateTime endDate)
    {
        var attendances = await _context.RehearsalAttendances
            .Include(a => a.Rehearsal)
            .Include(a => a.User)
            .Where(a => a.Attended && 
                       a.Rehearsal!.Date >= startDate.Date && 
                       a.Rehearsal.Date <= endDate.Date)
            .GroupBy(a => a.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        return attendances.ToDictionary(a => a.UserId, a => a.Count);
    }
}
