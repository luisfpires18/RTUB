// One photo tile in the gallery grid.
import { useState } from 'react';
import { type GalleryItem } from '../data/api';
import { Icon } from './icons';
import { dateLabel } from '../data/galleryTimeline';

/** Natural aspect ratio, never cropped; a broken or missing file becomes a quiet placeholder. */
export function Tile({ item, onOpen }: { item: GalleryItem; onOpen: () => void }) {
  const [broken, setBroken] = useState(!item.url);

  return (
    <button type="button" className="tile" onClick={onOpen} aria-label={`${item.title}, ${dateLabel(item)}`}>
      <span className="tile__frame">
        {broken ? (
          <span className="tile__missing">
            <Icon name="images" />
            Imagem indisponível
          </span>
        ) : item.type === 'video' ? (
          <>
            {/* #t=0.1 asks for the first frame: there are no stored thumbnails. */}
            <video
              className="tile__media"
              src={`${item.url}#t=0.1`}
              preload="metadata"
              muted
              playsInline
              onError={() => setBroken(true)}
            />
            <span className="tile__play" aria-hidden="true">
              <Icon name="play" />
            </span>
          </>
        ) : (
          <img className="tile__media" src={item.url!} alt="" loading="lazy" decoding="async" onError={() => setBroken(true)} />
        )}
        {item.membersOnly && (
          <span className="tile__badge">
            <Icon name="lock" />
            Membros
          </span>
        )}
      </span>
      <span className="tile__caption">
        <span className="tile__title">{item.title}</span>
        <span className="tile__date">{dateLabel(item)}</span>
      </span>
    </button>
  );
}
