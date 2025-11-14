namespace RTUB.Core.Enums;

/// <summary>
/// Represents the status of a poll
/// </summary>
public enum PollStatus
{
    /// <summary>
    /// Poll is scheduled to start in the future
    /// </summary>
    Scheduled = 0,
    
    /// <summary>
    /// Poll is currently active and accepting votes
    /// </summary>
    Active = 1,
    
    /// <summary>
    /// Poll has been closed and is no longer accepting votes
    /// </summary>
    Closed = 2
}
