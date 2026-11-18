// One song row of the album list: title, credits, play count and the action buttons.
import { Spinner } from './ui/Spinner';
import { Icon } from './icons';
import { type NowPlaying } from './MusicPlayer';
import { IconButton, plays } from './MusicUi';
import { type AlbumDetail, type Song } from '../data/musicApi';

export function MusicSongRow({
  s,
  now,
  playing,
  can,
  play,
  setLyricsFor,
  setLinksFor,
  setVideosFor,
  setEditing,
  setDeleting,
}: {
  s: Song;
  now: NowPlaying | null;
  playing: number | null;
  can: AlbumDetail['permissions'];
  play: (song: Song) => void;
  setLyricsFor: (song: Song) => void;
  setLinksFor: (song: Song) => void;
  setVideosFor: (song: Song) => void;
  setEditing: (songId: number) => void;
  setDeleting: (song: Song) => void;
}) {
  return (
    <li key={s.id} className={`song${now?.songId === s.id ? ' song--now' : ''}`}>
      <span className="song__track" aria-hidden={s.trackNumber === null}>
        {s.trackNumber ?? '·'}
      </span>
      <div className="song__main">
        <h3 className="song__title">
          {s.trackNumber !== null && <span className="sr-only">Faixa {s.trackNumber}: </span>}
          {s.title}
        </h3>
        <p className="song__credits">
          {s.lyricAuthor && (
            <span>
              <span className="song__label">Letra</span> {s.lyricAuthor}
            </span>
          )}
          {s.musicAuthor && (
            <span>
              <span className="song__label">Música</span> {s.musicAuthor}
            </span>
          )}
          {s.adaptation && (
            <span>
              <span className="song__label">Adaptação</span> {s.adaptation}
            </span>
          )}
          {!s.lyricAuthor && !s.musicAuthor && <span className="song__unknown">Autor desconhecido</span>}
        </p>
      </div>
      <span className="song__plays" title={plays(s.playCount)}>
        {s.playCount > 0 && (
          <>
            <Icon name="playFill" />
            {s.playCount}
            <span className="sr-only"> {s.playCount === 1 ? 'reprodução' : 'reproduções'}</span>
          </>
        )}
      </span>
      <div className="song__actions">
        <button
          type="button"
          className="song__btn song__btn--play"
          onClick={() => play(s)}
          disabled={!s.hasAudio || playing === s.id}
          title={s.hasAudio ? undefined : 'Áudio não disponível'}
        >
          {playing === s.id ? <Spinner small /> : <Icon name="playFill" />}
          Ouvir<span className="sr-only"> {s.title}</span>
        </button>
        <button type="button" className="song__btn" onClick={() => setLyricsFor(s)}>
          <Icon name="lyrics" />
          Letra<span className="sr-only"> de {s.title}</span>
        </button>
        <button
          type="button"
          className={`song__btn song__btn--count${s.links.length === 0 ? ' song__btn--quiet' : ''}`}
          onClick={() => setLinksFor(s)}
          disabled={s.links.length === 0}
        >
          <Icon name="link" />
          {s.links.length}
          <span className="sr-only"> links de {s.title}</span>
        </button>
        {s.hasPlayAlong && (
          <a className="song__btn" href={`/music/play/${s.id}`}>
            <Icon name="music" />
            Aprender<span className="sr-only"> {s.title}</span>
          </a>
        )}
        {s.videoCount !== null && (
          <button
            type="button"
            className={`song__btn song__btn--count${s.videoCount === 0 ? ' song__btn--quiet' : ''}`}
            onClick={() => setVideosFor(s)}
          >
            <Icon name="video" />
            {s.videoCount}
            <span className="sr-only"> vídeos de {s.title}</span>
          </button>
        )}
        {can.canManage && <IconButton icon="pencil" label={`Editar ${s.title}`} onClick={() => setEditing(s.id)} />}
        {can.canDelete && <IconButton icon="trash" tone="danger" label={`Eliminar ${s.title}`} onClick={() => setDeleting(s)} />}
      </div>
    </li>
  );
}
