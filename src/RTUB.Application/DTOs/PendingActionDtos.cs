namespace RTUB.Application.DTOs;

// "As minhas ações pendentes" (task 071, docs/react-pending-actions.md). Compact DTOs: a panel page carries at most
// PendingActionsService.PageSize rows and the counts; nothing else of the source module travels.

/// <summary>
/// One button of a row. <c>Href</c> opens the module's own page, where the action is done with that module's rules (the
/// only kind delivered in 071). Later phases add in-page answers (#253) as other kinds.
/// </summary>
public sealed record PendingActionButtonDto(string Label, string Href, bool Primary);

/// <summary>
/// One action. <c>Kind</c> + <c>SourceKey</c> identify it (for a dismissal). <c>Required</c>: shows "Obrigatório" and
/// can never be dismissed. <c>Dismissible</c>: an optional prompt the member may turn down for its period. Both false: a
/// response the member gives in the module (an answer is not a dismissal). Decided by the server, never by the page.
/// </summary>
public sealed record PendingActionRowDto(
    string Kind,
    string SourceKey,
    string Title,
    string? Date,
    string? Time,
    string? Context,
    bool Required,
    bool Dismissible,
    IReadOnlyList<PendingActionButtonDto> Actions);

/// <summary>A sub-filter of a panel (e.g. Próximas / Passadas) with its own count.</summary>
public sealed record PendingActionFilterDto(string Key, string Label, int Count);

/// <summary>
/// One page of one panel. <c>Page</c> is 1-based and already clamped to the last page (an emptied last page answers the
/// one before it). <c>Total</c> counts the selected filter; <c>Category</c> groups panels (participation, tasks,
/// responsibilities).
/// </summary>
public sealed record PendingActionPanelDto(
    string Key,
    string Title,
    string Category,
    IReadOnlyList<PendingActionFilterDto> Filters,
    string? Filter,
    int Page,
    int PageSize,
    int Total,
    IReadOnlyList<PendingActionRowDto> Rows);

/// <summary>
/// The page: Portugal's date, the current fiscal year, the member's activity status ("active", "retired", or "none"
/// for an account with no membership category) and the first page of every panel that applies to the member.
/// </summary>
public sealed record PendingActionsPageDto(string Today, string FiscalYear, string Status, IReadOnlyList<PendingActionPanelDto> Panels);

/// <summary>"Não" / "Agora não" on an optional action: its kind and source as the row gave them.</summary>
public sealed record PendingActionDismissInput(string? Kind, string? SourceKey);
