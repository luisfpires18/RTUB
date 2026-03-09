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
        _pushNotificationFactory = pushNotificationFactory;
        _pushNotificationService = pushNotificationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<IEnumerable<Meeting>> GetAllMeetingsAsync(string? searchTerm, int pageNumber, int pageSize, string userId)
    {
        var ctx = _contextFactory.CreateDbContext();
        var query = ctx.Meetings
            .AsNoTracking()
            .AsQueryable();

        // Apply visibility filtering for Veterano meetings
        query = await ApplyVeteranoFilterAsync(ctx, query, userId);

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
        var ctx = _contextFactory.CreateDbContext();
        var meeting = await ctx.Meetings
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
            user = await ctx.Users
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
                user = await ctx.Users
                    .AsNoTracking()
                    .Where(u => u.Id == userId)
                    .FirstOrDefaultAsync();
            }

            if (user != null && user.IsLeitao())
                return null;
        }

        // Check if user has permission to view Direção meetings
        if (meeting.Type == MeetingType.ReuniaoDirecao)
        {
            if (user == null)
            {
                user = await ctx.Users
                    .AsNoTracking()
                    .Where(u => u.Id == userId)
                    .FirstOrDefaultAsync();
            }

            if (user == null)
                return null;

            var hasDirecaoPosition = user.Positions != null &&
                (user.Positions.Contains(Position.Magister) ||
                 user.Positions.Contains(Position.ViceMagister) ||
                 user.Positions.Contains(Position.Secretario) ||
                 user.Positions.Contains(Position.PrimeiroTesoureiro) ||
                 user.Positions.Contains(Position.SegundoTesoureiro));

            if (!hasDirecaoPosition)
            {
                // Check if user is Admin with Tuno category
                var isAdminTuno = false;
                if (user.IsTuno())
                {
                    var adminRoleId = await ctx.Roles
                        .Where(r => r.Name == "Admin")
                        .Select(r => r.Id)
                        .FirstOrDefaultAsync();
                    if (adminRoleId != null)
                    {
                        isAdminTuno = await ctx.UserRoles
                            .AnyAsync(ur => ur.UserId == userId && ur.RoleId == adminRoleId);
                    }
                }

                if (!isAdminTuno)
                    return null;
            }
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
        var ctx = _contextFactory.CreateDbContext();
        var query = ctx.Meetings
            .AsNoTracking()
            .AsQueryable();

        // Apply visibility filtering for Veterano meetings
        query = await ApplyVeteranoFilterAsync(ctx, query, userId);

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
    /// Applies visibility filtering to the meeting query based on user role/positions.
    /// Filters out CV, Direção, and AG meetings based on user permissions.
    /// </summary>
    private async Task<IQueryable<Meeting>> ApplyVeteranoFilterAsync(ApplicationDbContext ctx, IQueryable<Meeting> query, string userId)
    {
        // Use FirstOrDefaultAsync to ensure we get a fully materialized user object
        var user = await ctx.Users
            .Where(u => u.Id == userId)
            .FirstOrDefaultAsync();

        // If user is not found, filter out restricted meetings
        if (user == null)
        {
            query = query.Where(m => m.Type != MeetingType.ConselhoVeteranos && m.Type != MeetingType.ReuniaoDirecao);
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

            // Filter Direção meetings: only Direção members + Admin with Tuno category
            var hasDirecaoPosition = user.Positions != null &&
                (user.Positions.Contains(Position.Magister) ||
                 user.Positions.Contains(Position.ViceMagister) ||
                 user.Positions.Contains(Position.Secretario) ||
                 user.Positions.Contains(Position.PrimeiroTesoureiro) ||
                 user.Positions.Contains(Position.SegundoTesoureiro));

            if (!hasDirecaoPosition)
            {
                // Check if user is Admin with Tuno category
                var isAdminTuno = false;
                if (user.IsTuno())
                {
                    var adminRoleId = await ctx.Roles
                        .Where(r => r.Name == "Admin")
                        .Select(r => r.Id)
                        .FirstOrDefaultAsync();
                    if (adminRoleId != null)
                    {
                        isAdminTuno = await ctx.UserRoles
                            .AnyAsync(ur => ur.UserId == userId && ur.RoleId == adminRoleId);
                    }
                }

                if (!isAdminTuno)
                {
                    query = query.Where(m => m.Type != MeetingType.ReuniaoDirecao);
                }
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
            var ctx = _contextFactory.CreateDbContext();
            // Load from DB asynchronously with AsNoTracking to prevent accumulating tracked entities
            // which can cause issues during subsequent SaveChangesAsync calls
            var users = await ctx.Users.AsNoTracking().ToListAsync();

            // Now filter in memory (CurrentRole and Positions can be unmapped)
            return users
                .Where(u =>
                    u.CurrentRole == "VETERANO" ||
                    u.CurrentRole == "TUNOSSAURO" ||
                    (u.Positions != null && u.Positions.Contains(Position.Magister)))
                .Select(u => u.Id)
                .ToList(); // sync, in-memory
        }

        // Special case: ReuniaoDirecao - only Direção members + Admin with Tuno category
        if (meetingType == MeetingType.ReuniaoDirecao)
        {
            var ctx = _contextFactory.CreateDbContext();
            var users = await ctx.Users.AsNoTracking().ToListAsync();

            // Get Admin role user IDs
            var adminRoleId = await ctx.Roles
                .Where(r => r.Name == "Admin")
                .Select(r => r.Id)
                .FirstOrDefaultAsync();
            var adminUserIds = adminRoleId != null
                ? (await ctx.UserRoles
                    .Where(ur => ur.RoleId == adminRoleId)
                    .Select(ur => ur.UserId)
                    .ToListAsync())
                    .ToHashSet()
                : new HashSet<string>();

            // Filter to Direção position holders (current fiscal year) + Admin with Tuno category
            return users
                .Where(u =>
                    (u.Positions != null &&
                        (u.Positions.Contains(Position.Magister) ||
                         u.Positions.Contains(Position.ViceMagister) ||
                         u.Positions.Contains(Position.Secretario) ||
                         u.Positions.Contains(Position.PrimeiroTesoureiro) ||
                         u.Positions.Contains(Position.SegundoTesoureiro))) ||
                    (adminUserIds.Contains(u.Id) && u.IsTuno()))
                .Select(u => u.Id)
                .ToList();
        }

        // All other meeting types can stay as EF queries
        var ctx2 = _contextFactory.CreateDbContext();
        IQueryable<ApplicationUser> query = ctx2.Users;

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
