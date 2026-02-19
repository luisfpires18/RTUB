using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
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
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
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
    public DbSet<ConversationUserSettings> ConversationUserSettings { get; set; }

    // Gallery DbSets
    public DbSet<GalleryMedia> GalleryMedia { get; set; }
    public DbSet<GalleryMediaPersonTag> GalleryMediaPersonTags { get; set; }

    // Questions DbSets
    public DbSet<Question> Questions { get; set; }
    public DbSet<QuestionReply> QuestionReplies { get; set; }

    // Naipes (Educational Content) DbSets
    public DbSet<NaipeContent> NaipeContents => Set<NaipeContent>();
    public DbSet<NaipeComment> NaipeComments => Set<NaipeComment>();
    public DbSet<NaipePlayCount> NaipePlayCounts => Set<NaipePlayCount>();
    public DbSet<NaipeTypeConfig> NaipeTypeConfigs => Set<NaipeTypeConfig>();

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

        // Detach duplicate ApplicationUser entities before processing to avoid tracking conflicts
        // This prevents issues when entities with navigation properties to ApplicationUser are added
        DetachDuplicateApplicationUsers();

        // Explicitly trigger DetectChanges with error recovery BEFORE iterating entries.
        // ChangeTracker.Entries<T>() implicitly calls DetectChanges, which can cause
        // ApplicationUser tracking conflicts when navigation fixup introduces new instances.
        // By calling DetectChanges explicitly, we can catch and recover from identity conflicts.
        var savedAutoDetect = ChangeTracker.AutoDetectChangesEnabled;
        try
        {
            // First attempt: trigger change detection (may discover navigation changes
            // that introduce ApplicationUser entities conflicting with already-tracked ones)
            try
            {
                ChangeTracker.DetectChanges();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("cannot be tracked"))
            {
                // A tracking conflict was introduced during DetectChanges (e.g., duplicate
                // ApplicationUser or other entity instances via navigation fixup).
                // Clean up ALL tracked duplicates and retry.
                ChangeTracker.AutoDetectChangesEnabled = false;
                DetachDuplicateTrackedEntities();
                ChangeTracker.AutoDetectChangesEnabled = true;
                ChangeTracker.DetectChanges();
            }

            // Disable auto-detect for the rest of audit processing — DetectChanges already ran
            ChangeTracker.AutoDetectChangesEnabled = false;

            // Clean up any ApplicationUser duplicates introduced by DetectChanges
            DetachDuplicateApplicationUsers();
        }
        catch
        {
            ChangeTracker.AutoDetectChangesEnabled = savedAutoDetect;
            throw;
        }

        // Track role changes (critical action)
        // Collect IDs during change tracking, then resolve names asynchronously after save
        var pendingRoleAudits = new List<(string Action, string TargetUserId, string RoleId, string? CachedUsername, string? CachedRoleName)>();

        try
        {

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

        }
        catch
        {
            ChangeTracker.AutoDetectChangesEnabled = savedAutoDetect;
            throw;
        }

        // IMPORTANT: Keep AutoDetectChangesEnabled = false when calling base.SaveChangesAsync().
        // We already ran DetectChanges() and cleaned up duplicates above. If auto-detect is
        // re-enabled here, base.SaveChangesAsync() would call DetectChanges() again internally,
        // which can re-introduce navigation fixup conflicts (e.g., ApplicationUser or Enrollment
        // duplicates) AFTER our cleanup — causing "Unexpected entry.EntityState: Detached" errors.
        var result = await base.SaveChangesAsync(cancellationToken);

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
                await base.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                _isAuditingEnabled = true;
            }
        }

        // Restore auto-detect changes now that all saves are complete
        ChangeTracker.AutoDetectChangesEnabled = savedAutoDetect;

        return result;
    }


    /// <summary>
    /// Resolves a UserId to a user's nickname from the local cache.
    /// Returns null if user is not found in cache.
    /// </summary>
    private string? ResolveUserIdToNickname(string? userId)
    {
        if (string.IsNullOrEmpty(userId))
            return null;

        var user = Users.Local.FirstOrDefault(u => u.Id == userId);
        return user?.Nickname ?? user?.UserName;
    }


    /// <summary>
    /// Resolves the display name for an entity based on its type and ID
    /// Optimized to only use Local cache to avoid database queries during SaveChanges
    /// </summary>
    private string? GetEntityDisplayName(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        var entityType = entry.Entity.GetType().Name;

        try
        {
            switch (entityType)
            {
                case "Song":
                    if (entry.Entity is Song song)
                        return song.Title;
                    break;

                case "Event":
                    if (entry.Entity is Event evt)
                        return evt.Name;
                    break;

                case "Enrollment":
                    if (entry.Entity is Enrollment enrollment)
                    {
                        // Try navigation properties first (if loaded), then fall back to Local cache
                        var userName = enrollment.User?.Nickname
                            ?? enrollment.User?.UserName
                            ?? ResolveUserIdToNickname(enrollment.UserId);
                        var eventName = enrollment.Event?.Name
                            ?? Events.Local.FirstOrDefault(e => e.Id == enrollment.EventId)?.Name;
                        var attendStatus = enrollment.WillAttend ? "Vai" : "Não vai";

                        if (userName != null && eventName != null)
                            return $"{userName} - {eventName} - {attendStatus}";
                        if (eventName != null)
                            return $"{eventName} - {attendStatus}";
                        if (userName != null)
                            return $"{userName} - {attendStatus}";
                        return null; // Neither user name nor event found - will fall back to entity ID display
                    }
                    break;

                case "EventRepertoire":
                    if (entry.Entity is EventRepertoire repertoire)
                    {
                        // Try navigation properties first (if loaded), then fall back to Local cache
                        var eventName = repertoire.Event?.Name
                            ?? Events.Local.FirstOrDefault(e => e.Id == repertoire.EventId)?.Name;
                        var songTitle = repertoire.Song?.Title
                            ?? Songs.Local.FirstOrDefault(s => s.Id == repertoire.SongId)?.Title;

                        if (eventName != null && songTitle != null)
                            return $"{eventName} - {songTitle}";
                        return eventName ?? songTitle; // Return partial if one is missing
                    }
                    break;

                case "RehearsalAttendance":
                    if (entry.Entity is RehearsalAttendance attendance)
                    {
                        // Try navigation properties first (if loaded), then fall back to Local cache
                        var userName = attendance.User?.Nickname
                            ?? attendance.User?.UserName
                            ?? ResolveUserIdToNickname(attendance.UserId);
                        var rehearsal = attendance.Rehearsal
                            ?? Rehearsals.Local.FirstOrDefault(r => r.Id == attendance.RehearsalId);
                        var attendStatus = attendance.WillAttend ? "Vai" : "Não vai";

                        if (userName != null && rehearsal != null)
                            return $"{userName} - {rehearsal.Date:yyyy-MM-dd} - {attendStatus}";
                        if (rehearsal != null)
                            return $"{rehearsal.Date:yyyy-MM-dd} - {attendStatus}";
                        if (userName != null)
                            return $"{userName} - {attendStatus}";
                        return null; // Neither user name nor rehearsal found - will fall back to entity ID display
                    }
                    break;

                case "MeetingParticipation":
                    if (entry.Entity is MeetingParticipation meetingParticipation)
                    {
                        // Try navigation properties first (if loaded), then fall back to Local cache
                        var userName = meetingParticipation.User?.Nickname
                            ?? meetingParticipation.User?.UserName
                            ?? ResolveUserIdToNickname(meetingParticipation.UserId);
                        var participationMeeting = meetingParticipation.Meeting
                            ?? Meetings.Local.FirstOrDefault(m => m.Id == meetingParticipation.MeetingId);
                        var attendStatus = meetingParticipation.WillAttend ? "Vai" : "Não vai";

                        if (userName != null && participationMeeting != null)
                            return $"{userName} - {participationMeeting.Title} - {attendStatus}";
                        if (participationMeeting != null)
                            return $"{participationMeeting.Title} - {attendStatus}";
                        if (userName != null)
                            return $"{userName} - {attendStatus}";
                        return null; // Neither user name nor meeting found - will fall back to entity ID display
                    }
                    break;

                case "RoleAssignment":
                    if (entry.Entity is RoleAssignment roleAssignment)
                    {
                        // Try navigation property first (if loaded), then fall back to Local cache
                        var userName = roleAssignment.User?.Nickname
                            ?? roleAssignment.User?.UserName
                            ?? ResolveUserIdToNickname(roleAssignment.UserId)
                            ?? roleAssignment.UserId;
                        return $"{userName} - {roleAssignment.Position}";
                    }
                    break;

                case "SongYouTubeUrl":
                    if (entry.Entity is SongYouTubeUrl youtubeUrl)
                    {
                        // Try navigation property first (if loaded), then fall back to Local cache
                        var songTitle = youtubeUrl.Song?.Title
                            ?? Songs.Local.FirstOrDefault(s => s.Id == youtubeUrl.SongId)?.Title;
                        return songTitle;
                    }
                    break;

                case "Transaction":
                    if (entry.Entity is Transaction transaction && transaction.ActivityId.HasValue)
                    {
                        // Try navigation property first (if loaded), then fall back to Local cache
                        var activityName = transaction.Activity?.Name
                            ?? Activities.Local.FirstOrDefault(a => a.Id == transaction.ActivityId.Value)?.Name;
                        return activityName;
                    }
                    break;

                case "Activity":
                    if (entry.Entity is Activity activity2)
                        return activity2.Name;
                    break;

                case "Album":
                    if (entry.Entity is Album album)
                        return album.Title;
                    break;

                case "Instrument":
                    if (entry.Entity is Instrument instrument)
                        return $"{instrument.Category} - {instrument.Name}";
                    break;

                case "Label":
                    if (entry.Entity is Label label && !string.IsNullOrEmpty(label.Content))
                    {
                        return label.Content.Length > 100
                            ? label.Content[..100] + "..."
                            : label.Content;
                    }
                    break;

                case "Product":
                    if (entry.Entity is Product product)
                        return product.Name;
                    break;

                case "Rehearsal":
                    if (entry.Entity is Rehearsal rehearsal2)
                        return rehearsal2.Date.ToString("yyyy-MM-dd");
                    break;

                case "Report":
                    if (entry.Entity is Report report)
                        return report.Title;
                    break;

                case "Request":
                    // Request doesn't have a specific name field, use ID
                    return null;

                case "Slideshow":
                    // Slideshow can use Title
                    if (entry.Entity is Slideshow slideshow)
                        return slideshow.Title;
                    break;

                case "LogisticsBoard":
                    if (entry.Entity is LogisticsBoard logisticsBoard)
                        return logisticsBoard.Name;
                    break;

                case "LogisticsList":
                    if (entry.Entity is LogisticsList logisticsList)
                        return logisticsList.Name;
                    break;

                case "LogisticsCard":
                    if (entry.Entity is LogisticsCard logisticsCard)
                        return logisticsCard.Title;
                    break;

                case "Meeting":
                    if (entry.Entity is Meeting meeting)
                        return meeting.Title;
                    break;

                case "MeetingRequest":
                    if (entry.Entity is MeetingRequest meetingRequest)
                        return meetingRequest.Title;
                    break;

                case "LeaderboardComment":
                    if (entry.Entity is LeaderboardComment leaderboardComment)
                    {
                        // Try navigation properties first (if loaded), then fall back to Local cache
                        var targetName = leaderboardComment.TargetUser?.Nickname
                            ?? leaderboardComment.TargetUser?.UserName
                            ?? ResolveUserIdToNickname(leaderboardComment.TargetUserId)
                            ?? leaderboardComment.TargetUserId;
                        var authorName = leaderboardComment.Author?.Nickname
                            ?? leaderboardComment.Author?.UserName
                            ?? ResolveUserIdToNickname(leaderboardComment.AuthorId)
                            ?? leaderboardComment.AuthorId;
                        return $"{authorName} → {targetName}";
                    }
                    break;

                case "LeaderboardCommentLike":
                    if (entry.Entity is LeaderboardCommentLike commentLike)
                    {
                        // Try navigation properties first (if loaded), then fall back to Local cache
                        var userName = commentLike.User?.Nickname
                            ?? commentLike.User?.UserName
                            ?? ResolveUserIdToNickname(commentLike.UserId)
                            ?? commentLike.UserId;
                        var likedComment = commentLike.Comment
                            ?? LeaderboardComments.Local.FirstOrDefault(c => c.Id == commentLike.CommentId);
                        if (likedComment != null)
                        {
                            var targetName = likedComment.TargetUser?.Nickname
                                ?? likedComment.TargetUser?.UserName
                                ?? ResolveUserIdToNickname(likedComment.TargetUserId)
                                ?? likedComment.TargetUserId;
                            var commentPreview = likedComment.Text.Length > 30
                                ? likedComment.Text[..30] + "..."
                                : likedComment.Text;
                            return $"{userName} liked {targetName}'s comment: {commentPreview}";
                        }
                        return $"{userName} liked comment";
                    }
                    break;

                case "Post":
                    if (entry.Entity is Post post)
                    {
                        // Try navigation properties first (if loaded), then fall back to Local cache
                        var postDiscussion = post.Discussion
                            ?? Discussions.Local.FirstOrDefault(d => d.Id == post.DiscussionId);
                        if (postDiscussion != null)
                        {
                            var postEvent = postDiscussion.Event
                                ?? Events.Local.FirstOrDefault(e => e.Id == postDiscussion.EventId);
                            if (postEvent != null)
                                return $"{postEvent.Name} - {post.Title}";
                        }
                        return post.Title;
                    }
                    break;

                case "Comment":
                    if (entry.Entity is Comment comment)
                    {
                        // Try navigation properties first (if loaded), then fall back to Local cache
                        var authorName = comment.Author?.Nickname
                            ?? comment.Author?.UserName
                            ?? ResolveUserIdToNickname(comment.AuthorId)
                            ?? comment.AuthorId;
                        var bodyPreview = comment.Body.Length > 50
                            ? comment.Body[..50] + "..."
                            : comment.Body;

                        // Try to get event name through Post -> Discussion -> Event
                        var commentPost = comment.Post
                            ?? Posts.Local.FirstOrDefault(p => p.Id == comment.PostId);
                        if (commentPost != null)
                        {
                            var commentDiscussion = commentPost.Discussion
                                ?? Discussions.Local.FirstOrDefault(d => d.Id == commentPost.DiscussionId);
                            if (commentDiscussion != null)
                            {
                                var commentEvent = commentDiscussion.Event
                                    ?? Events.Local.FirstOrDefault(e => e.Id == commentDiscussion.EventId);
                                if (commentEvent != null)
                                    return $"{commentEvent.Name} - {authorName}: {bodyPreview}";
                            }
                        }

                        return $"{authorName}: {bodyPreview}";
                    }
                    break;

                case "MemberInstrument":
                    if (entry.Entity is MemberInstrument memberInstrument)
                    {
                        // Try navigation property first (if loaded), then fall back to Local cache
                        var userName = memberInstrument.Member?.Nickname
                            ?? memberInstrument.Member?.UserName
                            ?? ResolveUserIdToNickname(memberInstrument.MemberId)
                            ?? memberInstrument.MemberId;
                        var instrumentName = RTUB.Core.Helpers.InstrumentTypeHelper.GetDisplayName(memberInstrument.InstrumentType);
                        return $"{userName} - {instrumentName}";
                    }
                    break;

                case "PushSubscription":
                    if (entry.Entity is PushSubscription pushSubscription)
                    {
                        // Try navigation property first (if loaded), then fall back to Local cache
                        var userName = pushSubscription.User?.Nickname
                            ?? pushSubscription.User?.UserName
                            ?? ResolveUserIdToNickname(pushSubscription.UserId)
                            ?? pushSubscription.UserId;
                        return userName;
                    }
                    break;

                case "Question":
                    if (entry.Entity is Question question)
                    {
                        // Show the question title for display
                        return question.Title;
                    }
                    break;

                case "Bet":
                    if (entry.Entity is Bet bet)
                    {
                        return bet.Title;
                    }
                    break;

                case "BetComment":
                    if (entry.Entity is BetComment betComment)
                    {
                        // Try navigation properties first (if loaded), then fall back to Local cache
                        var authorName = betComment.Author?.Nickname
                            ?? betComment.Author?.UserName
                            ?? ResolveUserIdToNickname(betComment.AuthorId)
                            ?? betComment.AuthorId;
                        var betEntity = betComment.Bet
                            ?? Bets.Local.FirstOrDefault(b => b.Id == betComment.BetId);
                        var betName = betEntity?.Title ?? $"Aposta #{betComment.BetId}";
                        var textPreview = string.IsNullOrEmpty(betComment.Text)
                            ? "[Media]"
                            : (betComment.Text.Length > 50 ? betComment.Text[..50] + "..." : betComment.Text);
                        return $"{authorName} em {betName}: {textPreview}";
                    }
                    break;

                case "GameScore":
                    if (entry.Entity is GameScore gameScore)
                    {
                        // Try to get the game title from local cache based on GameKey
                        var game = Games.Local.FirstOrDefault(g => g.Key == gameScore.GameKey);
                        var gameName = game?.Title ?? gameScore.GameKey;
                        return gameName;
                    }
                    break;

                case "Character":
                    if (entry.Entity is Character character)
                    {
                        // Try navigation property first (if loaded), then fall back to Local cache
                        var userName = character.User?.Nickname
                            ?? character.User?.UserName
                            ?? ResolveUserIdToNickname(character.UserId)
                            ?? character.UserId;
                        return $"Character - {userName} (Level {character.Level})";
                    }
                    break;
            }
        }
        catch (InvalidOperationException)
        {
            // If resolution fails due to database query issues, return null (will fall back to ID display)
            return null;
        }

        return null;
    }

    /// <summary>
    /// Detaches duplicate ApplicationUser entities to prevent tracking conflicts.
    /// This is necessary when entities with navigation properties to ApplicationUser are added,
    /// as EF Core may try to track the same ApplicationUser instance multiple times.
    /// This method also updates navigation properties of Added/Modified entities to reference the kept instance.
    /// </summary>
    private void DetachDuplicateApplicationUsers()
    {
        // Disable auto-detect changes to prevent EF Core from triggering relationship fixup
        // when we access/modify navigation properties. This prevents issues where unrelated
        // entities (like Meeting) get their state changed during this operation.
        var wasAutoDetectChangesEnabled = ChangeTracker.AutoDetectChangesEnabled;
        try
        {
            ChangeTracker.AutoDetectChangesEnabled = false;

            // Get all ApplicationUser entries that are currently being tracked
            var trackedUsers = ChangeTracker.Entries<ApplicationUser>()
                .Where(e => e.State != EntityState.Detached)
                .ToList();

            // Fix phantom "Added" users: if an ApplicationUser has an Id but is in Added state,
            // AND there is already another tracked instance with the same key (in a non-Added state),
            // it's a tracking artifact from navigation fixup. These cause UNIQUE constraint failures
            // when SaveChangesAsync tries to INSERT them.
            // NOTE: We only mark as Unchanged if a DUPLICATE exists, because IdentityUser always
            // generates a GUID Id at construction — so a lone Added user with an Id is a legitimate insert.
            var addedUsers = trackedUsers.Where(e => e.State == EntityState.Added && !string.IsNullOrEmpty(e.Entity.Id)).ToList();
            foreach (var entry in addedUsers)
            {
                var hasDuplicate = trackedUsers.Any(e =>
                    e != entry &&
                    e.State != EntityState.Added &&
                    e.State != EntityState.Detached &&
                    e.Entity.Id == entry.Entity.Id);

                if (hasDuplicate)
                {
                    entry.State = EntityState.Unchanged;
                }
            }

            // Build a canonical map: one ApplicationUser instance per key.
            // Prefer Modified > Unchanged > Added states to keep the most meaningful version.
            var canonicalUsers = new Dictionary<string, ApplicationUser>();
            var entriesToDetach = new List<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<ApplicationUser>>();

            var sortedUsers = trackedUsers
                .OrderBy(e => e.State == EntityState.Modified ? 0 : e.State == EntityState.Unchanged ? 1 : 2)
                .ToList();

            foreach (var entry in sortedUsers)
            {
                var uid = entry.Entity.Id;
                if (string.IsNullOrEmpty(uid)) continue;

                if (!canonicalUsers.ContainsKey(uid))
                {
                    canonicalUsers[uid] = entry.Entity;
                }
                else
                {
                    entriesToDetach.Add(entry);
                }
            }

            // Normalize ApplicationUser navigation references on Added/Modified entities
            // to point to the canonical (kept) instance, preventing conflicts during save.
            foreach (var entityEntry in ChangeTracker.Entries())
            {
                if (entityEntry.State == EntityState.Added || entityEntry.State == EntityState.Modified)
                {
                    foreach (var navigation in entityEntry.Navigations)
                    {
                        if (navigation.CurrentValue is ApplicationUser navUser && !string.IsNullOrEmpty(navUser.Id))
                        {
                            if (canonicalUsers.TryGetValue(navUser.Id, out var canonical))
                            {
                                if (!ReferenceEquals(navUser, canonical))
                                {
                                    navigation.CurrentValue = canonical;
                                }
                            }
                        }
                    }
                }
            }

            // Detach all duplicate entries
            foreach (var entry in entriesToDetach)
            {
                entry.State = EntityState.Detached;
            }
        }
        finally
        {
            ChangeTracker.AutoDetectChangesEnabled = wasAutoDetectChangesEnabled;
        }
    }

    /// <summary>
    /// Detaches duplicate tracked entities of ANY type to prevent tracking conflicts.
    /// This is a broader version of DetachDuplicateApplicationUsers that handles cases
    /// where navigation fixup during DetectChanges introduces duplicate instances of
    /// non-ApplicationUser entities (e.g., Enrollment, Event, etc.).
    /// </summary>
    private void DetachDuplicateTrackedEntities()
    {
        var wasAutoDetectChangesEnabled = ChangeTracker.AutoDetectChangesEnabled;
        try
        {
            ChangeTracker.AutoDetectChangesEnabled = false;

            // First, run the ApplicationUser-specific cleanup (handles phantom Added users + nav fixup)
            DetachDuplicateApplicationUsers();

            // Now handle duplicates of any other entity type.
            // Group tracked entries by (CLR type, primary key) and detach duplicates.
            var entriesToDetach = new List<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry>();

            var trackedEntries = ChangeTracker.Entries()
                .Where(e => e.State != EntityState.Detached)
                .ToList();

            // Group by CLR type for efficiency
            var entryGroups = trackedEntries.GroupBy(e => e.Entity.GetType());

            foreach (var group in entryGroups)
            {
                // Skip ApplicationUser — already handled by the specialized method above
                if (group.Key == typeof(ApplicationUser))
                    continue;

                var entityType = Model.FindEntityType(group.Key);
                var primaryKey = entityType?.FindPrimaryKey();
                if (primaryKey == null) continue;

                var seen = new Dictionary<string, Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry>();

                // Prefer Modified > Unchanged > Added to keep the most meaningful instance
                var sorted = group
                    .OrderBy(e => e.State == EntityState.Modified ? 0 : e.State == EntityState.Unchanged ? 1 : 2)
                    .ToList();

                foreach (var entry in sorted)
                {
                    var keyValues = primaryKey.Properties
                        .Select(p => entry.Property(p.Name).CurrentValue?.ToString() ?? "null");
                    var compositeKey = string.Join("|", keyValues);

                    if (!seen.ContainsKey(compositeKey))
                    {
                        seen[compositeKey] = entry;
                    }
                    else
                    {
                        entriesToDetach.Add(entry);
                    }
                }
            }

            foreach (var entry in entriesToDetach)
            {
                entry.State = EntityState.Detached;
            }
        }
        finally
        {
            ChangeTracker.AutoDetectChangesEnabled = wasAutoDetectChangesEnabled;
        }
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
