// Lyrics, links and members-only videos dialogs opened from an album song.
import { useCallback, useEffect, useRef, useState } from 'react';
import { ExternalLink, Loading } from './AppShared';
import { Icon } from './icons';
import { ConfirmDialog, Dialog, IconButton } from './MusicUi';
import { MAX_VIDEO_BYTES, musicApi, type Lyrics, type MusicLink, type Song, type Video } from '../data/musicApi';

// ---------- lyrics ----------

export function LyricsDialog({ song, onClose }: { song: Song; onClose: () => void }) {
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

export function LinksDialog({ song, onClose }: { song: Song; onClose: () => void }) {
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

export function VideosDialog({ song, onClose, onCount }: { song: Song; onClose: () => void; onCount: (n: number) => void }) {
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
