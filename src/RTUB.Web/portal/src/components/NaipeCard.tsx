// Naipes item card and pager.
import { useId } from 'react';
import { Icon } from './icons';
import { type NaipeItem } from '../data/naipesApi';
import { PAGE_SIZES } from '../data/naipesShared';

export function NaipeCard({ item, onOpen, onEdit, onDelete }: { item: NaipeItem; onOpen: () => void; onEdit: () => void; onDelete: () => void }) {
  return (
    <article className="naipe-card">
      <button type="button" className="naipe-card__open" onClick={onOpen}>
        <span className="naipe-card__media">
          {item.isVideo ? (
            <video src={item.url} muted preload="metadata" playsInline aria-hidden="true" tabIndex={-1} />
          ) : (
            <img src={item.url} alt="" loading="lazy" />
          )}
          <span className="naipe-card__kind">
            <Icon name={item.isVideo ? 'video' : 'images'} />
            {item.isVideo ? 'Vídeo' : 'Imagem'}
          </span>
        </span>
        <span className="naipe-card__body">
          <strong className="naipe-card__title">{item.title}</strong>
          {item.description && <span className="naipe-card__text">{item.description}</span>}
          <span className="naipe-card__meta">
            {item.isVideo && (
              <span>
                <Icon name="play" /> {item.playCount}
              </span>
            )}
            <span>
              <Icon name="chat" /> {item.commentCount}
            </span>
            <span className="naipe-card__by">{item.createdBy}</span>
          </span>
        </span>
      </button>
      {item.canEdit && (
        <span className="naipe-card__tools">
          <button type="button" className="icon-btn" onClick={onEdit}>
            <Icon name="pencil" />
            <span className="sr-only">Editar {item.title}</span>
          </button>
          <button type="button" className="icon-btn icon-btn--danger" onClick={onDelete}>
            <Icon name="trash" />
            <span className="sr-only">Eliminar {item.title}</span>
          </button>
        </span>
      )}
    </article>
  );
}

export function Pager({
  total,
  page,
  pages,
  pageSize,
  onPage,
  onPageSize,
}: {
  total: number;
  page: number;
  pages: number;
  pageSize: number;
  onPage: (page: number) => void;
  onPageSize: (size: number) => void;
}) {
  const id = useId();
  return (
    <nav className="naipe-pager" aria-label="Páginas">
      <span className="note">
        {total} {total === 1 ? 'conteúdo' : 'conteúdos'}
      </span>
      <span className="naipe-pager__nav">
        <button type="button" className="icon-btn" onClick={() => onPage(page - 1)} disabled={page <= 1}>
          <Icon name="chevronLeft" />
          <span className="sr-only">Página anterior</span>
        </button>
        <span>
          {page} / {pages}
        </span>
        <button type="button" className="icon-btn" onClick={() => onPage(page + 1)} disabled={page >= pages}>
          <Icon name="chevronRight" />
          <span className="sr-only">Página seguinte</span>
        </button>
      </span>
      <label htmlFor={id} className="naipe-pager__size">
        Por página
        <span className="control control--select control--sm">
          <select id={id} value={pageSize} onChange={(e) => onPageSize(Number(e.target.value))}>
            {PAGE_SIZES.map((s) => (
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
