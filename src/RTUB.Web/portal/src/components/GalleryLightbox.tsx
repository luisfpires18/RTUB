// Full-size photo viewer (native modal dialog) with previous/next.
import { useEffect, useRef, useState, type KeyboardEvent } from 'react';
import { type GalleryItem } from '../data/api';
import { Icon } from './icons';
import { dateLabel } from '../data/galleryTimeline';

/** A native modal <dialog>: focus containment, Esc and an inert page come from the platform. */
export function Lightbox({
  item,
  siblings,
  onMove,
  onClose,
  onEdit,
}: {
  item: GalleryItem;
  siblings: GalleryItem[];
  onMove: (item: GalleryItem) => void;
  onClose: () => void;
  /** The uploader, Admin or Owner. */
  onEdit?: () => void;
}) {
  const dialog = useRef<HTMLDialogElement>(null);
  const [broken, setBroken] = useState(false);
  const index = siblings.findIndex((s) => s.id === item.id);
  const previous = index > 0 ? siblings[index - 1] : undefined;
  const next = index >= 0 && index < siblings.length - 1 ? siblings[index + 1] : undefined;

  useEffect(() => {
    const d = dialog.current;
    if (d && !d.open) d.showModal();
  }, []);

  useEffect(() => setBroken(false), [item.id]);

  const onKey = (e: KeyboardEvent) => {
    if (e.key === 'ArrowLeft' && previous) onMove(previous);
    if (e.key === 'ArrowRight' && next) onMove(next);
  };

  return (
    <dialog
      ref={dialog}
      className="lightbox"
      aria-labelledby="lightbox-title"
      onClose={onClose}
      onCancel={(e) => {
        // Esc: close through React rather than waiting for the native close event.
        e.preventDefault();
        onClose();
      }}
      onKeyDown={onKey}
      onClick={(e) => e.target === e.currentTarget && onClose()}
    >
      <div className="lightbox__stage">
        {!item.url || broken ? (
          <p className="tile__missing">
            <Icon name="images" />
            Imagem indisponível
          </p>
        ) : item.type === 'video' ? (
          <video key={item.id} className="lightbox__media" src={item.url} controls playsInline preload="metadata" onError={() => setBroken(true)} />
        ) : (
          <img key={item.id} className="lightbox__media" src={item.url} alt={item.title} onError={() => setBroken(true)} />
        )}
      </div>

      <div className="lightbox__info">
        <div className="lightbox__text">
          <h2 id="lightbox-title" className="lightbox__title">
            {item.title}
          </h2>
          <p className="lightbox__date">
            {dateLabel(item)}
            {item.membersOnly && (
              <span className="tile__badge tile__badge--inline">
                <Icon name="lock" />
                Membros
              </span>
            )}
          </p>
          {item.people.length > 0 && (
            <ul className="chips" aria-label="Quem aparece">
              {item.people.map((p) => (
                <li key={p.id} className="chip">
                  {p.name}
                </li>
              ))}
            </ul>
          )}
        </div>
        <div className="lightbox__actions">
          {onEdit && (
            <button type="button" className="btn btn--ghost btn--sm" onClick={onEdit}>
              <Icon name="pencil" />
              Editar
            </button>
          )}
          {item.url && (
            <a className="btn btn--ghost btn--sm" href={item.url} target="_blank" rel="noopener noreferrer">
              <Icon name="external" />
              Abrir original
            </a>
          )}
          {index >= 0 && siblings.length > 1 && (
            <span className="lightbox__count">
              {index + 1} / {siblings.length}
            </span>
          )}
        </div>
      </div>

      <button type="button" className="icon-btn lightbox__close" onClick={onClose}>
        <Icon name="close" />
        <span className="sr-only">Fechar</span>
      </button>
      {previous && (
        <button type="button" className="icon-btn lightbox__nav lightbox__nav--prev" onClick={() => onMove(previous)}>
          <Icon name="prev" />
          <span className="sr-only">Anterior</span>
        </button>
      )}
      {next && (
        <button type="button" className="icon-btn lightbox__nav lightbox__nav--next" onClick={() => onMove(next)}>
          <Icon name="next" />
          <span className="sr-only">Seguinte</span>
        </button>
      )}
    </dialog>
  );
}
