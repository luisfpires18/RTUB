import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Icon } from '../components/icons';
import { normalize } from '../data/musicFormHelpers';
import { SongForm } from '../components/MusicForms';
import { MusicPlayer, type NowPlaying } from '../components/MusicPlayer';
import { Badge } from '../components/MusicUi';
import { musicApi, type AlbumDetail, type Song } from '../data/musicApi';
import { loginTo } from '../data/content';
import { MusicSongRow } from '../components/MusicAlbumSongRow';
import { LyricsDialog, LinksDialog, VideosDialog } from '../components/MusicAlbumDialogs';
import { Loading } from '../components/ui/Loading';
import { ConfirmDialog } from '../components/ui/ConfirmDialog';

type Load = { kind: 'loading' } | { kind: 'ok'; data: AlbumDetail } | { kind: 'signin' } | { kind: 'notfound' } | { kind: 'failed' };

/**
 * /music/albums/{id} - one album: its songs, the player with a server-side play
 * cooldown, lyrics (songbook PDF first, stored text otherwise), links, members-only videos, and the
 * song management the caller's role allows.
 */
export default function MusicAlbum({ albumId }: { albumId: number }) {
  const [load, setLoad] = useState<Load>({ kind: 'loading' });
  const [search, setSearch] = useState('');
  const [now, setNow] = useState<NowPlaying | null>(null);
  const [playing, setPlaying] = useState<number | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [lyricsFor, setLyricsFor] = useState<Song | null>(null);
  const [linksFor, setLinksFor] = useState<Song | null>(null);
  const [videosFor, setVideosFor] = useState<Song | null>(null);
  const [editing, setEditing] = useState<number | null | undefined>(undefined);
  const [deleting, setDeleting] = useState<Song | null>(null);
  const [deleteState, setDeleteState] = useState<{ busy: boolean; error?: string }>({ busy: false });
  const prefetched = useRef(new Map<number, string>());

  const reload = useCallback(() => {
    musicApi.album(albumId).then((r) => {
      if (r.kind === 'ok') {
        setLoad({ kind: 'ok', data: r.data });
        document.title = `${r.data.album.title} · Música · RTUB`;
      } else setLoad({ kind: r.kind === 'signin' ? 'signin' : r.kind === 'notfound' ? 'notfound' : 'failed' });
    });
  }, [albumId]);

  useEffect(() => {
    document.title = 'Música · RTUB';
    reload();
  }, [reload]);

  const data = load.kind === 'ok' ? load.data : null;
  const songs = data?.songs ?? [];
  const queue = useMemo(() => songs.filter((s) => s.hasAudio), [songs]);
  const shown = useMemo(() => {
    const q = normalize(search);
    if (!q) return songs;
    return songs.filter((s) => [s.title, s.lyricAuthor, s.musicAuthor].some((v) => v && normalize(v).includes(q)));
  }, [songs, search]);

  const setPlayCount = (songId: number, playCount: number) =>
    setLoad((l) => (l.kind === 'ok' ? { ...l, data: { ...l.data, songs: l.data.songs.map((s) => (s.id === songId ? { ...s, playCount } : s)) } } : l));

  /** Warms the neighbours' audio links so previous/next start at once (also from the lock screen). */
  const prefetchAround = (songId: number) => {
    const i = queue.findIndex((s) => s.id === songId);
    for (const s of [queue[i - 1], queue[i + 1]]) {
      if (s && !prefetched.current.has(s.id)) {
        musicApi.audio(s.id).then((r) => r.kind === 'ok' && prefetched.current.set(s.id, r.data.audioUrl));
      }
    }
  };

  const play = async (song: Song) => {
    setNotice(null);
    const ready = prefetched.current.get(song.id);
    if (ready) setNow({ songId: song.id, title: song.title, audioUrl: ready });
    else setPlaying(song.id);

    // Counting is the server's call: a repeat within the song's cooldown plays but is not counted.
    const r = await musicApi.play(song.id);
    setPlaying(null);
    if (r.kind === 'ok') {
      prefetched.current.delete(song.id);
      if (!ready) setNow({ songId: song.id, title: song.title, audioUrl: r.data.audioUrl });
      setPlayCount(song.id, r.data.playCount);
      prefetchAround(song.id);
    } else if (!ready) {
      setNotice(r.kind === 'notfound' && r.unavailable ? `O áudio de “${song.title}” não está disponível.` : 'Não foi possível reproduzir esta música.');
    }
  };

  const step = (delta: number) => {
    const i = queue.findIndex((s) => s.id === now?.songId);
    const next = queue[i + delta];
    if (next) void play(next);
  };

  const removeSong = async () => {
    if (!deleting) return;
    setDeleteState({ busy: true });
    const r = await musicApi.deleteSong(deleting.id);
    if (r.kind === 'ok') {
      if (now?.songId === deleting.id) setNow(null);
      setDeleting(null);
      setDeleteState({ busy: false });
      reload();
    } else setDeleteState({ busy: false, error: 'Não foi possível eliminar a música.' });
  };

  if (load.kind === 'loading') {
    return (
      <div className="wrap">
        <Loading label="A carregar o álbum…" />
      </div>
    );
  }

  if (!data) {
    return (
      <section className="page wrap" aria-labelledby="album-title">
        <BackLink />
        <div className="state">
          <Icon name={load.kind === 'signin' ? 'lock' : 'disc'} className="state__icon" />
          <h1 id="album-title" className="state__title">
            {load.kind === 'signin' ? 'Álbum reservado a membros' : load.kind === 'notfound' ? 'Álbum não encontrado' : 'Não foi possível carregar o álbum.'}
          </h1>
          {load.kind === 'signin' ? (
            <a className="btn btn--primary" href={loginTo(`/music/albums/${albumId}`)}>
              <Icon name="login" />
              Entrar como membro
            </a>
          ) : load.kind === 'failed' ? (
            <button type="button" className="btn btn--primary" onClick={reload}>
              Tentar novamente
            </button>
          ) : null}
        </div>
      </section>
    );
  }

  const { album, permissions: can } = data;
  const index = queue.findIndex((s) => s.id === now?.songId);

  return (
    <section className={`page wrap music-album${now ? ' music-album--playing' : ''}`} aria-labelledby="album-title">
      <BackLink />
      <header className="album-head">
        {album.coverUrl ? (
          <img className="album-head__cover" src={album.coverUrl} alt={`Capa de ${album.title}`} width="240" height="240" />
        ) : (
          <span className="album-head__cover album-card__cover--empty" aria-hidden="true">
            <Icon name="disc" />
          </span>
        )}
        <div className="album-head__text">
          <p className="eyebrow">{album.year ?? 'Álbum'}</p>
          <h1 id="album-title" className="page__title">
            {album.title}
          </h1>
          {album.description && <p className="page__lead">{album.description}</p>}
          <p className="album-head__meta">
            {songs.length} {songs.length === 1 ? 'música' : 'músicas'}
            {album.isPrivate && <Badge icon="lock">Privado</Badge>}
            {album.isExclusive && <Badge icon="star">Exclusivo</Badge>}
          </p>
          {can.canManage && (
            <button type="button" className="btn btn--primary" onClick={() => setEditing(null)}>
              <Icon name="plus" />
              Adicionar música
            </button>
          )}
        </div>
      </header>

      <div className="album-tools">
        <h2 className="section__title album-tools__title">Faixas</h2>
        <label className="control album-tools__search">
          <Icon name="search" />
          <span className="sr-only">Pesquisar faixas</span>
          <input type="search" placeholder="Título, autor da letra ou da música…" value={search} onChange={(e) => setSearch(e.target.value)} />
        </label>
      </div>

      {notice && (
        <p className="form__banner album-notice" role="status">
          {notice}
        </p>
      )}

      {songs.length === 0 ? (
        <div className="state">
          <Icon name="music" className="state__icon" />
          <p className="state__title">Ainda não há músicas neste álbum.</p>
        </div>
      ) : shown.length === 0 ? (
        <p className="note">Nenhuma faixa corresponde à pesquisa.</p>
      ) : (
        <ol className="songs">
          {shown.map((s) => (
            <MusicSongRow
              key={s.id}
              s={s}
              now={now}
              playing={playing}
              can={can}
              play={play}
              setLyricsFor={setLyricsFor}
              setLinksFor={setLinksFor}
              setVideosFor={setVideosFor}
              setEditing={setEditing}
              setDeleting={setDeleting}
            />
          ))}
        </ol>
      )}

      {now && (
        <MusicPlayer
          now={now}
          album={album.title}
          cover={album.coverUrl}
          hasPrevious={index > 0}
          hasNext={index >= 0 && index < queue.length - 1}
          onPrevious={() => step(-1)}
          onNext={() => step(1)}
          onClose={() => setNow(null)}
        />
      )}

      {lyricsFor && <LyricsDialog song={lyricsFor} onClose={() => setLyricsFor(null)} />}
      {linksFor && <LinksDialog song={linksFor} onClose={() => setLinksFor(null)} />}
      {videosFor && (
        <VideosDialog
          song={videosFor}
          onClose={() => setVideosFor(null)}
          onCount={(n) =>
            setLoad((l) => (l.kind === 'ok' ? { ...l, data: { ...l.data, songs: l.data.songs.map((x) => (x.id === videosFor.id ? { ...x, videoCount: n } : x)) } } : l))
          }
        />
      )}
      {editing !== undefined && (
        <SongForm
          albumId={album.id}
          songId={editing}
          canPlayAlong={can.canManageExclusive}
          onClose={() => setEditing(undefined)}
          onSaved={() => {
            setEditing(undefined);
            reload();
          }}
        />
      )}
      {deleting && (
        <ConfirmDialog
          title="Eliminar música"
          confirmLabel="Eliminar"
          busy={deleteState.busy}
          error={deleteState.error}
          onConfirm={removeSong}
          onClose={() => {
            setDeleting(null);
            setDeleteState({ busy: false });
          }}
        >
          <p>
            Eliminar a música <strong>{deleting.title}</strong>? Esta ação não pode ser revertida.
          </p>
        </ConfirmDialog>
      )}
    </section>
  );
}

function BackLink() {
  return (
    <a className="back-link" href="/music">
      <Icon name="arrow" />
      Música
    </a>
  );
}
