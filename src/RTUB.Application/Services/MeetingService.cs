using RTUB.Application.Interfaces;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Application.Extensions;
using Microsoft.EntityFrameworkCore;

namespace RTUB.Application.Services;

/// <summary>
/// Meeting service implementation
/// Contains business logic for meeting operations with Veterano visibility filtering
/// </summary>
public class MeetingService : IMeetingService
{
    private readonly ApplicationDbContext _context;

    public MeetingService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Meeting>> GetAllMeetingsAsync(string? searchTerm, int pageNumber, int pageSize, string userId)
    {
        var query = _context.Meetings.AsQueryable();
        
        // Apply visibility filtering for Veterano meetings
        query = await ApplyVeteranoFilterAsync(query, userId);
        
        // Apply search filter
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var lowerSearchTerm = searchTerm.ToLower();
            query = query.Where(m => 
                m.Title.ToLower().Contains(lowerSearchTerm) || 
                m.Statement.ToLower().Contains(lowerSearchTerm));
        }
        
        // Order by date - upcoming first, then past
        var today = DateTime.UtcNow.Date;
        query = query.OrderBy(m => m.Date >= today ? 0 : 1)
                     .ThenBy(m => m.Date >= today ? m.Date : DateTime.MaxValue)
                     .ThenByDescending(m => m.Date < today ? m.Date : DateTime.MinValue);
        
        // Apply pagination
        return await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<Meeting?> GetMeetingByIdAsync(int id, string userId)
    {
        var meeting = await _context.Meetings.FindAsync(id);
        
        if (meeting == null)
            return null;
        
        // Check if user has permission to view this meeting
        if (meeting.Type == MeetingType.ConselhoVeteranos)
        {
            // Use FirstOrDefaultAsync to ensure we get a fully materialized user object
            var user = await _context.Users
                .Where(u => u.Id == userId)
                .FirstOrDefaultAsync();
            if (user == null || (!user.IsVeterano() && !user.IsTunossauro()))
                return null;
        }
        
        return meeting;
    }

    public async Task<Meeting> CreateMeetingAsync(Meeting meeting)
    {
        _context.Meetings.Add(meeting);
        await _context.SaveChangesAsync();
        return meeting;
    }

    public async Task UpdateMeetingAsync(Meeting meeting)
    {
        var existingMeeting = await _context.Meetings.FindAsync(meeting.Id);
        if (existingMeeting == null)
            throw new InvalidOperationException($"Meeting with ID {meeting.Id} not found");
        
        existingMeeting.Type = meeting.Type;
        existingMeeting.Title = meeting.Title;
        existingMeeting.Date = meeting.Date;
        existingMeeting.Location = meeting.Location;
        existingMeeting.Statement = meeting.Statement;
        
        _context.Meetings.Update(existingMeeting);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteMeetingAsync(int id)
    {
        var meeting = await _context.Meetings.FindAsync(id);
        if (meeting == null)
            throw new InvalidOperationException($"Meeting with ID {id} not found");
        
        _context.Meetings.Remove(meeting);
        await _context.SaveChangesAsync();
    }

    public async Task<int> GetTotalCountAsync(string? searchTerm, string userId)
    {
        var query = _context.Meetings.AsQueryable();
        
        // Apply visibility filtering for Veterano meetings
        query = await ApplyVeteranoFilterAsync(query, userId);
        
        // Apply search filter
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var lowerSearchTerm = searchTerm.ToLower();
            query = query.Where(m => 
                m.Title.ToLower().Contains(lowerSearchTerm) || 
                m.Statement.ToLower().Contains(lowerSearchTerm));
        }
        
        return await query.CountAsync();
    }

    /// <summary>
    /// Applies Veterano visibility filtering to the query
    /// Filters out CV meetings if user is not Veterano or Tunossauro
    /// </summary>
    private async Task<IQueryable<Meeting>> ApplyVeteranoFilterAsync(IQueryable<Meeting> query, string userId)
    {
        // Use FirstOrDefaultAsync to ensure we get a fully materialized user object
        // FindAsync may not properly deserialize JSON properties like Categories
        var user = await _context.Users
            .Where(u => u.Id == userId)
            .FirstOrDefaultAsync();
        
        // If user is not found or not Veterano/Tunossauro, filter out CV meetings
        if (user == null || (!user.IsVeterano() && !user.IsTunossauro()))
        {
            query = query.Where(m => m.Type != MeetingType.ConselhoVeteranos);
        }
        
        return query;
    }
}
