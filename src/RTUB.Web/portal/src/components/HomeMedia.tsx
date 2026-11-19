// Home page music and gallery preview sections.
import { useEffect, useState } from 'react';
import { getGalleryPreview, type GalleryItem } from '../data/api';
import { ExternalLink } from './AppShared';
import { albums, portal, social } from '../data/content';
import { Icon } from './icons';
import { SectionHead, MoreLink } from './HomeParts';

// ---------- music ----------

export function Music() {
  const listen = social.filter((s) => s.name === 'Spotify' || s.name === 'YouTube');
  const latest = albums[albums.length - 1];

  return (
    <section id="music" className="section" aria-labelledby="music-title">
      <div className="wrap split">
        <div>
          <SectionHead id="music-title" eyebrow="Discografia" title={`${albums.length} álbuns para ouvir e recordar`}>
            Temas tradicionais e originais, gravados entre 1995 e 2013 por sucessivas gerações de tunos.
          </SectionHead>
          <ol className="discs">
            {albums.map((a) => (
              <li key={a.title} className="disc">
                <span className="disc__year">{a.year}</span>
                <span className="disc__title">{a.title}</span>
                <span className="disc__tracks">{a.tracks} temas</span>
              </li>
            ))}
          </ol>
          <div className="stream">
            {listen.map((s, i) => (
              <ExternalLink key={s.name} href={s.href} className={`btn ${i === 0 ? 'btn--primary' : 'btn--ghost'}`}>
                <Icon name={s.icon} />
                Ouvir no {s.name}
              </ExternalLink>
            ))}
          </div>
          <MoreLink href={portal.music}>Ouvir os álbuns e ler as letras</MoreLink>
        </div>
        <div className="record-wrap" aria-hidden="true">
          <div className="sleeve">
            <span className="sleeve__title">{latest.title}</span>
            <span className="sleeve__sub">
              RTUB · {latest.year}
            </span>
          </div>
          <div className="record">
            <img src="/icons/rtub-logo-352.webp" alt="" width="192" height="192" />
          </div>
        </div>
      </div>
    </section>
  );
}

// ---------- gallery ----------

const PREVIEW_PHOTOS = 5;

/** The latest public photos (GET /api/gallery?public=true): members-only ones never show here. */
export function Gallery() {
  const [photos, setPhotos] = useState<GalleryItem[] | null>();

  useEffect(() => {
    getGalleryPreview(PREVIEW_PHOTOS).then(
      (items) => setPhotos(items.filter((i) => i.type === 'image' && !i.membersOnly)),
      () => setPhotos(null),
    );
  }, []);

  return (
    <section id="gallery" className="section section--raise" aria-labelledby="gallery-title">
      <div className="wrap">
        <SectionHead id="gallery-title" eyebrow="Galeria" title="Em palco e fora dele" />
        {photos === undefined ? (
          <ul className="photos" aria-hidden="true">
            {Array.from({ length: PREVIEW_PHOTOS }, (_, i) => (
              <li key={i}>
                <span className="photo photo--skeleton" />
              </li>
            ))}
          </ul>
        ) : photos?.length ? (
          <ul className="photos">
            {photos.map((p) => (
              <li key={p.id}>
                <PreviewPhoto photo={p} />
              </li>
            ))}
          </ul>
        ) : (
          <p className="note">As fotografias estão na Galeria.</p>
        )}
        <MoreLink href={portal.gallery}>Abrir a galeria</MoreLink>
      </div>
    </section>
  );
}

/** Opens the photo in the Galeria; a missing or broken file becomes a quiet placeholder. */
function PreviewPhoto({ photo }: { photo: GalleryItem }) {
  const [broken, setBroken] = useState(!photo.url);
  return (
    <a className="photo" href={`${portal.gallery}?item=${photo.id}`}>
      {broken ? (
        <span className="photo__missing">
          <Icon name="images" />
        </span>
      ) : (
        <img src={photo.url!} alt="" loading="lazy" decoding="async" onError={() => setBroken(true)} />
      )}
      <span className="photo__caption">{photo.title}</span>
    </a>
  );
}
