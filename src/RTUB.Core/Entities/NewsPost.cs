namespace RTUB.Core.Entities;

/// <summary>
/// One post of the public "Novidades" feed (/news, React track 025, docs/react-news.md): a text announcement written by
/// Admin/Owner. <see cref="PublishedAt"/> null is a draft, seen only by Admin/Owner; a date publishes it and orders the
/// feed. Limits and the author link live in NewsPostConfiguration.
/// </summary>
public class NewsPost : BaseEntity
{
    public const int TitleMaxLength = 150;
    public const int BodyMaxLength = 5000;

    public string? Title { get; set; }

    public string Body { get; set; } = string.Empty;

    public DateTime? PublishedAt { get; set; }

    /// <summary>
    /// Public (the default) or members only (#222). A members-only post never reaches a visitor: the server leaves it
    /// out of every anonymous read (docs/react-news.md → Visibility).
    /// </summary>
    public bool IsPublic { get; set; } = true;

    /// <summary>The member who wrote it; null once that account is deleted (the post stays).</summary>
    public string? AuthorId { get; set; }

    public virtual ApplicationUser? Author { get; set; }
}
