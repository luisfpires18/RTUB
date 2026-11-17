// The page-size pager shared by the /meetings and /meetings/polls grids.
import { useId } from 'react';
import { Icon } from './icons';

export const MEETING_PAGE_SIZES = [6, 12, 18, 24];

export function Pager({
  total,
  itemLabel,
  page,
  pages,
  pageSize,
  sizes,
  onPage,
  onPageSize,
}: {
  total: number;
  itemLabel: string;
  page: number;
  pages: number;
  pageSize: number;
  sizes: number[];
  onPage: (page: number) => void;
  onPageSize: (size: number) => void;
}) {
  const id = useId();
  return (
    <nav className="mtg-pager" aria-label={`Páginas de ${itemLabel}`}>
      <span className="note">
        {total} {itemLabel}
      </span>
      <span className="mtg-pager__nav">
        <button type="button" className="icon-btn icon-btn--sm" onClick={() => onPage(page - 1)} disabled={page <= 1}>
          <Icon name="chevronLeft" />
          <span className="sr-only">Página anterior</span>
        </button>
        <span>
          {page} / {pages}
        </span>
        <button type="button" className="icon-btn icon-btn--sm" onClick={() => onPage(page + 1)} disabled={page >= pages}>
          <Icon name="chevronRight" />
          <span className="sr-only">Página seguinte</span>
        </button>
      </span>
      <label htmlFor={id} className="mtg-pager__size">
        Por página
        <span className="control control--select control--sm">
          <select id={id} value={pageSize} onChange={(e) => onPageSize(Number(e.target.value))}>
            {sizes.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </select>
        </span>
      </label>
    </nav>
  );
}
