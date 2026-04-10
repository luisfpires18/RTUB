using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Constants;
using RTUB.Core.Entities;

namespace RTUB.Application.Data;

/// <summary>
/// Database context for the application
/// Extends IdentityDbContext for ASP.NET Core Identity support
/// </summary>
public partial class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuditContext _auditContext;
    private readonly IAuditLogAppender _auditLogAppender;
    private bool _isAuditingEnabled = true;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IHttpContextAccessor httpContextAccessor, AuditContext auditContext, IAuditLogAppender auditLogAppender)
        : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
        _auditContext = auditContext;
        _auditLogAppender = auditLogAppender;
    }

    // Domain entity DbSets
    public DbSet<Event> Events { get; set; }
    public DbSet<Enrollment> Enrollments { get; set; }
    public DbSet<Album> Albums { get; set; }
    public DbSet<AlbumAccess> AlbumAccesses { get; set; }
    public DbSet<Song> Songs { get; set; }
    public DbSet<SongYouTubeUrl> SongYouTubeUrls { get; set; }
    public DbSet<SongVideo> SongVideos { get; set; } = null!;
    public DbSet<SongPlayCount> SongPlayCounts { get; set; }
    public DbSet<EventVideo> EventVideos { get; set; } = null!;
    public DbSet<EventRepertoire> EventRepertoires { get; set; }
    public DbSet<Report> Reports { get; set; }
    public DbSet<Activity> Activities { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<FiscalYear> FiscalYears { get; set; }
    public DbSet<MemberDebt> MemberDebts { get; set; }
    public DbSet<MbwayTransfer> MbwayTransfers { get; set; } = null!;
    public DbSet<NerbaOrder> NerbaOrders { get; set; } = null!;
    public DbSet<Slideshow> Slideshows { get; set; }
    public DbSet<Label> Labels { get; set; }
    public DbSet<Request> Requests { get; set; }
    public DbSet<RoleAssignment> RoleAssignments { get; set; }
    public DbSet<Rehearsal> Rehearsals { get; set; }
    public DbSet<RehearsalAttendance> RehearsalAttendances { get; set; }

    // Inventory & Shop DbSets
    public DbSet<Instrument> Instruments { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<ProductReservation> ProductReservations { get; set; }
    public DbSet<Trophy> Trophies { get; set; }

    // Logistics Board DbSets
    public DbSet<LogisticsBoard> LogisticsBoards { get; set; }
    public DbSet<LogisticsList> LogisticsLists { get; set; }
    public DbSet<LogisticsCard> LogisticsCards { get; set; }
    public DbSet<LogisticsCardAssignment> LogisticsCardAssignments { get; set; }
    public DbSet<LogisticsCardReminder> LogisticsCardReminders { get; set; }

    // Audit Log DbSet
    public DbSet<AuditLog> AuditLogs { get; set; }

    // Transportation DbSets
    public DbSet<Transportation> Transportations { get; set; } = null!;
    public DbSet<TransportationPassenger> TransportationPassengers { get; set; } = null!;

    // Discussion DbSets
    public DbSet<Discussion> Discussions { get; set; }
    public DbSet<Post> Posts { get; set; }
    public DbSet<Comment> Comments { get; set; }
    public DbSet<PostMedia> PostMedia { get; set; }
    public DbSet<CommentImage> CommentImages { get; set; }

    // Meeting DbSet
    public DbSet<Meeting> Meetings { get; set; }
    public DbSet<MeetingRequest> MeetingRequests { get; set; }
    public DbSet<MeetingParticipation> MeetingParticipations { get; set; }

    // Meeting Atas DbSets
    public DbSet<MeetingAta> MeetingAtas { get; set; }
    public DbSet<MeetingAtaAgendaPoint> MeetingAtaAgendaPoints { get; set; }
    public DbSet<MeetingAtaAttachment> MeetingAtaAttachments { get; set; }
    public DbSet<MeetingAtaConfirmation> MeetingAtaConfirmations { get; set; }

    // Leaderboard Comments DbSets
    public DbSet<LeaderboardComment> LeaderboardComments { get; set; }
    public DbSet<LeaderboardCommentLike> LeaderboardCommentLikes { get; set; }

    // Member Instruments DbSet
    public DbSet<MemberInstrument> MemberInstruments { get; set; }

    // Member Status DbSet
    public DbSet<MemberStatus> MemberStatuses { get; set; }

    // Geocoding Cache DbSet
    public DbSet<GeocodingCache> GeocodingCaches { get; set; }

    // Push Notifications DbSet
    public DbSet<PushSubscription> PushSubscriptions { get; set; }

    // Messaging DbSets
    public DbSet<Conversation> Conversations { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<MessageReaction> MessageReactions { get; set; } = null!;
    public DbSet<ConversationUserSettings> ConversationUserSettings { get; set; }

    // Gallery DbSets
    public DbSet<GalleryMedia> GalleryMedia { get; set; }
    public DbSet<GalleryMediaPersonTag> GalleryMediaPersonTags { get; set; }

    // Questions DbSets
    public DbSet<Question> Questions { get; set; }
    public DbSet<QuestionReply> QuestionReplies { get; set; }

    // Naipes (Educational Content) DbSets
    public DbSet<NaipeContent> NaipeContents { get; set; } = null!;
    public DbSet<NaipeComment> NaipeComments { get; set; } = null!;
    public DbSet<NaipePlayCount> NaipePlayCounts { get; set; } = null!;
    public DbSet<NaipeTypeConfig> NaipeTypeConfigs { get; set; } = null!;

    // Game Scores DbSet
    public DbSet<GameScore> GameScores { get; set; }

    // Games DbSet
    public DbSet<Game> Games { get; set; }

    // Betting DbSets
    public DbSet<Bet> Bets { get; set; }
    public DbSet<BetOption> BetOptions { get; set; }
    public DbSet<UserBet> UserBets { get; set; }
    public DbSet<BetComment> BetComments { get; set; }

    // My Tuno DbSets
    public DbSet<Character> Characters { get; set; }
    public DbSet<InventoryItem> InventoryItems { get; set; }
    public DbSet<ForgedWeapon> ForgedWeapons { get; set; }

    // Stage Mode DbSets
    public DbSet<StageProgress> StageProgresses { get; set; }
    public DbSet<StageEnemy> StageEnemies { get; set; }

    // Boss Mode DbSets
    public DbSet<BossModeProgress> BossModeProgresses { get; set; }

    // Survive Mode DbSets
    public DbSet<SurviveModeProgress> SurviveModeProgresses { get; set; }

    // Item Type Config DbSets
    public DbSet<ItemTypeConfig> ItemTypeConfigs { get; set; }
    public DbSet<ForgeComboConfig> ForgeComboConfigs { get; set; }

    // Event Contact DbSet
    public DbSet<EventContact> EventContacts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply EF Core configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Skip audit logging if disabled (to prevent infinite loop when saving audit logs)
        if (!_isAuditingEnabled)
        {
            return await base.SaveChangesAsync(cancellationToken);
        }

        // Try to get user from HttpContext first (for HTTP requests)
        var username = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
        var userId = _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        // Fallback to AuditContext for operations outside HTTP context (e.g., Blazor InteractiveServer)  
        // or when user is not yet authenticated (e.g., during login before SignInAsync is called)
        if (string.IsNullOrEmpty(username))
        {
            username = _auditContext.UserName;
            userId = _auditContext.UserId;
        }

        // Collect audit entries before saving
        var auditEntries = new List<AuditLog>();

        // Track Created entities to update EntityId after SaveChanges
        // (EntityId is 0/null before save, gets assigned after)
        var pendingCreatedAuditLogs = new List<(AuditLog auditLog, BaseEntity entity)>();

        // Track role changes (critical action)
        // Collect IDs during change tracking, then resolve names asynchronously after save
        var pendingRoleAudits = new List<(string Action, string TargetUserId, string RoleId, string? CachedUsername, string? CachedRoleName)>();

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            // Skip audit logging for excluded entities (high-frequency, low-value changes)
            var entityTypeName = entry.Entity.GetType().Name;
            if (AuditConfiguration.ExcludedEntityTypes.Contains(entityTypeName))
                continue;

            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    if (!string.IsNullOrEmpty(username))
                    {
                        entry.Entity.CreatedBy = username;
                    }

                    // Create audit log for new entity
                    var createdLog = _auditLogAppender.CreateAuditLog(entry, "Created", username, userId, ResolveUserIdToNickname, GetEntityDisplayName);
                    if (createdLog != null)
                    {
                        auditEntries.Add(createdLog);
                        // Track for EntityId update after SaveChanges
                        pendingCreatedAuditLogs.Add((createdLog, entry.Entity));
                    }
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    if (!string.IsNullOrEmpty(username))
                    {
                        entry.Entity.UpdatedBy = username;
                    }

                    // Skip audit logging for entities excluded from Modified-only tracking
                    if (AuditConfiguration.ModifiedExcludedEntityTypes.Contains(entityTypeName))
                        break;

                    // Check if this is a soft delete (DeletedAt field changed from null to a value)
                    var deletedAtProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "DeletedAt");
                    var isSoftDelete = deletedAtProperty != null
                        && deletedAtProperty.IsModified
                        && deletedAtProperty.OriginalValue == null
                        && deletedAtProperty.CurrentValue != null;

                    // Create audit log - use "Deleted" action for soft deletes, "Modified" otherwise
                    var action = isSoftDelete ? "Deleted" : "Modified";
                    var auditLog = _auditLogAppender.CreateAuditLog(entry, action, username, userId, ResolveUserIdToNickname, GetEntityDisplayName);
                    if (auditLog != null)
                    {
                        auditEntries.Add(auditLog);
                    }
                    break;

                case EntityState.Deleted:
                    // Create audit log for deleted entity
                    var deletedLog = _auditLogAppender.CreateAuditLog(entry, "Deleted", username, userId, ResolveUserIdToNickname, GetEntityDisplayName);
                    if (deletedLog != null)
                    {
                        auditEntries.Add(deletedLog);
                    }
                    break;
            }
        }

        // Also track ApplicationUser changes (not BaseEntity)
        foreach (var entry in ChangeTracker.Entries<ApplicationUser>())
        {
            if (entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
            {
                var action = entry.State == EntityState.Deleted ? "Deleted" : "Modified";
                var auditLog = _auditLogAppender.CreateAuditLogForUser(entry, action, username, userId);

                // Only add audit entry if there are meaningful changes to log
                if (auditLog != null)
                {
                    auditEntries.Add(auditLog);
                }
            }
        }

        // Track role changes (critical action)
        // Collect IDs during change tracking, then resolve names asynchronously after save

        foreach (var entry in ChangeTracker.Entries<IdentityUserRole<string>>())
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Deleted)
            {
                var action = entry.State == EntityState.Added ? "Role Added" : "Role Removed";

                // Try to resolve from Local cache only (no database queries)
                var targetUser = Users.Local.FirstOrDefault(u => u.Id == entry.Entity.UserId);
                var role = Roles.Local.FirstOrDefault(r => r.Id == entry.Entity.RoleId);

                // Store IDs and any cached values for async resolution after save
                pendingRoleAudits.Add((
                    action,
                    entry.Entity.UserId,
                    entry.Entity.RoleId,
                    targetUser?.UserName,
                    role?.Name
                ));
            }
        }

        var result = await SaveChangesWithRetryAsync(cancellationToken);

        // Update EntityId for Created audit logs now that IDs are assigned
        foreach (var (auditLog, entity) in pendingCreatedAuditLogs)
        {
            if (entity.Id > 0)
            {
                auditLog.EntityId = entity.Id;
            }
        }

        // Resolve any missing user/role names asynchronously after the main save
        if (pendingRoleAudits.Any())
        {
            // Collect IDs that need to be resolved
            var userIdsToResolve = pendingRoleAudits
                .Where(p => p.CachedUsername == null)
                .Select(p => p.TargetUserId)
                .Distinct()
                .ToList();

            var roleIdsToResolve = pendingRoleAudits
                .Where(p => p.CachedRoleName == null)
                .Select(p => p.RoleId)
                .Distinct()
                .ToList();

            // Asynchronously fetch missing users and roles in batch
            var resolvedUsers = userIdsToResolve.Any()
                ? await Users.Where(u => userIdsToResolve.Contains(u.Id))
                    .Select(u => new { u.Id, u.UserName })
                    .ToDictionaryAsync(u => u.Id, u => u.UserName, cancellationToken)
                : new Dictionary<string, string?>();

            var resolvedRoles = roleIdsToResolve.Any()
                ? await Roles.Where(r => roleIdsToResolve.Contains(r.Id))
                    .Select(r => new { r.Id, r.Name })
                    .ToDictionaryAsync(r => r.Id, r => r.Name, cancellationToken)
                : new Dictionary<string, string?>();

            // Create audit logs with resolved names
            foreach (var pending in pendingRoleAudits)
            {
                var targetUsername = pending.CachedUsername
                    ?? (resolvedUsers.TryGetValue(pending.TargetUserId, out var resolvedUser) ? resolvedUser : null)
                    ?? pending.TargetUserId;

                var roleName = pending.CachedRoleName
                    ?? (resolvedRoles.TryGetValue(pending.RoleId, out var resolvedRole) ? resolvedRole : null)
                    ?? pending.RoleId;

                auditEntries.Add(_auditLogAppender.CreateRoleAuditLog(
                    pending.Action,
                    pending.TargetUserId,
                    pending.RoleId,
                    targetUsername,
                    roleName,
                    username,
                    userId));
            }
        }

        // Save audit logs after successful save (disable auditing to prevent infinite loop)
        if (auditEntries.Any())
        {
            _isAuditingEnabled = false;
            try
            {
                AuditLogs.AddRange(auditEntries);
                await SaveChangesWithRetryAsync(cancellationToken);
            }
            finally
            {
                _isAuditingEnabled = true;
            }
        }

        return result;
    }

    /// <summary>
    /// Maximum number of retry attempts for SQLite "database table is locked" errors.
    /// </summary>
    private const int SqliteRetryCount = 3;

    /// <summary>
    /// Wraps base.SaveChangesAsync with automatic retry for SQLite "database table is locked"
    /// (Error 6). This transient error occurs when multiple connections contend for write access.
    /// </summary>
    private async Task<int> SaveChangesWithRetryAsync(CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return await base.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (attempt < SqliteRetryCount && IsSqliteTableLocked(ex))
            {
                // Exponential backoff: 50ms, 150ms, ...
                await Task.Delay(50 * (int)Math.Pow(2, attempt - 1), cancellationToken);
            }
        }
    }

    /// <summary>
    /// Checks whether a DbUpdateException was caused by SQLite Error 6 ("database table is locked").
    /// </summary>
    private static bool IsSqliteTableLocked(DbUpdateException ex)
    {
        return ex.InnerException is SqliteException { SqliteErrorCode: 6 };
    }

    /// <summary>
    /// Temporarily disables audit logging for operations that need to save multiple times
    /// but should only create a single audit log entry.
    /// Use with caution and always re-enable auditing afterwards.
    /// </summary>
    public void DisableAuditing()
    {
        _isAuditingEnabled = false;
    }

    /// <summary>
    /// Re-enables audit logging after it was temporarily disabled.
    /// </summary>
    public void EnableAuditing()
    {
        _isAuditingEnabled = true;
    }
}
