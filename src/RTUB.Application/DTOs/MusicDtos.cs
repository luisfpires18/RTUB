namespace RTUB.Application.DTOs;

// Contracts of the React Music area (React track 006, docs/react-music.md). Built for the browser:
// no EF entity, storage object key, user id of another member or audit field ever leaves the server.

/// <summary>What the caller may do; the server enforces the same rules on every write.</summary>
public sealed record MusicPermissionsDto(
    bool IsMember,
    bool CanManage,
    bool CanDelete,
    bool CanManageExclusive,
    bool CanSeeStatistics,
    bool CanSeeDetailedStatistics);

public sealed record MusicAlbumSummaryDto(
    int Id,
    string Title,
    int? Year,
    string? Description,
    string? CoverUrl,
    bool IsPrivate,
    bool IsExclusive,
    int SongCount);

public sealed record MusicAlbumListDto(IReadOnlyList<MusicAlbumSummaryDto> Albums, MusicPermissionsDto Permissions);

public sealed record MusicLinkDto(string Kind, string Url);

public sealed record MusicSongDto(
    int Id,
    string Title,
    int? TrackNumber,
    string? LyricAuthor,
    string? MusicAuthor,
    string? Adaptation,
    bool HasAudio,
    int PlayCount,
    IReadOnlyList<MusicLinkDto> Links,
    int? VideoCount,
    bool HasPlayAlong);

public sealed record MusicAlbumDetailDto(
    MusicAlbumSummaryDto Album,
    IReadOnlyList<MusicSongDto> Songs,
    MusicPermissionsDto Permissions);

/// <summary>A member who can be given access to an exclusive album (Owner-only list).</summary>
public sealed record MusicMemberDto(string Id, string DisplayName, string AvatarUrl);

/// <summary>Everything the album form edits, including the Owner-only access list.</summary>
public sealed record MusicAlbumEditDto(
    int Id,
    string Title,
    int? Year,
    string? Description,
    bool IsPrivate,
    bool IsExclusive,
    string? CoverUrl,
    IReadOnlyList<MusicMemberDto> AccessMembers);

/// <summary>Everything the song form edits (lyrics text included; it is not in the album payload).</summary>
public sealed record MusicSongEditDto(
    int Id,
    string Title,
    int? TrackNumber,
    string? LyricAuthor,
    string? MusicAuthor,
    string? Adaptation,
    string? SpotifyUrl,
    bool HasAudio,
    IReadOnlyList<string> YouTubeUrls,
    string? Lyrics,
    bool HasPlayAlong);

// ---------- Tocar junto (task 094): the play-along chart, version 1. Times are seconds into the recording. ----------

/// <summary>A chord change. <see cref="Label"/> is the name as the songbook writes it (FÁ, RÉm),
/// <see cref="Name"/> the same chord in letters (F, Dm). In a sung line it sits on a word and a letter.</summary>
public sealed record PlayAlongChordDto(string Label, string Name, double Time, int? Word, int? Char);

/// <summary>A lyric line with a time per word, or an instrumental line (no words, only chords).</summary>
public sealed record PlayAlongLineDto(
    string Text,
    double Start,
    double End,
    IReadOnlyList<string> Words,
    IReadOnlyList<double> WordTimes,
    IReadOnlyList<PlayAlongChordDto> Chords);

/// <summary><see cref="Kind"/> is verse, refrain or instrumental.</summary>
public sealed record PlayAlongSectionDto(string Name, string Kind, double Start, double End, IReadOnlyList<PlayAlongLineDto> Lines);

public sealed record PlayAlongChartDto(
    int Version,
    double Duration,
    double Bpm,
    IReadOnlyList<double> Beats,
    IReadOnlyList<PlayAlongSectionDto> Sections);

/// <summary>What the play-along page needs for one song (members only).</summary>
public sealed record MusicPlayAlongDto(int SongId, string Title, int AlbumId, string AlbumTitle, bool HasAudio, PlayAlongChartDto Chart);

/// <summary>Lyrics: a short-lived PDF link when the songbook has one, the stored text otherwise.</summary>
public sealed record MusicLyricsDto(string? PdfUrl, string? Text);

/// <summary>A short-lived audio link, and whether this play was counted (cooldown).</summary>
public sealed record MusicPlayDto(string AudioUrl, bool Counted, int PlayCount);

public sealed record MusicVideoDto(int Id, string Title, string Url, string MimeType, bool CanDelete);

public sealed record MusicSongRankDto(int SongId, string Title, int AlbumId, string AlbumTitle, int PlayCount);

public sealed record MusicAlbumRankDto(int AlbumId, string Title, int? Year, int PlayCount);

public sealed record MusicMemberSongDto(string SongTitle, string AlbumTitle, int PlayCount);

public sealed record MusicMemberStatsDto(string DisplayName, string? FullName, string AvatarUrl, int TotalPlays, IReadOnlyList<MusicMemberSongDto> Songs);

/// <summary>Play totals over the albums the caller can see; <see cref="Members"/> only for Admin/Owner.</summary>
public sealed record MusicStatisticsDto(
    IReadOnlyList<MusicSongRankDto> Songs,
    IReadOnlyList<MusicAlbumRankDto> Albums,
    IReadOnlyList<MusicMemberStatsDto>? Members);

public sealed record MusicAlbumInput(
    string? Title,
    int? Year,
    string? Description,
    bool IsPrivate,
    bool IsExclusive,
    IReadOnlyList<string>? AccessUserIds);

/// <summary>A cropped cover image as uploaded (WebP from the React cropper).</summary>
public sealed record MusicCoverUpload(Stream Content, string FileName, string ContentType, long Length);

public sealed record MusicSongInput(
    string? Title,
    int? TrackNumber,
    string? LyricAuthor,
    string? MusicAuthor,
    string? Adaptation,
    string? SpotifyUrl,
    bool HasAudio,
    IReadOnlyList<string>? YouTubeUrls,
    string? Lyrics);

public sealed record MusicVideoUpload(Stream Content, string FileName, string ContentType, long Length, string? Title);

public enum MusicResultStatus
{
    Ok,
    NotFound,
    /// <summary>The album is private and the caller is not signed in.</summary>
    SignInRequired,
    Forbidden,
    Invalid,
    /// <summary>The song has no audio file in storage.</summary>
    Unavailable,
}

/// <summary>The outcome of a Music operation; endpoints map <see cref="Status"/> onto HTTP.</summary>
public sealed record MusicResult<T>(MusicResultStatus Status, T? Value = default, IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public static MusicResult<T> Ok(T value) => new(MusicResultStatus.Ok, value);
    public static MusicResult<T> Fail(MusicResultStatus status) => new(status);
    public static MusicResult<T> Invalid(IReadOnlyDictionary<string, string[]> errors) => new(MusicResultStatus.Invalid, default, errors);
}
