namespace RTUB.Application.DTOs;

/// <summary>
/// Summary of emoji reactions for a single emoji on a message
/// </summary>
public class MessageReactionSummaryDto
{
    public string Emoji { get; set; } = string.Empty;
    public int Count { get; set; }

    /// <summary>
    /// True if the current user has reacted with this emoji
    /// </summary>
    public bool ReactedByCurrentUser { get; set; }

    /// <summary>
    /// IDs of users who reacted (used for tooltips)
    /// </summary>
    public List<string> UserIds { get; set; } = [];
}
