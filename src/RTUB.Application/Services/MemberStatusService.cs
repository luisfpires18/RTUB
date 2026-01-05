using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Microsoft.Extensions.Logging;

namespace RTUB.Application.Services;

/// <summary>
/// Member status service implementation
/// Provides comprehensive member status including retirement state and last activity tracking
/// Follows Single Responsibility and Dependency Inversion principles
/// Status is cached in the database and updated periodically or on-demand
/// </summary>
public class MemberStatusService : IMemberStatusService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly ILogger<MemberStatusService> _logger;

    public MemberStatusService(
        ApplicationDbContext context, 
        UserManager<ApplicationUser> userManager,
        IPushNotificationService pushNotificationService,
        ILogger<MemberStatusService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _pushNotificationService = pushNotificationService ?? throw new ArgumentNullException(nameof(pushNotificationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the comprehensive status of a member from the database cache
    /// If not found or stale (older than 1 hour), recalculates and updates
    /// </summary>
    /// <param name="userId">The user ID to get status for</param>
    /// <returns>A result containing retirement status, last activity dates, and activity flags</returns>
    public async Task<MemberStatusResult> GetMemberStatusAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

        // Try to get cached status from database
        var memberStatus = await _context.MemberStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(ms => ms.UserId == userId);

        // If cache exists and is fresh (less than 1 hour old), return it
        if (memberStatus != null && (DateTime.UtcNow - memberStatus.LastUpdatedAt).TotalHours < 1)
        {
            return MapToResult(memberStatus);
        }

        // Otherwise, calculate fresh status and update database
        return await UpdateMemberStatusAsync(userId);
    }

    /// <summary>
    /// Updates the status for a specific member by recalculating from activities
    /// Persists the result to the database
    /// Sends push notification if member transitions from retired to active
    /// Logs detailed state changes for tracking
    /// </summary>
    /// <param name="userId">The user ID to update status for</param>
    /// <returns>The updated status result</returns>
    public async Task<MemberStatusResult> UpdateMemberStatusAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

        // Get user for logging
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException($"User with ID {userId} not found");
        }
        
        var memberName = !string.IsNullOrEmpty(user.Nickname) 
            ? user.Nickname 
            : $"{user.FirstName} {user.LastName}";

        // Calculate the status using the existing logic
        var result = await CalculateMemberStatusAsync(userId);

        // Find or create MemberStatus record
        var memberStatus = await _context.MemberStatuses
            .FirstOrDefaultAsync(ms => ms.UserId == userId);

        bool wasRetired = memberStatus?.IsRetired ?? false;
        int? oldProgressMonths = memberStatus?.ProgressMonths;
        int? oldProgressTotal = memberStatus?.ProgressTotalMonths;
        bool isNewRecord = memberStatus == null;

        if (memberStatus == null)
        {
            // Create new record
            memberStatus = new MemberStatus
            {
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };
            _context.MemberStatuses.Add(memberStatus);
            
            // Log new member status creation
            if (result.HasAnyActivity)
            {
                _logger.LogInformation("Created status for {MemberName}: {Status}, Progress: {Progress}", 
                    memberName, 
                    result.IsRetired ? "Retired" : "Active",
                    result.ProgressDescription ?? "N/A");
            }
        }

        // Update fields
        memberStatus.IsRetired = result.IsRetired;
        memberStatus.LastRehearsalDate = result.LastRehearsalDate;
        memberStatus.LastEventDate = result.LastEventDate;
        memberStatus.LastActivityDate = result.LastActivityDate;
        memberStatus.HasAnyActivity = result.HasAnyActivity;
        memberStatus.ProgressMonths = result.ProgressMonths;
        memberStatus.ProgressTotalMonths = result.ProgressTotalMonths;
        memberStatus.ProgressDescription = result.ProgressDescription;
        memberStatus.LastUpdatedAt = DateTime.UtcNow;
        memberStatus.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Log state changes for existing records
        if (!isNewRecord && result.HasAnyActivity)
        {
            // Log retirement status change
            if (wasRetired && !result.IsRetired)
            {
                _logger.LogInformation("✅ {MemberName} changed from RETIRED to ACTIVE (achieved 3/3 consecutive months)", memberName);
            }
            else if (!wasRetired && result.IsRetired)
            {
                _logger.LogInformation("⚠️ {MemberName} changed from ACTIVE to RETIRED (6+ months without activity)", memberName);
            }
            // Log progress changes for retired members trying to return
            else if (result.IsRetired && oldProgressMonths.HasValue && result.ProgressMonths.HasValue && 
                     oldProgressTotal == 3 && result.ProgressTotalMonths == 3)
            {
                if (oldProgressMonths != result.ProgressMonths)
                {
                    _logger.LogInformation("📊 {MemberName} progress: {OldProgress}/3 → {NewProgress}/3 months toward reactivation", 
                        memberName, oldProgressMonths.Value, result.ProgressMonths.Value);
                }
            }
            // Log progress changes for active members approaching retirement
            else if (!result.IsRetired && oldProgressMonths.HasValue && result.ProgressMonths.HasValue &&
                     oldProgressTotal == 6 && result.ProgressTotalMonths == 6)
            {
                if (oldProgressMonths != result.ProgressMonths)
                {
                    _logger.LogInformation("📊 {MemberName} has {NewProgress} months until retirement (was {OldProgress})", 
                        memberName, result.ProgressMonths.Value, oldProgressMonths.Value);
                }
            }
        }

        // Send push notification if member just became active (was retired, now active)
        if (!isNewRecord && wasRetired && !result.IsRetired)
        {
            await SendMemberBecameActiveNotificationAsync(userId);
        }

        return result;
    }

    /// <summary>
    /// Updates the status for all active members (Caloiro, Tuno, Veterano, Tunossauro)
    /// Should be called periodically by a background service
    /// </summary>
    /// <returns>The number of member statuses updated</returns>
    public async Task<int> UpdateAllMemberStatusesAsync()
    {
        // Get all active members (excluding Leitao and TunoHonorario)
        var activeMembers = await _userManager.Users
            .Where(u => !u.Categories.Contains(MemberCategory.Leitao) &&
                       !u.Categories.Contains(MemberCategory.TunoHonorario) &&
                       (u.Categories.Contains(MemberCategory.Caloiro) ||
                        u.Categories.Contains(MemberCategory.Tuno) ||
                        u.Categories.Contains(MemberCategory.Veterano) ||
                        u.Categories.Contains(MemberCategory.Tunossauro)))
            .Select(u => u.Id)
            .ToListAsync();

        int updatedCount = 0;

        foreach (var userId in activeMembers)
        {
            try
            {
                await UpdateMemberStatusAsync(userId);
                updatedCount++;
            }
            catch (Exception)
            {
                // Log error and continue with next member
                // Don't let one failure stop the entire batch
                continue;
            }
        }

        return updatedCount;
    }

    /// <summary>
    /// Calculates the comprehensive status of a member including retirement state and last activity dates
    /// This is the core calculation logic extracted from the old GetMemberStatusAsync
    /// Retirement status follows a state transition model:
    /// - New members: manual initial state (can be set as active or retired)
    /// - RETIRED → ACTIVE: requires 3 consecutive months with activity
    /// - ACTIVE → RETIRED: requires 6 months without any activity
    /// CRITICAL: Only includes PAST activities (before DateTime.UtcNow)
    /// Uses same predicates as XP/Leaderboard logic to ensure consistency
    /// </summary>
    /// <param name="userId">The user ID to calculate status for</param>
    /// <returns>A result containing retirement status, last activity dates, and activity flags</returns>
    private async Task<MemberStatusResult> CalculateMemberStatusAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

        var now = DateTime.UtcNow;

        // Get last rehearsal date where member was present and rehearsal is not canceled
        // CRITICAL: Only include PAST rehearsals (Date < now)
        // Uses same predicate as MemberStatisticsService for XP calculation
        var lastRehearsalDate = await _context.RehearsalAttendances
            .Include(ra => ra.Rehearsal)
            .Where(ra => ra.UserId == userId
                && ra.Attended
                && ra.Rehearsal != null
                && !ra.Rehearsal.IsCanceled
                && ra.Rehearsal.Date < now) // BUG FIX: Exclude future rehearsals
            .OrderByDescending(ra => ra.Rehearsal!.Date)
            .Select(ra => ra.Rehearsal!.Date)
            .FirstOrDefaultAsync();

        // Get last event date where member is enrolled (WillAttend) and event is not canceled
        // CRITICAL: Only include PAST events (EndDate or Date < now)
        // Uses same predicate as MemberStatisticsService for XP calculation
        var lastEventDate = await _context.Enrollments
            .Include(e => e.Event)
            .Where(e => e.UserId == userId
                && e.WillAttend
                && e.Event != null
                && !e.Event.IsCancelled
                && (e.Event.EndDate ?? e.Event.Date) < now) // BUG FIX: Exclude future events
            .OrderByDescending(e => e.Event!.EndDate ?? e.Event!.Date)
            .Select(e => e.Event!.EndDate ?? e.Event!.Date)
            .FirstOrDefaultAsync();

        // Calculate last activity date as the maximum of the two
        DateTime? lastActivityDate = null;
        bool hasLastRehearsal = lastRehearsalDate != default(DateTime);
        bool hasLastEvent = lastEventDate != default(DateTime);

        if (hasLastRehearsal && hasLastEvent)
        {
            lastActivityDate = lastRehearsalDate > lastEventDate
                ? lastRehearsalDate
                : lastEventDate;
        }
        else if (hasLastRehearsal)
        {
            lastActivityDate = lastRehearsalDate;
        }
        else if (hasLastEvent)
        {
            lastActivityDate = lastEventDate;
        }

        // Determine if user has any past activity
        bool hasAnyActivity = lastActivityDate.HasValue;

        // Get current user to check and update retirement status
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException($"User with ID {userId} not found");
        }

        bool isRetired;

        // Apply retirement rules only if user has activity history
        if (hasAnyActivity)
        {
            // Start with current retirement status from database
            isRetired = user.IsRetired;
            
            // State transition rules based on activity:
            // 1. RETIRED → ACTIVE: requires 3 consecutive months with activity
            // 2. ACTIVE → RETIRED: requires 6 months without any activity
            
            if (user.IsRetired)
            {
                // Currently retired - check if should return to active
                // Need 3 consecutive months of activity to become active
                var consecutiveMonths = await CountConsecutiveMonthsWithActivityAsync(userId, now);
                if (consecutiveMonths >= 3)
                {
                    isRetired = false; // Return to active
                }
            }
            else
            {
                // Currently active - check if should become retired
                // Need 6 months without activity to become retired
                var sixMonthsAgo = now.AddMonths(-6);
                if (lastActivityDate!.Value < sixMonthsAgo)
                {
                    isRetired = true; // Become retired
                }
            }

            // Persist retirement status if changed
            if (user.IsRetired != isRetired)
            {
                user.IsRetired = isRetired;
                await _userManager.UpdateAsync(user);
            }
        }
        else
        {
            // No activity history -> keep current status (allows manual setting for new members)
            isRetired = user.IsRetired;
        }

        // Calculate progress based on consecutive months of activity
        int? progressMonths = null;
        int? progressTotalMonths = null;
        string? progressDescription = null;

        if (hasAnyActivity && lastActivityDate.HasValue)
        {
            // Get consecutive months count (already calculated above if retired, need to recalculate if active)
            var consecutiveMonths = await CountConsecutiveMonthsWithActivityAsync(userId, now);
            
            if (isRetired)
            {
                // For retired members: show consecutive months toward reactivation (need 3 to become active)
                progressMonths = consecutiveMonths;
                progressTotalMonths = 3;
                progressDescription = $"{consecutiveMonths}/3 meses de atividade consecutiva";
            }
            else
            {
                // For active members: show months until retirement (6 months from last activity)
                var monthsSinceLastActivity = GetMonthsDifference(lastActivityDate.Value, now);
                var monthsUntilReform = 6 - monthsSinceLastActivity;
                if (monthsUntilReform < 0) monthsUntilReform = 0;
                
                progressMonths = monthsUntilReform;
                progressTotalMonths = 6;
                progressDescription = monthsUntilReform > 0 
                    ? $"{monthsUntilReform} {(monthsUntilReform == 1 ? "mês" : "meses")} até reforma"
                    : "Próximo da reforma";
            }
        }

        return new MemberStatusResult
        {
            IsRetired = isRetired,
            LastRehearsalDate = lastRehearsalDate == default ? null : lastRehearsalDate,
            LastEventDate = lastEventDate == default ? null : lastEventDate,
            LastActivityDate = lastActivityDate,
            HasAnyActivity = hasAnyActivity,
            ProgressMonths = progressMonths,
            ProgressTotalMonths = progressTotalMonths,
            ProgressDescription = progressDescription
        };
    }

    
    /// <summary>
    /// Counts the number of consecutive months (starting from most recent) that have at least one activity
    /// Used to determine if a retired member should return to active status
    /// Member needs 3 consecutive months to transition from RETIRED to ACTIVE
    /// Stops counting when a month without activity is found
    /// CRITICAL: Only counts PAST activities (before referenceDate)
    /// IMPORTANT: Starts from the PREVIOUS month (i=1), NOT the current month
    /// The current ongoing month is not counted until it has completed
    /// </summary>
    private async Task<int> CountConsecutiveMonthsWithActivityAsync(string userId, DateTime referenceDate)
    {
        int consecutiveMonths = 0;

        // Check up to 12 previous months (reasonable limit)
        // Start from i=1 to exclude the CURRENT ongoing month
        // Only count COMPLETED months (previous months)
        // i=1 is the previous month, i=12 is 12 months ago
        for (int i = 1; i < 13; i++)
        {
            var targetDate = referenceDate.AddMonths(-i);
            var monthStart = new DateTime(targetDate.Year, targetDate.Month, 1); // First day of the month
            var monthEnd = monthStart.AddMonths(1); // First day of next month

            // Get activities in this month
            // CRITICAL: Exclude future activities (after referenceDate)
            // For consecutive months, we count the START date of activities (when they began)
            var hasRehearsalInMonth = await _context.RehearsalAttendances
                .Include(ra => ra.Rehearsal)
                .AnyAsync(ra => ra.UserId == userId
                    && ra.Attended
                    && ra.Rehearsal != null
                    && !ra.Rehearsal.IsCanceled
                    && ra.Rehearsal.Date >= monthStart
                    && ra.Rehearsal.Date < monthEnd
                    && ra.Rehearsal.Date < referenceDate); // Exclude future rehearsals

            var hasEventInMonth = await _context.Enrollments
                .Include(e => e.Event)
                .AnyAsync(e => e.UserId == userId
                    && e.WillAttend
                    && e.Event != null
                    && !e.Event.IsCancelled
                    && e.Event.Date >= monthStart  // Use start date for month counting
                    && e.Event.Date < monthEnd
                    && e.Event.Date < referenceDate); // Exclude future events

            if (hasRehearsalInMonth || hasEventInMonth)
            {
                consecutiveMonths++;
            }
            else
            {
                // Stop counting when we find a month without activity
                break;
            }
        }

        return consecutiveMonths;
    }
    
    /// <summary>
    /// Sends a push notification when a member becomes active (transitions from retired to active)
    /// Notifies all subscribed users about the status change
    /// </summary>
    private async Task SendMemberBecameActiveNotificationAsync(string userId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("Cannot send activation notification: User {UserId} not found", userId);
                return;
            }

            var memberName = !string.IsNullOrEmpty(user.Nickname) 
                ? user.Nickname 
                : $"{user.FirstName} {user.LastName}";

            var notification = new SendPushNotificationDto
            {
                Title = "Membro Reativado! 🎉",
                Body = $"{memberName} é agora membro ativo após 3 meses consecutivos de atividade!",
                Tag = $"member-active-{userId}",
                Icon = "/images/favicon/android-chrome-192x192.png",
                Url = "/members"
            };

            // Broadcast to all subscribed users
            await _pushNotificationService.BroadcastAsync(notification);
            
            _logger.LogInformation("Sent member activation notification for user {UserId} ({MemberName})", userId, memberName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending member activation notification for user {UserId}", userId);
            // Don't rethrow - notification failure shouldn't break the status update
        }
    }
    
    /// <summary>
    /// Calculates the difference in months between two dates
    /// </summary>
    private int GetMonthsDifference(DateTime startDate, DateTime endDate)
    {
        return ((endDate.Year - startDate.Year) * 12) + endDate.Month - startDate.Month;
    }
    
    /// <summary>
    /// Maps a MemberStatus entity to a MemberStatusResult DTO
    /// </summary>
    private MemberStatusResult MapToResult(MemberStatus memberStatus)
    {
        return new MemberStatusResult
        {
            IsRetired = memberStatus.IsRetired,
            LastRehearsalDate = memberStatus.LastRehearsalDate,
            LastEventDate = memberStatus.LastEventDate,
            LastActivityDate = memberStatus.LastActivityDate,
            HasAnyActivity = memberStatus.HasAnyActivity,
            ProgressMonths = memberStatus.ProgressMonths,
            ProgressTotalMonths = memberStatus.ProgressTotalMonths,
            ProgressDescription = memberStatus.ProgressDescription
        };
    }
}
