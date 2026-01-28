namespace RTUB.Application.DTOs;

/// <summary>
/// Enum representing the position of a message within a group of consecutive messages from the same sender.
/// Used for UI display purposes to determine avatar and timestamp visibility.
/// </summary>
public enum MessageGroupPosition
{
    /// <summary>
    /// Only message in the group
    /// </summary>
    Single,

    /// <summary>
    /// First message in a group
    /// </summary>
    First,

    /// <summary>
    /// Middle message in a group
    /// </summary>
    Middle,

    /// <summary>
    /// Last message in a group
    /// </summary>
    Last
}
