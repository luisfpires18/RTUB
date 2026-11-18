using Microsoft.EntityFrameworkCore;
using RTUB.Core.Entities;

namespace RTUB.Application.Data;

// DbSet properties of every entity.

public partial class ApplicationDbContext
{
    // Domain entity DbSets
    public DbSet<Event> Events { get; set; }

    public DbSet<Enrollment> Enrollments { get; set; }

    public DbSet<Album> Albums { get; set; }

    public DbSet<AlbumAccess> AlbumAccesses { get; set; }

    public DbSet<Song> Songs { get; set; }

    public DbSet<SongYouTubeUrl> SongYouTubeUrls { get; set; }

    public DbSet<SongVideo> SongVideos { get; set; } = null!;

    public DbSet<SongPlayAlong> SongPlayAlongs { get; set; } = null!;

    public DbSet<SongPlayAlongFavorite> SongPlayAlongFavorites { get; set; } = null!;

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

    public DbSet<Request> Requests { get; set; }

    public DbSet<RoleAssignment> RoleAssignments { get; set; }

    public DbSet<Rehearsal> Rehearsals { get; set; }

    public DbSet<RehearsalAttendance> RehearsalAttendances { get; set; }

    // Inventory & Shop DbSets
    public DbSet<Instrument> Instruments { get; set; }

    public DbSet<Product> Products { get; set; }

    public DbSet<ProductReservation> ProductReservations { get; set; }

    public DbSet<ShopPromotion> ShopPromotions { get; set; }

    public DbSet<ShopPromotionItem> ShopPromotionItems { get; set; }

    public DbSet<Trophy> Trophies { get; set; }

    // Logistics Board DbSets
    public DbSet<LogisticsBoard> LogisticsBoards { get; set; }

    public DbSet<LogisticsList> LogisticsLists { get; set; }

    public DbSet<LogisticsCard> LogisticsCards { get; set; }

    public DbSet<LogisticsCardAssignment> LogisticsCardAssignments { get; set; }

    public DbSet<LogisticsCardReminder> LogisticsCardReminders { get; set; }

    public DbSet<LogisticsCardComment> LogisticsCardComments { get; set; }

    public DbSet<LogisticsCardActivity> LogisticsCardActivities { get; set; }

    public DbSet<LogisticsCardAttachment> LogisticsCardAttachments { get; set; }

    public DbSet<LogisticsBoardMember> LogisticsBoardMembers { get; set; }

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

    public DbSet<MeetingPoll> MeetingPolls { get; set; } = null!;

    public DbSet<MeetingPollOption> MeetingPollOptions { get; set; } = null!;

    public DbSet<MeetingPollVote> MeetingPollVotes { get; set; } = null!;

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

    // News feed DbSet
    public DbSet<NewsPost> NewsPosts { get; set; }

    // Questions DbSets
    public DbSet<Question> Questions { get; set; }

    public DbSet<QuestionReply> QuestionReplies { get; set; }

    // Naipes (Educational Content) DbSets
    public DbSet<NaipeContent> NaipeContents { get; set; } = null!;

    public DbSet<NaipeComment> NaipeComments { get; set; } = null!;

    public DbSet<NaipePlayCount> NaipePlayCounts { get; set; } = null!;

    public DbSet<NaipeTypeConfig> NaipeTypeConfigs { get; set; } = null!;

    // Owner dashboard: sign-ins and hours seen
    public DbSet<LoginEvent> LoginEvents { get; set; } = null!;

    // Page views per day and route for /owner/dashboard
    public DbSet<PageView> PageViews { get; set; } = null!;

    // Event Contact DbSet
    public DbSet<EventContact> EventContacts { get; set; }

    // As minhas ações pendentes: a member's own dismissals of optional actions
    public DbSet<PendingActionDismissal> PendingActionDismissals { get; set; } = null!;

    // Push preferences: the push kinds a member turned off, per account
    public DbSet<PushPreference> PushPreferences { get; set; } = null!;
}
