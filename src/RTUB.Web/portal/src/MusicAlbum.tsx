import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ExternalLink, Loading } from './App';
import { Icon } from './icons';
import { normalize, SongForm } from './MusicForms';
import { MusicPlayer, type NowPlaying } from './MusicPlayer';
import { Badge, ConfirmDialog, Dialog, IconButton, plays } from './MusicUi';
import { loginTo, MAX_VIDEO_BYTES, musicApi, type AlbumDetail, type Lyrics, type MusicLink, type Song, type Video } from './musicApi';

type Load = { kind: 'loading' } | { kind: 'ok'; data: AlbumDetail } | { kind: 'signin' } | { kind: 'notfound' } | { kind: 'failed' };

/**
 * /music/albums/{id} - one album (React track 006): its songs, the player with a server-side play
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
        <label className="search">
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
                  {playing === s.id ? <span className="spinner spinner--small" aria-hidden="true" /> : <Icon name="playFill" />}
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
    <a className="doc__back" href="/music">
      Música
    </a>
  );
}

// ---------- lyrics ----------

function LyricsDialog({ song, onClose }: { song: Song; onClose: () => void }) {
  const [lyrics, setLyrics] = useState<Lyrics | null>(null);
  const [failed, setFailed] = useState(false);
  const [view, setView] = useState<'pdf' | 'text'>('pdf');

  useEffect(() => {
    musicApi.lyrics(song.id).then((r) => {
      if (r.kind !== 'ok') return setFailed(true);
      setLyrics(r.data);
      setView(r.data.pdfUrl ? 'pdf' : 'text');
    });
  }, [song.id]);

  return (
    <Dialog title={`Letra · ${song.title}`} size="lg" onClose={onClose}>
      {failed ? (
        <p className="form__banner" role="alert">
          Não foi possível carregar a letra.
        </p>
      ) : !lyrics ? (
        <Loading label="A procurar a letra…" />
      ) : !lyrics.pdfUrl && !lyrics.text ? (
        <div className="state">
          <Icon name="lyrics" className="state__icon" />
          <p className="state__title">Ainda não há letra para esta música.</p>
        </div>
      ) : (
        <div className="lyrics">
          {lyrics.pdfUrl && lyrics.text && (
            <div className="segmented" role="group" aria-label="Formato da letra">
              <button type="button" aria-pressed={view === 'pdf'} onClick={() => setView('pdf')}>
                Cancioneiro (PDF)
              </button>
              <button type="button" aria-pressed={view === 'text'} onClick={() => setView('text')}>
                Texto
              </button>
            </div>
          )}
          {view === 'pdf' && lyrics.pdfUrl ? (
            <>
              <iframe className="lyrics__pdf" src={`${lyrics.pdfUrl}#view=FitH`} title={`Letra de ${song.title} (PDF)`} />
              <ExternalLink href={lyrics.pdfUrl} className="more">
                Abrir o PDF
              </ExternalLink>
            </>
          ) : (
            <div className="lyrics__text">{lyrics.text}</div>
          )}
        </div>
      )}
    </Dialog>
  );
}

// ---------- links ----------

function LinksDialog({ song, onClose }: { song: Song; onClose: () => void }) {
  const youTube = song.links.filter((l) => l.kind === 'youtube');
  const label = (l: MusicLink) =>
    l.kind === 'spotify' ? 'Abrir no Spotify' : youTube.length > 1 ? `Abrir no YouTube (${youTube.indexOf(l) + 1})` : 'Abrir no YouTube';

  return (
    <Dialog title={`Links · ${song.title}`} onClose={onClose}>
      <ul className="link-list">
        {song.links.map((l) => (
          <li key={`${l.kind}-${l.url}`}>
            <ExternalLink href={l.url} className={`btn ${l.kind === 'spotify' ? 'btn--primary' : 'btn--ghost'}`}>
              <Icon name={l.kind} />
              {label(l)}
            </ExternalLink>
          </li>
        ))}
      </ul>
    </Dialog>
  );
}

// ---------- videos (members only) ----------

function VideosDialog({ song, onClose, onCount }: { song: Song; onClose: () => void; onCount: (n: number) => void }) {
  const [videos, setVideos] = useState<Video[] | null>(null);
  const [failed, setFailed] = useState(false);
  const [selected, setSelected] = useState<number | null>(null);
  const [file, setFile] = useState<File | null>(null);
  const [title, setTitle] = useState('');
  const [upload, setUpload] = useState<{ busy: boolean; error?: string }>({ busy: false });
  const [removing, setRemoving] = useState<Video | null>(null);
  const [removeState, setRemoveState] = useState<{ busy: boolean; error?: string }>({ busy: false });
  const logged = useRef(new Set<number>());

  const load = useCallback(() => {
    musicApi.videos(song.id).then((r) => {
      if (r.kind !== 'ok') return setFailed(true);
      setVideos(r.data);
      onCount(r.data.length);
    });
  }, [song.id, onCount]);

  useEffect(() => {
    load();
  }, [song.id]);

  const pick = (f: File | null) => {
    setUpload({ busy: false });
    if (f && f.size > MAX_VIDEO_BYTES) {
      setFile(null);
      setUpload({ busy: false, error: 'O vídeo não pode exceder 100 MB.' });
      return;
    }
    setFile(f);
    setTitle(f ? `${song.title} - Vídeo ${(videos?.length ?? 0) + 1}` : '');
  };

  const send = async () => {
    if (!file) return;
    setUpload({ busy: true });
    const r = await musicApi.uploadVideo(song.id, file, title);
    if (r.kind === 'ok') {
      setFile(null);
      setTitle('');
      setUpload({ busy: false });
      load();
    } else {
      setUpload({ busy: false, error: r.kind === 'invalid' ? Object.values(r.errors)[0] : 'Não foi possível enviar o vídeo.' });
    }
  };

  const remove = async () => {
    if (!removing) return;
    setRemoveState({ busy: true });
    const r = await musicApi.deleteVideo(removing.id);
    if (r.kind === 'ok') {
      if (selected === removing.id) setSelected(null);
      setRemoving(null);
      setRemoveState({ busy: false });
      load();
    } else setRemoveState({ busy: false, error: 'Não foi possível eliminar o vídeo.' });
  };

  const current = videos?.find((v) => v.id === selected);

  return (
    <>
      <Dialog title={`Vídeos · ${song.title}`} size="lg" onClose={onClose}>
        {failed ? (
          <p className="form__banner" role="alert">
            Não foi possível carregar os vídeos.
          </p>
        ) : !videos ? (
          <Loading label="A carregar os vídeos…" />
        ) : (
          <div className="videos">
            {videos.length === 0 ? (
              <p className="note">Ainda não há vídeos desta música.</p>
            ) : (
              <ul className="videos__list">
                {videos.map((v) => (
                  <li key={v.id} className={`videos__item${v.id === selected ? ' videos__item--on' : ''}`}>
                    <button type="button" className="videos__pick" aria-pressed={v.id === selected} onClick={() => setSelected(v.id === selected ? null : v.id)}>
                      <Icon name="playFill" />
                      {v.title}
                    </button>
                    {v.canDelete && <IconButton icon="trash" tone="danger" label={`Eliminar ${v.title}`} onClick={() => setRemoving(v)} />}
                  </li>
                ))}
              </ul>
            )}

            {current && (
              <video
                key={current.id}
                className="videos__player"
                controls
                playsInline
                preload="metadata"
                onPlay={() => {
                  if (!logged.current.has(current.id)) {
                    logged.current.add(current.id);
                    void musicApi.videoPlayed(current.id);
                  }
                }}
              >
                <source src={current.url} type={current.mimeType} />
                {current.mimeType === 'video/quicktime' && <source src={current.url} type="video/mp4" />}
                Este navegador não reproduz este formato. Vídeos gravados em telemóvel (HEVC) abrem melhor no Safari.
              </video>
            )}

            <div className="videos__upload">
              <h3 className="stats__title">Adicionar vídeo</h3>
              {upload.busy ? (
                <Loading label="A enviar o vídeo…" />
              ) : (
                <>
                  <label className="btn btn--ghost btn--sm">
                    <Icon name="upload" />
                    {file ? file.name : 'Escolher vídeo'}
                    <input
                      className="sr-only"
                      type="file"
                      accept="video/*"
                      onChange={(e) => {
                        pick(e.target.files?.[0] ?? null);
                        e.target.value = '';
                      }}
                    />
                  </label>
                  <p className="form__hint">Até 100 MB.</p>
                  {file && (
                    <div className="form">
                      <div className="form__field">
                        <label htmlFor="video-title">Título do vídeo</label>
                        <input id="video-title" maxLength={200} value={title} onChange={(e) => setTitle(e.target.value)} />
                      </div>
                      <div className="videos__send">
                        <button type="button" className="btn btn--ghost btn--sm" onClick={() => pick(null)}>
                          Cancelar
                        </button>
                        <button type="button" className="btn btn--primary btn--sm" onClick={send} disabled={!title.trim()}>
                          <Icon name="upload" />
                          Enviar vídeo
                        </button>
                      </div>
                    </div>
                  )}
                  {upload.error && (
                    <p className="form__error" role="alert">
                      {upload.error}
                    </p>
                  )}
                </>
              )}
            </div>
          </div>
        )}
      </Dialog>
      {removing && (
        <ConfirmDialog
          title="Eliminar vídeo"
          confirmLabel="Eliminar"
          busy={removeState.busy}
          error={removeState.error}
          onConfirm={remove}
          onClose={() => {
            setRemoving(null);
            setRemoveState({ busy: false });
          }}
        >
          <p>
            Eliminar o vídeo <strong>{removing.title}</strong>?
          </p>
        </ConfirmDialog>
      )}
    </>
  );
}
