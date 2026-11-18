// Naipes item card.
import { Icon } from './icons';
import { type NaipeItem } from '../data/naipesApi';

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
