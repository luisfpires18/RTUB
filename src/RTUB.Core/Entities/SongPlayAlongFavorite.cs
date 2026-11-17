namespace RTUB.Core.Entities;
/// <summary>A member's star on an Aprender song: starred songs come first on /music/learn (098).</summary>
public class SongPlayAlongFavorite : BaseEntity
{
    public int SongId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public virtual Song Song { get; set; } = null!;
}
