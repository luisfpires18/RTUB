import { useEffect, useRef } from 'react';
import { Icon } from './icons';

export type NowPlaying = { songId: number; title: string; audioUrl: string };

const LOGO = '/icons/rtub-logo-512.png';

/**
 * The album page's fixed audio player. It also owns the lock-screen / system media card
 * (Media Session): title, album and cover, play/pause, previous/next within the album and seeking.
 * No seek-forward/back handlers: iOS would show those instead of previous/next. When the player
 * closes or the page goes away, the session's handlers and metadata are released, so no card
 * outlives the player it belongs to.
 */
export function MusicPlayer({
  now,
  album,
  cover,
  hasPrevious,
  hasNext,
  onPrevious,
  onNext,
  onClose,
}: {
  now: NowPlaying;
  album: string;
  cover: string | null;
  hasPrevious: boolean;
  hasNext: boolean;
  onPrevious: () => void;
  onNext: () => void;
  onClose: () => void;
}) {
  const audio = useRef<HTMLAudioElement>(null);
  const handlers = useRef({ onPrevious, onNext, hasPrevious, hasNext });
  handlers.current = { onPrevious, onNext, hasPrevious, hasNext };

  // A new song: new source, start playing (the browser may refuse without a gesture; the
  // controls are there to start it by hand).
  useEffect(() => {
    const el = audio.current;
    if (!el) return;
    el.src = now.audioUrl;
    el.play().catch(() => undefined);

    if ('mediaSession' in navigator) {
      const artwork = cover ?? LOGO;
      navigator.mediaSession.metadata = new MediaMetadata({
        title: now.title,
        artist: 'RTUB',
        album,
        artwork: [{ src: artwork, sizes: '512x512' }],
      });
    }
  }, [now, album, cover]);

  // The session's action handlers, bound once for this player's <audio> and released with it.
  useEffect(() => {
    const el = audio.current;
    if (!el || !('mediaSession' in navigator)) return;
    const session = navigator.mediaSession;
    const set = (action: MediaSessionAction, handler: MediaSessionActionHandler | null) => {
      try {
        session.setActionHandler(action, handler);
      } catch {
        // Not every browser supports every action.
      }
    };

    set('play', () => void el.play().catch(() => undefined));
    set('pause', () => el.pause());
    set('previoustrack', () => {
      if (el.currentTime > 3 || !handlers.current.hasPrevious) el.currentTime = 0;
      else handlers.current.onPrevious();
    });
    set('nexttrack', () => {
      if (handlers.current.hasNext) handlers.current.onNext();
    });
    set('seekto', (d) => {
      if (d.seekTime !== undefined) el.currentTime = d.seekTime;
    });

    const position = () => {
      if (!Number.isFinite(el.duration)) return;
      try {
        session.setPositionState({ duration: el.duration, position: Math.min(el.currentTime, el.duration), playbackRate: el.playbackRate });
      } catch {
        // setPositionState is optional.
      }
    };
    const state = () => {
      session.playbackState = el.paused ? 'paused' : 'playing';
      position();
    };
    el.addEventListener('play', state);
    el.addEventListener('pause', state);
    el.addEventListener('loadedmetadata', position);

    return () => {
      el.removeEventListener('play', state);
      el.removeEventListener('pause', state);
      el.removeEventListener('loadedmetadata', position);
      for (const action of ['play', 'pause', 'previoustrack', 'nexttrack', 'seekto'] as MediaSessionAction[]) set(action, null);
      session.metadata = null;
      session.playbackState = 'none';
      el.pause();
      el.removeAttribute('src');
      el.load();
    };
  }, []);

  return (
    <div className="player" role="region" aria-label="Leitor de música">
      <div className="player__inner wrap">
        <div className="player__now">
          <Icon name="music" />
          <span className="player__title">{now.title}</span>
          <span className="player__album">{album}</span>
        </div>
        <div className="player__controls">
          <button type="button" className="icon-btn icon-btn--sm" onClick={onPrevious} disabled={!hasPrevious} title="Anterior">
            <Icon name="prev" />
            <span className="sr-only">Música anterior</span>
          </button>
          <audio
            ref={audio}
            className="player__audio"
            controls
            preload="auto"
            onEnded={() => handlers.current.hasNext && handlers.current.onNext()}
          >
            O seu navegador não reproduz áudio.
          </audio>
          <button type="button" className="icon-btn icon-btn--sm" onClick={onNext} disabled={!hasNext} title="Seguinte">
            <Icon name="next" />
            <span className="sr-only">Música seguinte</span>
          </button>
          <button type="button" className="icon-btn icon-btn--sm" onClick={onClose} title="Fechar">
            <Icon name="close" />
            <span className="sr-only">Fechar o leitor</span>
          </button>
        </div>
      </div>
    </div>
  );
}
