using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;
using RTUB.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace RTUB.Application.Services;

/// <summary>
/// Rehearsal attendance service implementation
/// Contains business logic for attendance tracking
/// </summary>
public class RehearsalAttendanceService : IRehearsalAttendanceService
{
    private readonly IRehearsalAttendanceRepository _attendanceRepository;

    public RehearsalAttendanceService(IRehearsalAttendanceRepository attendanceRepository)
    {
        _attendanceRepository = attendanceRepository;
    }

    public async Task<RehearsalAttendance?> GetAttendanceByIdAsync(int id)
    {
        var attendance = await _attendanceRepository.GetByIdAsync(id);
        if (attendance != null)
        {
            // Load the rehearsal navigation property
            await _attendanceRepository.Query()
                .Include(a => a.Rehearsal)
                .FirstOrDefaultAsync(a => a.Id == id);
        }
        return attendance;
    }

    public async Task<IEnumerable<RehearsalAttendance>> GetAttendancesByRehearsalIdAsync(int rehearsalId)
    {
        return await _attendanceRepository.GetAttendancesByRehearsalIdAsync(rehearsalId);
    }

    public async Task<IEnumerable<RehearsalAttendance>> GetAttendancesByUserIdAsync(string userId)
    {
        return await _attendanceRepository.GetAttendancesByUserIdAsync(userId);
    }

    public async Task<RehearsalAttendance> MarkAttendanceAsync(int rehearsalId, string userId, bool willAttend = true, InstrumentType? instrument = null, string? notes = null, string? otherInstruments = null)
    {
        // Check if attendance already exists
        var existing = await _attendanceRepository.GetAttendanceByRehearsalAndUserAsync(rehearsalId, userId);
        
        if (existing != null)
        {
            // Update existing attendance
            existing.WillAttend = willAttend;
            if (instrument.HasValue)
                existing.UpdateInstrument(instrument);
            // Always update notes, even if empty (allows clearing notes)
            existing.Notes = notes;
            // Update other instruments
            existing.OtherInstruments = otherInstruments;
            
            await _attendanceRepository.UpdateAsync(existing);
            return existing;
        }

        // Create new attendance (defaults to pending - Attended = false)
        var attendance = RehearsalAttendance.Create(rehearsalId, userId, instrument);
        attendance.WillAttend = willAttend;
        // Always set notes, even if empty
        attendance.Notes = notes;
        // Set other instruments
        attendance.OtherInstruments = otherInstruments;
        
        return await _attendanceRepository.AddAsync(attendance);
    }

    public async Task UpdateAttendanceAsync(int id, bool attended, InstrumentType? instrument = null)
    {
        var attendance = await _attendanceRepository.GetByIdAsync(id);
        if (attendance == null)
            throw new EntityNotFoundException(nameof(RehearsalAttendance), id);

        attendance.MarkAttendance(attended);
        if (instrument.HasValue)
            attendance.UpdateInstrument(instrument);
        
        await _attendanceRepository.UpdateAsync(attendance);
    }

    public async Task DeleteAttendanceAsync(int id)
    {
        var attendance = await _attendanceRepository.GetByIdAsync(id);
        if (attendance == null)
            throw new EntityNotFoundException(nameof(RehearsalAttendance), id);
        
        await _attendanceRepository.DeleteAsync(id);
    }

    public async Task<int> GetUserAttendanceCountAsync(string userId, DateTime startDate, DateTime endDate)
    {
        return await _attendanceRepository.Query()
            .Include(a => a.Rehearsal)
            .Where(a => a.UserId == userId && 
                       a.Attended && 
                       a.Rehearsal!.Date >= startDate.Date && 
                       a.Rehearsal.Date <= endDate.Date)
            .CountAsync();
    }

    public async Task<Dictionary<string, int>> GetAttendanceStatsAsync(DateTime startDate, DateTime endDate)
    {
        var attendances = await _attendanceRepository.Query()
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
