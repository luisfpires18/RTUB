// "Mostrar mais (n)": shows `n` more rows of a list already in memory; renders nothing when none are left.

/** `remaining` = rows still hidden. `className` is the page's spacing class: "members-more" (default) or "tr-more". */
export function LoadMore({ remaining, onMore, className = 'members-more' }: { remaining: number; onMore: () => void; className?: string }) {
  if (remaining <= 0) return null;
  return (
    <button type="button" className={`btn btn--ghost btn--sm ${className}`} onClick={onMore}>
      Mostrar mais ({remaining})
    </button>
  );
}
