// The event page's video list and player.

import { useRef, useState } from 'react';
import { eventsApi, type EventVideo } from '../data/eventsApi';
import { Icon } from './icons';

/** A list of videos and one player; each first play is recorded. */
export function Videos({ videos, onManage }: { videos: EventVideo[]; onManage?: () => void }) {
  const [picked, setCurrent] = useState<number>();
  const played = useRef(new Set<number>());
  // The list can change under it (a video added, renamed or deleted): fall back to the first.
  const current = videos.find((v) => v.id === picked) ?? videos[0];

  return (
    <section className="event-section" aria-labelledby="videos-title">
      <div className="event-section__head">
        <h2 id="videos-title" className="event-section__title">
          Vídeos
        </h2>
        {onManage && (
          <button type="button" className="btn btn--ghost btn--sm" onClick={onManage}>
            <Icon name="video" />
            Gerir vídeos
          </button>
        )}
      </div>
      {!current ? (
        <p className="note">Ainda não há vídeos desta atuação.</p>
      ) : (
      <div className="event-video">
        <p className="event-video__title">{current.title}</p>
        <video
          key={current.id}
          className="event-video__player"
          controls
          playsInline
          preload="metadata"
          onPlay={() => {
            if (played.current.has(current.id)) return;
            played.current.add(current.id);
            void eventsApi.videoPlayed(current.id);
          }}
        >
          <source src={current.url} type={current.mimeType} />
          {current.mimeType === 'video/quicktime' && <source src={current.url} type="video/mp4" />}
        </video>
        <p className="note">Se o vídeo não abrir, experimente outro navegador: alguns telemóveis gravam num formato pouco suportado.</p>
      </div>
      )}
      {videos.length > 1 && current && (
        <ul className="event-video__list">
          {videos.map((v) => (
            <li key={v.id}>
              <button
                type="button"
                className={v.id === current.id ? 'video-pick video-pick--on' : 'video-pick'}
                aria-pressed={v.id === current.id}
                onClick={() => setCurrent(v.id)}
              >
                <Icon name="play" />
                {v.title}
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
