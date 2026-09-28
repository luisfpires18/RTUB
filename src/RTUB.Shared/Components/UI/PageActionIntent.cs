namespace RTUB.Shared;

/// <summary>Visual weight of a <see cref="PageAction"/>.</summary>
public enum PageActionIntent
{
    /// <summary>Routine action: quiet outline button.</summary>
    Secondary,

    /// <summary>The page's main action: filled RTUB purple. At most one per page.</summary>
    Primary,

    /// <summary>Destructive action: red outline, placed last.</summary>
    Danger
}

/// <summary>Where a <see cref="PageAction"/> is rendered (set by <see cref="PageActions"/>).</summary>
public enum PageActionPlacement
{
    Header,
    Bar
}
