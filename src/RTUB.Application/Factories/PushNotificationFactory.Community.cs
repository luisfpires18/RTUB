using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Factories;

// Push factory: discussions, leaderboard, songs, naipes, gallery tags and mentions.
public partial class PushNotificationFactory
{
    /// <summary>
    /// Creates a push notification for new discussion posts.
    /// </summary>
    public SendPushNotificationDto CreateDiscussionPostNotification(Event @event, string authorNickname, string postTitle)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorNickname);
        ArgumentException.ThrowIfNullOrWhiteSpace(postTitle);

        var eventUrl = DiscussionUrl(@event.Id);

        return new SendPushNotificationDto
        {
            Title = $"Novo post de {authorNickname}",
            Body = $"{postTitle} - {@event.Name}",
            Icon = "/icons/rtub-logo-192.png",
            Url = eventUrl,
            Tag = $"event-discussion-{@event.Id}",
            PreferenceKey = PushPreferenceCatalog.DiscussionActivity
        };
    }

    /// <summary>
    /// Creates a push notification for new discussion comments.
    /// </summary>
    public SendPushNotificationDto CreateDiscussionCommentNotification(Event @event, string authorNickname, string postTitle)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorNickname);
        ArgumentException.ThrowIfNullOrWhiteSpace(postTitle);

        var eventUrl = DiscussionUrl(@event.Id);

        return new SendPushNotificationDto
        {
            Title = $"Novo comentário de {authorNickname}",
            Body = $"{postTitle} - {@event.Name}",
            Icon = "/icons/rtub-logo-192.png",
            Url = eventUrl,
            Tag = $"event-discussion-{@event.Id}",
            PreferenceKey = PushPreferenceCatalog.DiscussionActivity
        };
    }

    /// <summary>
    /// Creates a push notification for new 1st place in leaderboard.
    /// </summary>
    public SendPushNotificationDto CreateLeaderboardFirstPlaceNotification(string userNickname, int level)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userNickname);

        var leaderboardUrl = "/leaderboard";

        return new SendPushNotificationDto
        {
            Title = "Novo 1º Lugar no Ranking!",
            Body = $"{userNickname} - Nível {level}",
            Icon = "/icons/rtub-logo-192.png",
            Url = leaderboardUrl,
            Tag = "leaderboard-first-place",
            PreferenceKey = PushPreferenceCatalog.LeaderboardFirstPlace
        };
    }

    /// <summary>
    /// Creates a push notification when someone comments on a user's leaderboard profile.
    /// </summary>
    public SendPushNotificationDto CreateLeaderboardCommentNotification(string authorName, string targetUserName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorName);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetUserName);

        var leaderboardUrl = "/leaderboard";

        return new SendPushNotificationDto
        {
            Title = "Novo comentário no teu perfil",
            Body = $"{authorName} comentou no teu perfil",
            Icon = "/icons/rtub-logo-192.png",
            Url = leaderboardUrl,
            Tag = "leaderboard-comment",
            PreferenceKey = PushPreferenceCatalog.LeaderboardComments
        };
    }

    /// <summary>
    /// Creates a push notification when someone likes a user's comment.
    /// </summary>
    public SendPushNotificationDto CreateLeaderboardCommentLikeNotification(string likerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(likerName);

        var leaderboardUrl = "/leaderboard";

        return new SendPushNotificationDto
        {
            Title = "Novo like no teu comentário",
            Body = $"{likerName} gostou do teu comentário",
            Icon = "/icons/rtub-logo-192.png",
            Url = leaderboardUrl,
            Tag = "leaderboard-comment-like",
            PreferenceKey = PushPreferenceCatalog.LeaderboardComments
        };
    }

    /// <summary>
    /// Creates a push notification when someone uploads a video to a song.
    /// </summary>
    public SendPushNotificationDto CreateSongVideoUploadNotification(Song song, string uploaderName)
    {
        ArgumentNullException.ThrowIfNull(song);
        ArgumentException.ThrowIfNullOrWhiteSpace(uploaderName);

        var musicUrl = $"/music/albums/{song.AlbumId}";

        return new SendPushNotificationDto
        {
            Title = "Novo vídeo",
            Body = $"{uploaderName} adicionou um vídeo a \"{song.Title}\"",
            Icon = "/icons/rtub-logo-192.png",
            Url = musicUrl,
            Tag = $"song-video-{song.Id}",
            PreferenceKey = PushPreferenceCatalog.MediaNew
        };
    }

    /// <summary>
    /// A song is new in Aprender: the Owner sends it from /music/learn; it opens the song's Aprender page.
    /// </summary>
    public SendPushNotificationDto CreateSongLearnNotification(int songId, string songTitle, string? message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(songTitle);

        return new SendPushNotificationDto
        {
            Title = "Nova música para aprender",
            Body = string.IsNullOrWhiteSpace(message) ? $"Já podes aprender \"{songTitle}\" com os acordes a acompanhar a gravação." : message.Trim(),
            Icon = "/icons/rtub-logo-192.png",
            Url = $"/music/play/{songId}",
            Tag = $"song-learn-{songId}",
            PreferenceKey = PushPreferenceCatalog.MediaNew
        };
    }

    /// <summary>
    /// Creates a push notification when new naipe content (video or image) is published.
    /// </summary>
    public SendPushNotificationDto CreateNaipeContentNotification(string contentTitle, InstrumentType instrumentType, bool isVideo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentTitle);
        var instrumentTypeName = StatusHelper.GetInstrumentDisplay(instrumentType);

        var naipesUrl = $"/naipes?instrument={instrumentType}";
        var contentType = isVideo ? "vídeo" : "imagem";

        return new SendPushNotificationDto
        {
            Title = $"Novo {contentType} em {instrumentTypeName}",
            Body = $"\"{TruncateContent(contentTitle, 60)}\" foi adicionado aos Naipes",
            Icon = "/icons/rtub-logo-192.png",
            Url = naipesUrl,
            Tag = $"naipe-content-{instrumentTypeName.ToLower().Replace(" ", "-")}",
            PreferenceKey = PushPreferenceCatalog.MediaNew
        };
    }

    /// <summary>
    /// Creates a push notification when a user is tagged in a gallery media item.
    /// </summary>
    public SendPushNotificationDto CreateGalleryTagNotification(int mediaId)
    {

        var galleryUrl = $"/gallery?item={mediaId}";

        return new SendPushNotificationDto
        {
            Title = "Nova media na Galeria",
            Body = "Foste marcado numa nova foto ou vídeo.",
            Icon = "/icons/rtub-logo-192.png",
            Url = galleryUrl,
            Tag = "gallery-tag",
            PreferenceKey = PushPreferenceCatalog.GalleryTag
        };
    }

    /// <summary>
    /// Creates a push notification when a user is @mentioned in a discussion post or comment.
    /// </summary>
    public SendPushNotificationDto CreateMentionNotification(Event @event, string mentionerNickname, string postTitle, bool isComment)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentException.ThrowIfNullOrWhiteSpace(mentionerNickname);
        ArgumentException.ThrowIfNullOrWhiteSpace(postTitle);

        var discussionUrl = DiscussionUrl(@event.Id);
        var context = isComment ? $"num comentário sobre: {postTitle}" : $"num post: {postTitle}";

        return new SendPushNotificationDto
        {
            Title = $"{mentionerNickname} mencionou-te",
            Body = $"{@event.Name} - {context}",
            Icon = "/icons/rtub-logo-192.png",
            Url = discussionUrl,
            Tag = $"mention-{@event.Id}",
            PreferenceKey = PushPreferenceCatalog.Mentions
        };
    }
}
