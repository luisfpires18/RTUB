namespace RTUB.Core.Entities;

/// <summary>
/// "As minhas ações pendentes" (task 071, docs/react-pending-actions.md): a member's own "no / not now" on an optional
/// action, so it stops showing for that member and that period. Personal only: it never hides work for anyone else and
/// it is never a shared decision (those live in their module, e.g. a treasury "Sem movimentos"). Required actions are
/// never stored here; the service refuses them. Keyed by member + action kind + source + period, where the period is
/// the action's own reset window (a fiscal year "2026-2027", an ISO week "2026-W45", or "" for "once"). A new period is
/// a new key, so the prompt comes back by itself. Not audited (personal, low value), like <see cref="LoginEvent"/>.
/// </summary>
public class PendingActionDismissal
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    /// <summary>The action kind, e.g. "news.prompt" (a stable code, never shown).</summary>
    public string ActionKind { get; set; } = string.Empty;

    /// <summary>What the action is about in its module, e.g. an event id; "" when the action has no single source.</summary>
    public string SourceKey { get; set; } = string.Empty;

    public string Period { get; set; } = string.Empty;

    /// <summary>UTC.</summary>
    public DateTime DismissedAt { get; set; }
}
