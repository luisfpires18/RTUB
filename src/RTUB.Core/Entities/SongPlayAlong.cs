using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// The play-along chart of a song (task 094, docs/react-music.md → Tocar junto): the timed chords, lyric
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

    public virtual Song Song { get; set; } = null!;
}
