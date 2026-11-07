namespace RTUB.Core.Enums;

/// <summary>
/// Status of a logistics card
/// </summary>
public enum CardStatus
{
    Todo = 0,
    InProgress = 1,
    Done = 2,

    /// <summary>Task 067: waiting on something outside the card ("Bloqueado").</summary>
    Blocked = 3
}
