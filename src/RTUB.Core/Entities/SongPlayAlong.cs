using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// The play-along chart of a song (docs/react-music.md → Tocar junto): the timed chords, lyric
/// lines and beats of the song's recording, as validated JSON. One per song, in its own table so the
/// song queries never carry it.
/// </summary>
public class SongPlayAlong : BaseEntity
{
    [Required]
    public int SongId { get; set; }

    /// <summary>The chart as normalized JSON (<c>PlayAlongChart</c> in RTUB.Application writes and reads it).</summary>
    [Required]
    public string Chart { get; set; } = string.Empty;

    /// <summary>How many times members opened the Aprender page of the song.</summary>
    public int Views { get; set; }

    public virtual Song Song { get; set; } = null!;
}
