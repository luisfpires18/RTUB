import { useCallback, useEffect, useState } from 'react';
import { Loading } from './App';
import { Icon } from './icons';
import { PlayAlongField } from './MusicForms';
import { ConfirmDialog, Dialog, Field, fieldProps, IconButton } from './MusicUi';
import { musicApi, type AlbumSummary, type LearnSong, type Song } from './musicApi';

const MAX_NOTICE = 300;

type Open =
  | { kind: 'add' }
  | { kind: 'edit'; song: LearnSong }
  | { kind: 'delete'; song: LearnSong; busy?: boolean; error?: string }
  | { kind: 'notify'; song: LearnSong };

/**
 * /music/learn - Aprender Músicas (task 095), members only: every song that has an Aprender chart, one
 * /music grid per album. A card opens the song's Aprender page and nothing else. The Owner (097) adds a
 * song's chart here, edits or removes it, and tells the members about a new one (the bell).
 */
export default function MusicLearn() {
  const [state, setState] = useState<{ songs?: LearnSong[]; canManage?: boolean; failed?: boolean }>({});
  const [open, setOpen] = useState<Open | null>(null);

  const load = useCallback((quiet = false) => {
    if (!quiet) setState({});
    musicApi.learnSongs().then((r) => setState(r.kind === 'ok' ? r.data : { failed: true }));
  }, []);

  useEffect(() => {
    document.title = 'Aprender Músicas · RTUB';
    load();
  }, [load]);

  const { songs, canManage } = state;

  // The list comes sorted by album (newest first), so the albums are runs of it.
  const albums: { id: number; title: string; year: number | null; songs: LearnSong[] }[] = [];
  for (const s of songs ?? []) {
    const last = albums[albums.length - 1];
    if (last?.id === s.albumId) last.songs.push(s);
    else albums.push({ id: s.albumId, title: s.albumTitle, year: s.year, songs: [s] });
  }

  const remove = async () => {
    if (open?.kind !== 'delete') return;
    setOpen({ ...open, busy: true, error: undefined });
    const r = await musicApi.deletePlayAlong(open.song.songId);
    if (r.kind === 'ok') {
      setOpen(null);
      load(true);
    } else setOpen({ ...open, busy: false, error: 'Não foi possível remover a pauta.' });
  };

  return (
    <section className="page wrap music" aria-labelledby="learn-title">
      <header className="page__head events-page__head">
        <div>
          <p className="eyebrow">Música</p>
          <h1 id="learn-title" className="page__title">
            Aprender Músicas
          </h1>
          <p className="page__lead">As músicas com pauta para aprender: os acordes a acompanhar a gravação.</p>
        </div>
        {canManage && (
          <div className="music__actions">
            <button type="button" className="btn btn--primary" onClick={() => setOpen({ kind: 'add' })}>
              <Icon name="plus" />
              Adicionar música
            </button>
          </div>
        )}
      </header>

      {state.failed ? (
        <div className="state" role="alert">
          <p className="state__title">Não foi possível carregar as músicas.</p>
          <button type="button" className="btn btn--primary" onClick={() => load()}>
            Tentar novamente
          </button>
        </div>
      ) : !songs ? (
        <Loading label="A carregar as músicas…" />
      ) : songs.length === 0 ? (
        <div className="state">
          <Icon name="disc" className="state__icon" />
          <p className="state__title">Ainda não há músicas para aprender.</p>
        </div>
      ) : (
        albums.map((album) => (
          <section key={album.id} className="learn-album" aria-labelledby={`learn-album-${album.id}`}>
            <h2 id={`learn-album-${album.id}`} className="learn-album__title">
              {album.title}
              {album.year && <span className="learn-album__year">{album.year}</span>}
            </h2>
            <ul className="albums">
              {album.songs.map((s) => (
                <li key={s.songId} className="album-card">
                  <a className="album-card__link" href={`/music/play/${s.songId}`}>
                    {s.coverUrl ? (
                      <img className="album-card__cover" src={s.coverUrl} alt="" width="400" height="400" loading="lazy" />
                    ) : (
                      <span className="album-card__cover album-card__cover--empty" aria-hidden="true">
                        <Icon name="disc" />
                      </span>
                    )}
                    <span className="album-card__body">
                      <span className="album-card__title">{s.title}</span>
                    </span>
                  </a>
                  {canManage && (
                    <div className="album-card__manage">
                      <IconButton icon="bell" label={`Avisar os membros: ${s.title}`} onClick={() => setOpen({ kind: 'notify', song: s })} />
                      <IconButton icon="pencil" label={`Editar a pauta de ${s.title}`} onClick={() => setOpen({ kind: 'edit', song: s })} />
                      <IconButton icon="trash" tone="danger" label={`Remover a pauta de ${s.title}`} onClick={() => setOpen({ kind: 'delete', song: s })} />
                    </div>
                  )}
                </li>
              ))}
            </ul>
          </section>
        ))
      )}

      {open?.kind === 'add' && (
        <AddDialog
          taken={new Set((songs ?? []).map((s) => s.songId))}
          onChanged={() => load(true)}
          onClose={() => setOpen(null)}
        />
      )}

      {open?.kind === 'edit' && (
        <Dialog title={`Pauta de ${open.song.title}`} onClose={() => setOpen(null)}>
          <PlayAlongField
            songId={open.song.songId}
            has
            onChange={(has) => {
              if (!has) setOpen(null);
              load(true);
            }}
          />
        </Dialog>
      )}

      {open?.kind === 'delete' && (
        <ConfirmDialog
          title="Remover pauta"
          confirmLabel="Remover"
          busy={open.busy === true}
          error={open.error}
          onConfirm={remove}
          onClose={() => setOpen(null)}
        >
          <p>
            Remover a pauta de <strong>{open.song.title}</strong>? A música sai de Aprender Músicas; a música em si fica como está.
          </p>
        </ConfirmDialog>
      )}

      {open?.kind === 'notify' && <NotifyDialog song={open.song} onClose={() => setOpen(null)} />}
    </section>
  );
}

/** Owner: pick an album and one of its songs without a chart, then send the .json (and tick dedilhado). */
function AddDialog({ taken, onChanged, onClose }: { taken: Set<number>; onChanged: () => void; onClose: () => void }) {
  const [albums, setAlbums] = useState<AlbumSummary[]>([]);
  const [albumId, setAlbumId] = useState<number | null>(null);
  const [songs, setSongs] = useState<Song[]>([]);
  const [songId, setSongId] = useState<number | null>(null);
  const [has, setHas] = useState(false);

  useEffect(() => {
    musicApi.albums().then((r) => r.kind === 'ok' && setAlbums(r.data.albums));
  }, []);

  useEffect(() => {
    setSongs([]);
    setSongId(null);
    setHas(false);
    if (albumId === null) return;
    // `taken` is the list as it was when the dialog opened: a song added here stays selected.
    musicApi.album(albumId).then((r) => r.kind === 'ok' && setSongs(r.data.songs.filter((s) => !taken.has(s.id))));
  }, [albumId, taken]);

  return (
    <Dialog title="Adicionar música a Aprender" onClose={onClose}>
      <div className="form">
        <Field id="learn-album" label="Álbum">
          <span className="control control--select">
            <select {...fieldProps('learn-album')} value={albumId ?? ''} onChange={(e) => setAlbumId(Number(e.target.value) || null)}>
              <option value="">Escolher álbum</option>
              {albums.map((a) => (
                <option key={a.id} value={a.id}>
                  {a.title}
                </option>
              ))}
            </select>
          </span>
        </Field>
        {albumId !== null && (
          <Field id="learn-song" label="Música" hint={songs.length === 0 ? 'Todas as músicas deste álbum já têm pauta.' : undefined}>
            <span className="control control--select">
              <select
                {...fieldProps('learn-song', undefined, songs.length === 0 ? 'hint' : undefined)}
                value={songId ?? ''}
                onChange={(e) => {
                  setSongId(Number(e.target.value) || null);
                  setHas(false);
                }}
              >
                <option value="">Escolher música</option>
                {songs.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.title}
                  </option>
                ))}
              </select>
            </span>
          </Field>
        )}
        {songId !== null && (
          <PlayAlongField
            key={songId}
            songId={songId}
            has={has}
            onChange={(now) => {
              setHas(now);
              onChanged();
            }}
          />
        )}
      </div>
    </Dialog>
  );
}

/** Owner: a push to the members who can see the song. Nothing is sent before "Enviar". */
function NotifyDialog({ song, onClose }: { song: LearnSong; onClose: () => void }) {
  const [message, setMessage] = useState(`Já podes aprender "${song.title}" com os acordes a acompanhar a gravação.`);
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<{ text: string; error: boolean } | null>(null);

  const send = async () => {
    setBusy(true);
    setResult(null);
    const r = await musicApi.notifyPlayAlong(song.songId, message);
    setBusy(false);
    setResult(
      r.kind === 'ok'
        ? { text: 'Notificação enviada.', error: false }
        : { text: r.kind === 'invalid' ? (r.errors.message ?? 'Mensagem inválida.') : 'Não foi possível enviar a notificação.', error: true },
    );
  };

  const sent = result?.error === false;

  return (
    <Dialog
      title="Avisar os membros"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            {sent ? 'Fechar' : 'Cancelar'}
          </button>
          {!sent && (
            <button type="button" className="btn btn--primary" onClick={send} disabled={busy || message.trim().length === 0}>
              {busy ? <span className="spinner spinner--small" aria-hidden="true" /> : <Icon name="bell" />}
              Enviar
            </button>
          )}
        </>
      }
    >
      <div className="form">
        <p className="form__hint">
          Notificação push "Nova música para aprender" para os membros que podem ver <strong>{song.title}</strong>. Abre a música em Aprender.
        </p>
        <Field id="learn-notice" label="Mensagem" hint={`${message.length} / ${MAX_NOTICE}`}>
          <textarea
            {...fieldProps('learn-notice', undefined, 'hint')}
            rows={3}
            maxLength={MAX_NOTICE}
            value={message}
            disabled={busy || sent}
            onChange={(e) => setMessage(e.target.value)}
          />
        </Field>
        {result && (
          <p className={result.error ? 'form__banner' : 'form__hint'} role={result.error ? 'alert' : 'status'}>
            {result.text}
          </p>
        )}
      </div>
    </Dialog>
  );
}
