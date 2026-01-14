using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
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
/// Creates audit log entries for status changes to track member progression
/// </summary>
public class MemberStatusService : IMemberStatusService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<MemberStatusService> _logger;
    private readonly MemberStatusUpdateOptions _options;

    public MemberStatusService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IPushNotificationService pushNotificationService,
        IAuditLogService auditLogService,
        ILogger<MemberStatusService> logger,
        IOptions<MemberStatusUpdateOptions> options)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _pushNotificationService = pushNotificationService ?? throw new ArgumentNullException(nameof(pushNotificationService));
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
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
            return await MapToResultAsync(memberStatus);
        }

        // Otherwise, calculate fresh status and update database
        return await UpdateMemberStatusAsync(userId);
    }

    /// <summary>
    /// Updates the status for a specific member by recalculating from activities
    /// Persists the result to the database
    /// Sends push notification if member transitions from retired to active
    /// Logs detailed state changes only when values actually change
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
        }

        // Update fields
        // IMPORTANT: Only update IsRetired if there's no manual override
        // When OverrideRetired is true, the admin has manually set the status
        // and we should not overwrite it with automatic calculations
        if (!memberStatus.OverrideRetired)
        {
            memberStatus.IsRetired = result.IsRetired;
        }
        // Always update these fields regardless of override status
        memberStatus.LastRehearsalDate = result.LastRehearsalDate;
        memberStatus.LastEventDate = result.LastEventDate;
        memberStatus.LastActivityDate = result.LastActivityDate;
        memberStatus.HasAnyActivity = result.HasAnyActivity;
        memberStatus.ProgressMonths = result.ProgressMonths;
        memberStatus.ProgressTotalMonths = result.ProgressTotalMonths;
        memberStatus.ProgressDescription = result.ProgressDescription;
        memberStatus.TotalActivitiesCount = result.TotalActivitiesCount;
        memberStatus.LastUpdatedAt = DateTime.UtcNow;
        memberStatus.UpdatedAt = DateTime.UtcNow;
        // Note: OverrideRetired is NOT set here - it's only set during manual activation

        await _context.SaveChangesAsync();

        // Calculate activity months description for detailed logging
        var now = DateTime.UtcNow;
        var currentMonthStart = new DateTime(now.Year, now.Month, 1);
        var hasActivityInCurrentMonth = await HasActivityInPeriodAsync(userId, currentMonthStart, now);

        // Log state changes for existing records ONLY when values actually change
        // Creates both console logs and audit log entries for the tracing page
        if (!isNewRecord && result.HasAnyActivity)
        {
            // Log retirement status change
            if (wasRetired != result.IsRetired)
            {
                if (wasRetired && !result.IsRetired)
                {
                    // Get detailed activity months for troubleshooting
                    var (monthCount, activityMonths) = await GetActivityMonthsDescriptionAsync(userId, now, hasActivityInCurrentMonth);
                    var changeDescription = $"Retired => Active (achieved {monthCount}/3 consecutive months: {activityMonths})";
                    _logger.LogInformation("✅ {MemberName}: {Change}", memberName, changeDescription);

                    // Create audit log entry with detailed information
                    await _auditLogService.AddAsync(new AuditLog
                    {
                        EntityType = "MemberStatus",
                        EntityId = null, // No specific entity ID
                        Action = "StatusChange",
                        UserId = "System",
                        UserName = "System",
                        TargetMemberName = memberName,
                        Timestamp = DateTime.UtcNow,
                        Changes = changeDescription,
                        EntityDisplayName = memberName,
                        IsCriticalAction = false
                    });
                }
                else if (!wasRetired && result.IsRetired)
                {
                    var changeDescription = "Active => Retired (6+ months without activity)";
                    _logger.LogInformation("⚠️ {MemberName}: {Change}", memberName, changeDescription);

                    // Create audit log entry
                    await _auditLogService.AddAsync(new AuditLog
                    {
                        EntityType = "MemberStatus",
                        EntityId = null,
                        Action = "StatusChange",
                        UserId = "System",
                        UserName = "System",
                        TargetMemberName = memberName,
                        Timestamp = DateTime.UtcNow,
                        Changes = changeDescription,
                        EntityDisplayName = memberName,
                        IsCriticalAction = false
                    });
                }
            }
            // Log progress changes for retired members trying to return
            else if (result.IsRetired && oldProgressMonths.HasValue && result.ProgressMonths.HasValue &&
                     oldProgressTotal == 3 && result.ProgressTotalMonths == 3)
            {
                if (oldProgressMonths != result.ProgressMonths)
                {
                    var changeDescription = $"{oldProgressMonths.Value}/3 => {result.ProgressMonths.Value}/3 months toward reactivation";
                    _logger.LogInformation("📊 {MemberName}: {Change}", memberName, changeDescription);

                    // Create audit log entry
                    await _auditLogService.AddAsync(new AuditLog
                    {
                        EntityType = "MemberStatus",
                        EntityId = null,
                        Action = "ProgressChange",
                        UserId = "System",
                        UserName = "System",
                        TargetMemberName = memberName,
                        Timestamp = DateTime.UtcNow,
                        Changes = changeDescription,
                        EntityDisplayName = memberName,
                        IsCriticalAction = false
                    });
                }
            }
            // Log progress changes for active members approaching retirement
            else if (!result.IsRetired && oldProgressMonths.HasValue && result.ProgressMonths.HasValue &&
                     oldProgressTotal == 6 && result.ProgressTotalMonths == 6)
            {
                if (oldProgressMonths != result.ProgressMonths)
                {
                    var changeDescription = $"{oldProgressMonths.Value} months until retirement => {result.ProgressMonths.Value} months";
                    _logger.LogInformation("📊 {MemberName}: {Change}", memberName, changeDescription);

                    // Create audit log entry
                    await _auditLogService.AddAsync(new AuditLog
                    {
                        EntityType = "MemberStatus",
                        EntityId = null,
                        Action = "ProgressChange",
                        UserId = "System",
                        UserName = "System",
                        TargetMemberName = memberName,
                        Timestamp = DateTime.UtcNow,
                        Changes = changeDescription,
                        EntityDisplayName = memberName,
                        IsCriticalAction = false
                    });
                }
            }
        }

        // Send push notifications based on status changes (only if enabled in configuration)
        if (_options.PushNotificationsEnabled && !isNewRecord)
        {
            // Broadcast: Member just became active (was retired, now active)
            // Skip notification if this was a manual admin activation (OverrideRetired=true)
            // to avoid spam from admin actions
            if (wasRetired && !result.IsRetired && !memberStatus.OverrideRetired)
            {
                await SendMemberBecameActiveNotificationAsync(userId, memberName);
            }

            // Broadcast: Member just became retired (was active, now retired)
            if (!wasRetired && result.IsRetired)
            {
                await SendMemberBecameRetiredNotificationAsync(userId, memberName);
            }

            // Warning to user: 1 month left until retirement
            if (!result.IsRetired && result.ProgressMonths == 1 && result.ProgressTotalMonths == 6)
            {
                // Only send if this is a change (wasn't 1 month before)
                if (oldProgressMonths != 1 || oldProgressTotal != 6)
                {
                    await SendRetirementWarningNotificationAsync(userId, memberName);
                }
            }

            // Warning to user: At 2/3 progress toward reactivation
            if (result.IsRetired && result.ProgressMonths == 2 && result.ProgressTotalMonths == 3)
            {
                // Only send if this is a change (wasn't 2/3 before)
                if (oldProgressMonths != 2 || oldProgressTotal != 3)
                {
                    await SendReactivationEncouragementNotificationAsync(userId, memberName);
                }
            }
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

        // Check if member has activity in the current month (computed once and reused)
        var currentMonthStart = new DateTime(now.Year, now.Month, 1);
        var hasActivityInCurrentMonth = await HasActivityInPeriodAsync(userId, currentMonthStart, now);

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
            // 2. ACTIVE → RETIRED: requires 6 consecutive months without any activity (per month)

            // IMPORTANT: If user was just manually activated (IsRetired=false but has insufficient activity),
            // we should NOT immediately retire them again. This allows admins to manually activate members.
            // They will only be retired if they accumulate 6 NEW consecutive months of inactivity.

            if (user.IsRetired)
            {
                // Currently retired - check if should return to active
                // Need 3 consecutive months of activity to become active
                // Count consecutive months WITH activity (starting from most recent completed month)
                var consecutiveMonthsWithActivity = await CountConsecutiveMonthsWithActivityAsync(userId, now, hasActivityInCurrentMonth);
                if (consecutiveMonthsWithActivity >= 3)
                {
                    isRetired = false; // Return to active

                    // If they naturally earned their way back to active, clear any manual override
                    // They've proven they're active through participation
                    var memberStatusRecord = await _context.MemberStatuses
                        .FirstOrDefaultAsync(ms => ms.UserId == userId);
                    if (memberStatusRecord != null && memberStatusRecord.OverrideRetired)
                    {
                        memberStatusRecord.OverrideRetired = false;
                        await _context.SaveChangesAsync();
                    }
                }
            }
            else
            {
                // Currently active - check if should become retired
                // Need 6 consecutive months WITHOUT activity to become retired

                // IMPORTANT: Check if retirement status was manually overridden by an admin
                // If OverrideRetired is true, respect the manual activation and don't auto-retire
                var memberStatusRecord = await _context.MemberStatuses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ms => ms.UserId == userId);

                var hasManualOverride = memberStatusRecord?.OverrideRetired ?? false;

                if (!hasManualOverride)
                {
                    // Count consecutive months WITHOUT activity (starting from most recent completed month)
                    var consecutiveMonthsWithoutActivity = await CountConsecutiveMonthsWithoutActivityAsync(userId, now);
                    if (consecutiveMonthsWithoutActivity >= 6)
                    {
                        isRetired = true; // Become retired
                    }
                }
                else
                {
                    // Manual override is active - member stays active regardless of inactivity
                    // The override will be cleared when they naturally accumulate enough activity
                    // or when an admin manually retires them
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

        // Calculate progress based on consecutive months of activity/inactivity
        int? progressMonths = null;
        int? progressTotalMonths = null;
        string? progressDescription = null;

        if (hasAnyActivity && lastActivityDate.HasValue)
        {
            if (isRetired)
            {
                // For retired members: show consecutive months toward reactivation (need 3 to become active)
                // Count consecutive months WITH activity starting from the most recent completed month
                var consecutiveMonthsWithActivity = await CountConsecutiveMonthsWithActivityAsync(userId, now, hasActivityInCurrentMonth);
                progressMonths = consecutiveMonthsWithActivity;
                progressTotalMonths = 3;
                progressDescription = $"{consecutiveMonthsWithActivity}/3 meses de atividade consecutiva";
            }
            else
            {
                // For active members: show months until retirement
                // Count consecutive months WITHOUT activity (starting from most recent completed month)
                // 6 months without activity = retired
                var consecutiveMonthsWithoutActivity = await CountConsecutiveMonthsWithoutActivityAsync(userId, now);

                // If the member hasn't participated in the current month yet, 
                // add 1 to show them the potential risk (proactive warning)
                // This encourages participation before the month ends
                if (!hasActivityInCurrentMonth)
                {
                    consecutiveMonthsWithoutActivity++;
                }

                var monthsUntilReform = 6 - consecutiveMonthsWithoutActivity;
                if (monthsUntilReform < 0) monthsUntilReform = 0;

                progressMonths = monthsUntilReform;
                progressTotalMonths = 6;
                progressDescription = monthsUntilReform > 0
                    ? $"{monthsUntilReform} {(monthsUntilReform == 1 ? "mês" : "meses")} até reforma"
                    : "Próximo da reforma";
            }
        }

        // Calculate total activities count (rehearsals + events)
        var totalActivitiesCount = 0;
        if (hasAnyActivity)
        {
            var rehearsalCount = await _context.RehearsalAttendances
                .Include(ra => ra.Rehearsal)
                .Where(ra => ra.UserId == userId
                    && ra.Attended
                    && ra.Rehearsal != null
                    && !ra.Rehearsal.IsCanceled
                    && ra.Rehearsal.Date < now)
                .CountAsync();

            var eventCount = await _context.Enrollments
                .Include(e => e.Event)
                .Where(e => e.UserId == userId
                    && e.WillAttend
                    && e.Event != null
                    && !e.Event.IsCancelled
                    && (e.Event.EndDate ?? e.Event.Date) < now)
                .CountAsync();

            totalActivitiesCount = rehearsalCount + eventCount;
        }

        // Note: hasActivityInCurrentMonth was already calculated at the beginning of this method
        return new MemberStatusResult
        {
            IsRetired = isRetired,
            LastRehearsalDate = lastRehearsalDate == default ? null : lastRehearsalDate,
            LastEventDate = lastEventDate == default ? null : lastEventDate,
            LastActivityDate = lastActivityDate,
            HasAnyActivity = hasAnyActivity,
            ProgressMonths = progressMonths,
            ProgressTotalMonths = progressTotalMonths,
            ProgressDescription = progressDescription,
            TotalActivitiesCount = totalActivitiesCount,
            HasActivityInCurrentMonth = hasActivityInCurrentMonth
        };
    }


    /// <summary>
    /// Counts the number of consecutive months that have at least one activity
    /// Used to determine if a retired member should return to active status
    /// Member needs 3 consecutive months to transition from RETIRED to ACTIVE
    /// Stops counting when a month without activity is found
    /// 
    /// Logic:
    /// - First checks if the CURRENT month has any PAST activity (before referenceDate)
    /// - If yes, includes current month in the count and then checks previous months
    /// - If no, only counts from the previous completed month backwards
    /// - This ensures progress is shown immediately when user participates
    /// 
    /// Example (referenceDate = Jan 15, 2026):
    /// - User has activity in Jan 2026 (before Jan 15) → count = 1, then check Dec 2025
    /// - User has activity in Dec 2025 → count = 2, then check Nov 2025
    /// - User has activity in Nov 2025 → count = 3 → becomes active
    /// 
    /// Example (referenceDate = Jan 15, 2026, user only has Dec activity):
    /// - No activity in Jan 2026 yet → start from Dec 2025
    /// - User has activity in Dec 2025 → count = 1
    /// - No activity in Nov 2025 → stop, return 1
    /// </summary>
    private async Task<int> CountConsecutiveMonthsWithActivityAsync(string userId, DateTime referenceDate, bool? preCalculatedCurrentMonthActivity = null)
    {
        int consecutiveMonths = 0;

        // Use pre-calculated value if provided, otherwise calculate
        bool hasActivityInCurrentMonth;
        if (preCalculatedCurrentMonthActivity.HasValue)
        {
            hasActivityInCurrentMonth = preCalculatedCurrentMonthActivity.Value;
        }
        else
        {
            var currentMonthStart = new DateTime(referenceDate.Year, referenceDate.Month, 1);
            hasActivityInCurrentMonth = await HasActivityInPeriodAsync(userId, currentMonthStart, referenceDate);
        }

        int startingMonth = 1; // Default: start from previous completed month

        if (hasActivityInCurrentMonth)
        {
            // Current month has activity - include it and start checking from there
            consecutiveMonths = 1;
            startingMonth = 1; // Check previous months (i=1 is last month)
        }

        // Check previous completed months (i=1 is last month, i=2 is 2 months ago, etc.)
        for (int i = startingMonth; i <= 12; i++)
        {
            var targetDate = referenceDate.AddMonths(-i);
            var monthStart = new DateTime(targetDate.Year, targetDate.Month, 1);
            var monthEnd = monthStart.AddMonths(1);

            var hasActivityInMonth = await HasActivityInPeriodAsync(userId, monthStart, monthEnd);

            if (hasActivityInMonth)
            {
                consecutiveMonths++;
            }
            else
            {
                // Stop counting when we find a month without activity (gap in consecutive months)
                break;
            }
        }

        return consecutiveMonths;
    }

    /// <summary>
    /// Gets detailed information about which months have activity for diagnostic purposes
    /// Used in audit logs when retirement status changes to help troubleshoot issues
    /// Returns a tuple: (monthCount, monthsDescription)
    /// </summary>
    private async Task<(int count, string description)> GetActivityMonthsDescriptionAsync(string userId, DateTime referenceDate, bool hasActivityInCurrentMonth)
    {
        var monthsWithActivity = new List<string>();
        var cultureInfo = System.Globalization.CultureInfo.GetCultureInfo("pt-PT");

        if (hasActivityInCurrentMonth)
        {
            monthsWithActivity.Add(referenceDate.ToString("MMM yyyy", cultureInfo));
        }

        // Check previous 12 months backwards from current month
        // The loop intentionally checks from i=1 (last month) to i=12 (12 months ago)
        // This ensures we find consecutive months starting from the most recent
        for (int i = 1; i <= 12; i++)
        {
            var targetDate = referenceDate.AddMonths(-i);
            var monthStart = new DateTime(targetDate.Year, targetDate.Month, 1);
            var monthEnd = monthStart.AddMonths(1);

            var hasActivityInMonth = await HasActivityInPeriodAsync(userId, monthStart, monthEnd);
            if (hasActivityInMonth)
            {
                monthsWithActivity.Add(targetDate.ToString("MMM yyyy", cultureInfo));
            }
            else if (monthsWithActivity.Any())
            {
                // Stop once we hit a gap - this only collects consecutive months for the report
                // e.g., if current month + Dec + Nov have activity but Oct doesn't, we stop at Nov
                break;
            }
        }

        if (!monthsWithActivity.Any())
        {
            return (0, "no activity months found");
        }

        return (monthsWithActivity.Count, string.Join(", ", monthsWithActivity));
    }

    /// <summary>
    /// Counts the number of consecutive COMPLETED months that have NO activity
    /// Used to determine if an active member should become retired
    /// Member needs 6 consecutive months WITHOUT activity to transition from ACTIVE to RETIRED
    /// Stops counting when a month WITH activity is found
    /// 
    /// IMPORTANT: Only counts COMPLETED months (not the current month)
    /// The current month is still in progress, so the user has a chance to participate
    /// 
    /// Example (referenceDate = Jan 15, 2026):
    /// - Dec 2025 (completed) - no activity → count = 1
    /// - Nov 2025 (completed) - no activity → count = 2
    /// - ... continues until a month with activity is found or 6 months reached
    /// </summary>
    private async Task<int> CountConsecutiveMonthsWithoutActivityAsync(string userId, DateTime referenceDate)
    {
        int consecutiveMonthsWithoutActivity = 0;

        // Start from i=1 (previous completed month), NOT i=0 (current month)
        // The current month is still in progress so we don't penalize the user yet
        for (int i = 1; i <= 12; i++)
        {
            var targetDate = referenceDate.AddMonths(-i);
            var monthStart = new DateTime(targetDate.Year, targetDate.Month, 1);
            var monthEnd = monthStart.AddMonths(1);

            var hasActivityInMonth = await HasActivityInPeriodAsync(userId, monthStart, monthEnd);

            if (!hasActivityInMonth)
            {
                // No activity this month
                consecutiveMonthsWithoutActivity++;
            }
            else
            {
                // Found activity - stop counting consecutive months without activity
                break;
            }
        }

        return consecutiveMonthsWithoutActivity;
    }

    /// <summary>
    /// Helper method to check if a user has any PAST activity in a given time period
    /// CRITICAL: Only includes activities that have already occurred (before DateTime.UtcNow)
    /// Future enrollments/registrations are NOT counted as activity
    /// </summary>
    private async Task<bool> HasActivityInPeriodAsync(string userId, DateTime periodStart, DateTime periodEnd)
    {
        var now = DateTime.UtcNow;

        // CRITICAL: Only include PAST rehearsals (Date < now)
        // This ensures future rehearsals the member is registered for don't count
        var hasRehearsalInPeriod = await _context.RehearsalAttendances
            .Include(ra => ra.Rehearsal)
            .AnyAsync(ra => ra.UserId == userId
                && ra.Attended
                && ra.Rehearsal != null
                && !ra.Rehearsal.IsCanceled
                && ra.Rehearsal.Date >= periodStart
                && ra.Rehearsal.Date < periodEnd
                && ra.Rehearsal.Date < now);  // BUG FIX: Exclude future rehearsals

        if (hasRehearsalInPeriod)
            return true;

        // CRITICAL: Only include PAST events (EndDate or Date < now)
        // This ensures future events the member is enrolled in don't count
        var hasEventInPeriod = await _context.Enrollments
            .Include(e => e.Event)
            .AnyAsync(e => e.UserId == userId
                && e.WillAttend
                && e.Event != null
                && !e.Event.IsCancelled
                && e.Event.Date >= periodStart
                && e.Event.Date < periodEnd
                && (e.Event.EndDate ?? e.Event.Date) < now);  // BUG FIX: Exclude future events

        return hasEventInPeriod;
    }

    /// <summary>
    /// Sends a push notification when a member becomes active (transitions from retired to active)
    /// Broadcasts to all subscribed users about the status change
    /// </summary>
    private async Task SendMemberBecameActiveNotificationAsync(string userId, string memberName)
    {
        try
        {
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
    /// Sends a push notification when a member becomes retired (transitions from active to retired)
    /// Broadcasts to all subscribed users about the status change
    /// </summary>
    private async Task SendMemberBecameRetiredNotificationAsync(string userId, string memberName)
    {
        try
        {
            var notification = new SendPushNotificationDto
            {
                Title = "Membro Reformado 📋",
                Body = $"{memberName} passou a reformado após 6 meses sem atividade.",
                Tag = $"member-retired-{userId}",
                Icon = "/images/favicon/android-chrome-192x192.png",
                Url = "/members"
            };

            // Broadcast to all subscribed users
            await _pushNotificationService.BroadcastAsync(notification);

            _logger.LogInformation("Sent member retirement notification for user {UserId} ({MemberName})", userId, memberName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending member retirement notification for user {UserId}", userId);
            // Don't rethrow - notification failure shouldn't break the status update
        }
    }

    /// <summary>
    /// Sends a warning notification to a user who has 1 month left until retirement
    /// </summary>
    private async Task SendRetirementWarningNotificationAsync(string userId, string memberName)
    {
        try
        {
            var notification = new SendPushNotificationDto
            {
                Title = "Aviso: 1 mês até reforma ⚠️",
                Body = $"{memberName}, tens apenas 1 mês até ficares reformado. Participa numa atividade para evitar!",
                Tag = $"retirement-warning-{userId}",
                Icon = "/images/favicon/android-chrome-192x192.png",
                Url = "/events"
            };

            // Send to the specific user only
            await _pushNotificationService.SendToUserAsync(userId, notification);

            _logger.LogInformation("Sent retirement warning notification to user {UserId} ({MemberName})", userId, memberName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending retirement warning notification for user {UserId}", userId);
            // Don't rethrow - notification failure shouldn't break the status update
        }
    }

    /// <summary>
    /// Sends an encouragement notification to a retired user who is at 2/3 progress toward reactivation
    /// </summary>
    private async Task SendReactivationEncouragementNotificationAsync(string userId, string memberName)
    {
        try
        {
            var notification = new SendPushNotificationDto
            {
                Title = "Quase lá! 2/3 para reativação 💪",
                Body = $"{memberName}, estás a 2/3 do caminho para voltares ao ativo! Participa este mês para completar!",
                Tag = $"reactivation-encouragement-{userId}",
                Icon = "/images/favicon/android-chrome-192x192.png",
                Url = "/events"
            };

            // Send to the specific user only
            await _pushNotificationService.SendToUserAsync(userId, notification);

            _logger.LogInformation("Sent reactivation encouragement notification to user {UserId} ({MemberName})", userId, memberName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending reactivation encouragement notification for user {UserId}", userId);
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
    /// Gets the comprehensive status for multiple members in a single batch query
    /// More efficient than calling GetMemberStatusAsync in a loop
    /// Returns cached statuses if available and fresh (less than 1 hour old)
    /// </summary>
    /// <param name="userIds">The user IDs to get status for</param>
    /// <returns>Dictionary mapping user IDs to their status results (null if not cached)</returns>
    public async Task<Dictionary<string, MemberStatusResult?>> GetMemberStatusesBatchAsync(IEnumerable<string> userIds)
    {
        var userIdList = userIds.ToList();

        if (!userIdList.Any())
            return new Dictionary<string, MemberStatusResult?>();

        // Load all member statuses in a single query
        var memberStatuses = await _context.MemberStatuses
            .AsNoTracking()
            .Where(ms => userIdList.Contains(ms.UserId))
            .ToListAsync();

        var oneDayAgo = DateTime.UtcNow.AddDays(-1);

        // Convert to dictionary for O(1) lookups instead of O(n) FirstOrDefault in loop
        var statusesByUserId = memberStatuses.ToDictionary(ms => ms.UserId);

        // Build result dictionary
        var result = new Dictionary<string, MemberStatusResult?>();
        foreach (var userId in userIdList)
        {
            statusesByUserId.TryGetValue(userId, out var memberStatus);

            // Only return cached status if it's fresh (less than 24 hours old)
            // Cache is refreshed daily by background job at 00:00
            if (memberStatus != null && memberStatus.LastUpdatedAt > oneDayAgo)
            {
                result[userId] = await MapToResultAsync(memberStatus);
            }
            else
            {
                // Status is stale or doesn't exist - return null
                // Caller can decide whether to update or use fallback
                result[userId] = null;
            }
        }

        return result;
    }

    /// <summary>
    /// Maps a MemberStatus entity to a MemberStatusResult DTO
    /// Computes HasActivityInCurrentMonth, ProgressMonths, and IsRetired dynamically since they depend on current time
    /// CRITICAL: Recalculates IsRetired and progress for BOTH active and retired members to ensure consistency
    /// between batch queries (cached) and individual queries (fresh calculation)
    /// This is essential because future activities should never count toward status calculations
    /// IMPORTANT: Respects OverrideRetired flag - when admin manually sets status, it won't be recalculated
    /// </summary>
    private async Task<MemberStatusResult> MapToResultAsync(MemberStatus memberStatus)
    {
        // Calculate current month activity dynamically
        var now = DateTime.UtcNow;
        var currentMonthStart = new DateTime(now.Year, now.Month, 1);
        var hasActivityInCurrentMonth = await HasActivityInPeriodAsync(memberStatus.UserId, currentMonthStart, now);

        // Recalculate IsRetired and progress dynamically for both active and retired members
        // This ensures consistency between cached (batch) and fresh (individual) queries
        // CRITICAL: This prevents stale cache from showing incorrect status (e.g., active when should be retired)
        // IMPORTANT: If OverrideRetired is true, respect the admin's manual setting and don't recalculate IsRetired
        bool isRetired = memberStatus.IsRetired;
        int? progressMonths = memberStatus.ProgressMonths;
        int? progressTotalMonths = memberStatus.ProgressTotalMonths;
        string? progressDescription = memberStatus.ProgressDescription;

        if (memberStatus.HasAnyActivity)
        {
            // Calculate consecutive months with activity (for retired→active transition check)
            var consecutiveMonthsWithActivity = await CountConsecutiveMonthsWithActivityAsync(memberStatus.UserId, now, hasActivityInCurrentMonth);

            // Calculate consecutive months without activity (for active→retired transition check)
            var consecutiveMonthsWithoutActivity = await CountConsecutiveMonthsWithoutActivityAsync(memberStatus.UserId, now);

            // Only recalculate IsRetired if OverrideRetired is false
            // When OverrideRetired is true, the admin has manually set the status and we should respect it
            if (!memberStatus.OverrideRetired)
            {
                // Dynamically recalculate IsRetired based on current consecutive months
                // This ensures the displayed status matches the actual calculated status
                if (memberStatus.IsRetired)
                {
                    // Currently cached as retired - check if should be active
                    if (consecutiveMonthsWithActivity >= 3)
                    {
                        isRetired = false; // Should be active (3+ consecutive months of activity)
                    }
                }
                else
                {
                    // Currently cached as active - check if should be retired
                    if (consecutiveMonthsWithoutActivity >= 6)
                    {
                        isRetired = true; // Should be retired (6+ consecutive months without activity)
                    }
                }
            }

            // Always recalculate progress regardless of OverrideRetired
            // Progress is informational and shows actual activity status
            if (isRetired)
            {
                // Progress for retired members: X/3 months toward reactivation
                progressMonths = consecutiveMonthsWithActivity;
                progressTotalMonths = 3;
                progressDescription = $"{consecutiveMonthsWithActivity}/3 meses de atividade consecutiva";
            }
            else
            {
                // Progress for active members: months until retirement
                // If no activity in current month, add 1 for proactive warning
                var displayMonthsWithoutActivity = consecutiveMonthsWithoutActivity;
                if (!hasActivityInCurrentMonth)
                {
                    displayMonthsWithoutActivity++;
                }

                var monthsUntilReform = 6 - displayMonthsWithoutActivity;
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
            LastRehearsalDate = memberStatus.LastRehearsalDate,
            LastEventDate = memberStatus.LastEventDate,
            LastActivityDate = memberStatus.LastActivityDate,
            HasAnyActivity = memberStatus.HasAnyActivity,
            ProgressMonths = progressMonths,
            ProgressTotalMonths = progressTotalMonths,
            ProgressDescription = progressDescription,
            TotalActivitiesCount = memberStatus.TotalActivitiesCount,
            HasActivityInCurrentMonth = hasActivityInCurrentMonth
        };
    }
}
