// Anterior / "p / n" / Seguinte for a list cut into pages; renders nothing for a single page.

/** `words`: the status reads "Página p de n" (inside dialogs) instead of "p / n". */
export function SimplePager({ page, pages, onPage, words }: { page: number; pages: number; onPage: (page: number) => void; words?: boolean }) {
  if (pages <= 1) return null;
  return (
    <div className="pager">
      <button type="button" className="btn btn--ghost btn--sm" disabled={page <= 1} onClick={() => onPage(page - 1)}>
        Anterior
      </button>
      {words ? (
        <span>
          Página {page} de {pages}
        </span>
      ) : (
        <span className="pager__status">
          {page} / {pages}
        </span>
      )}
      <button type="button" className="btn btn--ghost btn--sm" disabled={page >= pages} onClick={() => onPage(page + 1)}>
        Seguinte
      </button>
    </div>
  );
}
