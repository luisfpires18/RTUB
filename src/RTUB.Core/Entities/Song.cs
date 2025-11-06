using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a song within an album
/// </summary>
public class Song : BaseEntity
{
    [Required(ErrorMessage = "O título da música é obrigatório")]
    [MaxLength(200, ErrorMessage = "O título da música não pode exceder 200 caracteres")]
    public string Title { get; set; } = string.Empty;
    
    [Range(1, 999, ErrorMessage = "O número da faixa deve estar entre 1 e 999")]
    public int? TrackNumber { get; set; }
    
    [MaxLength(200, ErrorMessage = "O autor da letra não pode exceder 200 caracteres")]
    public string? LyricAuthor { get; set; }
    
    [MaxLength(200, ErrorMessage = "O autor da música não pode exceder 200 caracteres")]
    public string? MusicAuthor { get; set; }
    
    [MaxLength(200, ErrorMessage = "A adaptação não pode exceder 200 caracteres")]
    public string? Adaptation { get; set; }
    
    [MaxLength(10000, ErrorMessage = "A letra não pode exceder 10000 caracteres")]
    public string? Lyrics { get; set; }
    
    [Range(1, 7200, ErrorMessage = "A duração deve estar entre 1 e 7200 segundos")]
    public int? Duration { get; set; } // Duration in seconds
    
    [MaxLength(500, ErrorMessage = "O URL do Spotify não pode exceder 500 caracteres")]
    public string? SpotifyUrl { get; set; }
    
    public bool HasMusic { get; set; } = false;
    
    [Required(ErrorMessage = "O álbum é obrigatório")]
    public int AlbumId { get; set; }
    
    // Navigation properties
    public virtual Album? Album { get; set; }
    public virtual ICollection<SongYouTubeUrl> YouTubeUrls { get; set; } = new List<SongYouTubeUrl>();
    public virtual ICollection<EventRepertoire> EventRepertoires { get; set; } = new List<EventRepertoire>();

    // Private constructor for EF Core
    public Song() { }

    public static Song Create(string title, int albumId, int? trackNumber = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("O título da música não pode estar vazio", nameof(title));

        return new Song
        {
            Title = title,
            AlbumId = albumId,
            TrackNumber = trackNumber
        };
    }

    public void UpdateDetails(string title, int? trackNumber, string? lyricAuthor, string? musicAuthor, string? adaptation, int? duration)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("O título da música não pode estar vazio", nameof(title));

        Title = title;
        TrackNumber = trackNumber;
        LyricAuthor = lyricAuthor;
        MusicAuthor = musicAuthor;
        Adaptation = adaptation;
        Duration = duration;
    }

    public void SetLyrics(string? lyrics)
    {
        Lyrics = lyrics;
    }

    public void SetSpotifyUrl(string? url)
    {
        SpotifyUrl = url;
    }

    public void SetHasMusic(bool hasMusic)
    {
        HasMusic = hasMusic;
    }
}
