namespace RTUB.Core.Enums;

/// <summary>Who sees a Logística board (task 067). Mod, Admin and Owner always do.</summary>
public enum BoardVisibility
{
    /// <summary>Every signed-in member, Leitões included (every board before 067).</summary>
    Members = 0,

    /// <summary>Only the members on the board's access list.</summary>
    Private = 1
}
