using RTUB.Application.Interfaces;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Exceptions;
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
        var query = _context.Meetings
            .AsNoTracking()
            .AsQueryable();
        
        // Apply visibility filtering for Veterano meetings
        query = await ApplyVeteranoFilterAsync(query, userId);
        
        // Apply search filter
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(m => 
                m.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) || 
                m.Statement.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
        }
        
        // Order by date - upcoming first, then past
        var today = DateTime.UtcNow.Date;
        query = query.OrderBy(m => m.Date >= today ? 0 : 1)
                     .ThenBy(m => m.Date >= today ? m.Date : DateTime.MaxValue)
                     .ThenByDescending(m => m.Date < today ? m.Date : DateTime.MinValue);
        
        // Apply pagination
        return await query
            .Include(m => m.Organizer)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<Meeting?> GetMeetingByIdAsync(int id, string userId)
    {
        var meeting = await _context.Meetings
            .AsNoTracking()
            .Include(m => m.Organizer)
            .FirstOrDefaultAsync(m => m.Id == id);
        
        if (meeting == null)
            return null;
        
        // Check if user has permission to view this meeting
        if (meeting.Type == MeetingType.ConselhoVeteranos)
        {
            var user = await _context.Users
                .Where(u => u.Id == userId)
                .FirstOrDefaultAsync();
            
            if (user == null)
                return null;
                
            // Use CurrentRole property instead of Categories to avoid JSON deserialization issues
            var role = user.CurrentRole;
            var hasMagisterPosition = user.Positions != null && user.Positions.Contains(Position.Magister);
            
            // Allow CV meetings for Veterans, Tunossauros, and Magister position holders
            if (role != "VETERANO" && role != "TUNOSSAURO" && !hasMagisterPosition)
                return null;
        }
        
        // Check if user is Leitão trying to access Assembleia Geral meetings
        if (meeting.Type == MeetingType.AssembleiaGeralOrdinaria || 
            meeting.Type == MeetingType.AssembleiaGeralExtraordinaria)
        {
            var user = await _context.Users
                .Where(u => u.Id == userId)
                .FirstOrDefaultAsync();
            
            if (user != null && user.IsLeitao())
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
            throw new EntityNotFoundException(nameof(Meeting), meeting.Id);
        
        existingMeeting.Type = meeting.Type;
        existingMeeting.Title = meeting.Title;
        existingMeeting.Date = meeting.Date;
        existingMeeting.Location = meeting.Location;
        existingMeeting.Statement = meeting.Statement;
        existingMeeting.OrganizerUserId = meeting.OrganizerUserId;
        existingMeeting.IsCancelled = meeting.IsCancelled;
        existingMeeting.CancellationReason = meeting.CancellationReason;
        
        await _context.SaveChangesAsync();
    }

    public async Task DeleteMeetingAsync(int id)
    {
        var meeting = await _context.Meetings.FindAsync(id);
        if (meeting == null)
            throw new EntityNotFoundException(nameof(Meeting), id);
        
        _context.Meetings.Remove(meeting);
        await _context.SaveChangesAsync();
    }

    public async Task<int> GetTotalCountAsync(string? searchTerm, string userId)
    {
        var query = _context.Meetings
            .AsNoTracking()
            .AsQueryable();
        
        // Apply visibility filtering for Veterano meetings
        query = await ApplyVeteranoFilterAsync(query, userId);
        
        // Apply search filter
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(m => 
                m.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) || 
                m.Statement.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
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
        var user = await _context.Users
            .Where(u => u.Id == userId)
            .FirstOrDefaultAsync();
        
        // If user is not found, filter out CV meetings
        // If user is Veterano/Tunossauro OR has Magister position, they can see CV meetings
        if (user == null)
        {
            query = query.Where(m => m.Type != MeetingType.ConselhoVeteranos);
        }
        else
        {
            var role = user.CurrentRole;
            var hasMagisterPosition = user.Positions != null && user.Positions.Contains(Position.Magister);
            
            // Allow CV meetings for Veterans, Tunossauros, and Magister position holders
            if (role != "VETERANO" && role != "TUNOSSAURO" && !hasMagisterPosition)
            {
                query = query.Where(m => m.Type != MeetingType.ConselhoVeteranos);
            }
            
            // Filter out Assembleia Geral meetings if user is Leitão (not an associated member)
            if (user.IsLeitao())
            {
                query = query.Where(m => 
                    m.Type != MeetingType.AssembleiaGeralOrdinaria && 
                    m.Type != MeetingType.AssembleiaGeralExtraordinaria);
            }
        }
        
        return query;
    }
}
