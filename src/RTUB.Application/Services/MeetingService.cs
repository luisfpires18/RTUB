using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services;

/// <summary>
/// Meeting service implementation using Repository pattern
/// Contains business logic for meeting operations with Veterano visibility filtering
/// Note: Still uses ApplicationDbContext for Veterano filtering due to ApplicationUser dependency
/// This is a pragmatic tradeoff - full abstraction would require IUserRepository
/// </summary>
public class MeetingService : IMeetingService
{
    private readonly IMeetingRepository _meetingRepository;
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ApplicationDbContext _context;
    private readonly IPushNotificationFactory _pushNotificationFactory;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MeetingService(
        IMeetingRepository meetingRepository,
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IPushNotificationFactory pushNotificationFactory,
        IPushNotificationService pushNotificationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _meetingRepository = meetingRepository;
        _contextFactory = contextFactory;
        _context = contextFactory.CreateDbContext();
        _pushNotificationFactory = pushNotificationFactory;
        _pushNotificationService = pushNotificationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<IEnumerable<Meeting>> GetAllMeetingsAsync(string? searchTerm, int pageNumber, int pageSize, string userId)
    {
        var query = _context.Meetings
            .AsNoTracking()
            .AsQueryable();

        // Apply visibility filtering for Veterano meetings
        query = await ApplyVeteranoFilterAsync(query, userId);

        // Apply search filter using WhereIf extension
        query = query.WhereIf(!string.IsNullOrWhiteSpace(searchTerm),
            m => m.Title.Contains(searchTerm!, StringComparison.OrdinalIgnoreCase) ||
                 m.Statement.Contains(searchTerm!, StringComparison.OrdinalIgnoreCase));

        // Order by date - upcoming first, then past
        var today = DateTime.UtcNow.Date;
        query = query.OrderBy(m => m.Date >= today ? 0 : 1)
                     .ThenBy(m => m.Date >= today ? m.Date : DateTime.MaxValue)
                     .ThenByDescending(m => m.Date < today ? m.Date : DateTime.MinValue);

        // Apply pagination using extension method
        return await query
            .Include(m => m.Organizer)
            .Include(m => m.TunoRepresentative)
            .PaginateAsync(pageNumber, pageSize);
    }

    public async Task<Meeting?> GetMeetingByIdAsync(int id, string userId)
    {
        var meeting = await _context.Meetings
            .AsNoTracking()
            .Include(m => m.Organizer)
            .Include(m => m.TunoRepresentative)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (meeting == null)
            return null;

        // Load user once and cache for visibility checks
        ApplicationUser? user = null;

        // Check if user has permission to view this meeting
        if (meeting.Type == MeetingType.ConselhoVeteranos)
        {
            user = await _context.Users
                .AsNoTracking()
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
            // Reuse cached user if already loaded
            if (user == null)
            {
                user = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.Id == userId)
                    .FirstOrDefaultAsync();
            }

            if (user != null && user.IsLeitao())
                return null;
        }

        return meeting;
    }

    public async Task<Meeting> CreateMeetingAsync(Meeting meeting)
    {
        var createdMeeting = await _meetingRepository.AddAsync(meeting);

        // Send push notification to appropriate users based on meeting type
        try
        {
            var baseUrl = GetBaseUrl();
            var notification = _pushNotificationFactory.CreateMeetingNotification(createdMeeting, isReminder: false, baseUrl);

            // Get users based on meeting type
            var eligibleUserIds = await GetEligibleUsersForMeeting(createdMeeting.Type);

            // Send to each eligible user
            foreach (var userId in eligibleUserIds)
            {
                await _pushNotificationService.SendToUserAsync(userId, notification);
            }
        }
        catch
        {
            // Log error but don't fail the operation
            // Notification is secondary to the main operation
            // Note: Exception is intentionally swallowed - notification failure should not break meeting creation
        }

        return createdMeeting;
    }

    public async Task UpdateMeetingAsync(Meeting meeting)
    {
        var existingMeeting = await _meetingRepository.GetByIdOrThrowAsync(meeting.Id);

        existingMeeting.Type = meeting.Type;
        existingMeeting.Title = meeting.Title;
        existingMeeting.Date = meeting.Date;
        existingMeeting.Location = meeting.Location;
        existingMeeting.Statement = meeting.Statement;
        existingMeeting.OrganizerUserId = meeting.OrganizerUserId;
        existingMeeting.TunoRepresentativeUserId = meeting.TunoRepresentativeUserId;
        existingMeeting.IsCancelled = meeting.IsCancelled;
        existingMeeting.CancellationReason = meeting.CancellationReason;

        await _meetingRepository.UpdateAsync(existingMeeting);
    }

    public async Task DeleteMeetingAsync(int id)
    {
        var meeting = await _meetingRepository.GetByIdOrThrowAsync(id);

        await _meetingRepository.DeleteAsync(meeting);
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

            // Allow CV meetings for Veterans, Tunossauros, Magister position holders,
            // AND users who are designated as TunoRepresentative for a specific meeting
            if (role != "VETERANO" && role != "TUNOSSAURO" && !hasMagisterPosition)
            {
                // Filter CV meetings but allow access to specific meetings where this user
                // is designated as the Tuno Representative (e.g., a TUNO member chosen to
                // attend and participate in a particular CV meeting)
                query = query.Where(m => m.Type != MeetingType.ConselhoVeteranos || m.TunoRepresentativeUserId == userId);
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

    /// <summary>
    /// Gets list of user IDs eligible to receive notifications for a meeting based on type
    /// </summary>
    private async Task<List<string>> GetEligibleUsersForMeeting(MeetingType meetingType)
    {
        // Special case: ConselhoVeteranos uses client-side filtering
        if (meetingType == MeetingType.ConselhoVeteranos)
        {
            // Load from DB asynchronously with AsNoTracking to prevent accumulating tracked entities
            // which can cause issues during subsequent SaveChangesAsync calls
            var users = await _context.Users.AsNoTracking().ToListAsync();

            // Now filter in memory (CurrentRole and Positions can be unmapped)
            return users
                .Where(u =>
                    u.CurrentRole == "VETERANO" ||
                    u.CurrentRole == "TUNOSSAURO" ||
                    (u.Positions != null && u.Positions.Contains(Position.Magister)))
                .Select(u => u.Id)
                .ToList(); // sync, in-memory
        }

        // All other meeting types can stay as EF queries
        IQueryable<ApplicationUser> query = _context.Users;

        switch (meetingType)
        {
            case MeetingType.AssembleiaGeralOrdinaria:
            case MeetingType.AssembleiaGeralExtraordinaria:
                query = query.Where(u => !u.Categories.Contains(MemberCategory.Leitao));
                break;

            default:
                // All users
                break;
        }

        return await query
            .Select(u => u.Id)
            .ToListAsync();
    }

    private string GetBaseUrl()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request != null)
        {
            return $"{request.Scheme}://{request.Host}";
        }
        return "https://rtub.pt"; // Fallback
    }
}
