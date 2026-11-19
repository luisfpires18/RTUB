// List paging: Pager (arrow buttons + page size, meetings / owner / naipes) and TextPager (Anterior / Seguinte, music stats).
import { useId } from 'react';
import { Icon } from '../icons';

/**
 * `block` picks the look: "mtg-pager" (small arrows) or "naipe-pager" (regular arrows). `pages` defaults to
 * ceil(total / pageSize); `ariaLabel` to "Páginas de <itemLabel>".
 */
export function Pager({
  total,
  itemLabel,
  page,
  pageSize,
  pages = Math.max(1, Math.ceil(total / pageSize)),
  sizes,
  block = 'mtg-pager',
  ariaLabel = `Páginas de ${itemLabel}`,
  onPage,
  onPageSize,
}: {
  total: number;
  itemLabel: string;
  page: number;
  pages?: number;
  pageSize: number;
  sizes: number[];
  block?: 'mtg-pager' | 'naipe-pager';
  ariaLabel?: string;
  onPage: (page: number) => void;
  onPageSize: (size: number) => void;
}) {
  const id = useId();
  const arrow = block === 'mtg-pager' ? 'icon-btn icon-btn--sm' : 'icon-btn';
  return (
    <nav className={block} aria-label={ariaLabel}>
      <span className="note">
        {total} {itemLabel}
      </span>
      <span className={`${block}__nav`}>
        <button type="button" className={arrow} onClick={() => onPage(page - 1)} disabled={page <= 1}>
          <Icon name="chevronLeft" />
          <span className="sr-only">Página anterior</span>
        </button>
        <span>
          {page} / {pages}
        </span>
        <button type="button" className={arrow} onClick={() => onPage(page + 1)} disabled={page >= pages}>
          <Icon name="chevronRight" />
          <span className="sr-only">Página seguinte</span>
        </button>
      </span>
      <label htmlFor={id} className={`${block}__size`}>
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

/** Anterior / Seguinte text buttons with fixed sizes 5-25; hidden for 5 rows or fewer. */
export function TextPager({
  page,
  pageSize,
  total,
  label,
  onPage,
  onPageSize,
}: {
  page: number;
  pageSize: number;
  total: number;
  label: string;
  onPage: (page: number) => void;
  onPageSize: (size: number) => void;
}) {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  const sizeId = useId();
  if (total <= 5) return null;
  return (
    <div className="pager">
      <button type="button" className="btn btn--ghost btn--sm" disabled={page <= 1} onClick={() => onPage(page - 1)}>
        Anterior
      </button>
      <span className="pager__status">
        {page} / {pages} · {total} {label}
      </span>
      <button type="button" className="btn btn--ghost btn--sm" disabled={page >= pages} onClick={() => onPage(page + 1)}>
        Seguinte
      </button>
      <label className="pager__size" htmlFor={sizeId}>
        Por página
        <span className="control control--select control--sm">
          <select id={sizeId} value={pageSize} onChange={(e) => onPageSize(Number(e.target.value))}>
            {[5, 10, 15, 20, 25].map((n) => (
              <option key={n} value={n}>
                {n}
              </option>
            ))}
          </select>
        </span>
      </label>
    </div>
  );
}
