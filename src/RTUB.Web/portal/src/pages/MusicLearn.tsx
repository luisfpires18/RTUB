import { useCallback, useEffect, useState } from 'react';
import { EmptyState } from '../components/ui/EmptyState';
import { PageHead } from '../components/ui/PageHead';
import { Icon } from '../components/icons';
import { PlayAlongField } from '../components/MusicPlayAlongField';
import { Dialog, IconButton } from '../components/MusicUi';
import { musicApi, type LearnSong } from '../data/musicApi';
import { AddDialog, NotifyDialog } from '../components/MusicLearnDialogs';
import { Loading } from '../components/ui/Loading';
import { ConfirmDialog } from '../components/ui/ConfirmDialog';

/** Group id of the starred songs (album ids start at 1). */
const FAVORITES = 0;

type Open =
  | { kind: 'add' }
  | { kind: 'edit'; song: LearnSong }
  | { kind: 'delete'; song: LearnSong; busy?: boolean; error?: string }
  | { kind: 'notify'; song: LearnSong };

/**
 * /music/learn - Aprender Músicas, members only: every song that has an Aprender chart, one
 * /music grid per album. A card opens the song's Aprender page and nothing else. The Owner adds a
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

  // Starred songs come first, as their own group; the rest are albums. The list comes sorted by
  // album (newest first), so the albums are runs of it.
  const groups: { id: number; title: string; year: number | null; songs: LearnSong[] }[] = [];
  const starred = (songs ?? []).filter((s) => s.starred);
  if (starred.length > 0) groups.push({ id: FAVORITES, title: 'Favoritas', year: null, songs: starred });
  for (const s of (songs ?? []).filter((x) => !x.starred)) {
    const last = groups[groups.length - 1];
    if (last?.id === s.albumId) last.songs.push(s);
    else groups.push({ id: s.albumId, title: s.albumTitle, year: s.year, songs: [s] });
  }

  const toggleStar = async (song: LearnSong) => {
    const next = !song.starred;
    const set = (starred: boolean) =>
      setState((cur) => ({ ...cur, songs: cur.songs?.map((x) => (x.songId === song.songId ? { ...x, starred } : x)) }));
    set(next);
    const r = await musicApi.starPlayAlong(song.songId, next);
    if (r.kind !== 'ok') set(!next);
  };

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
      <PageHead eyebrow="Música" title="Aprender Músicas" titleId="learn-title" lead="As músicas com pauta para aprender: os acordes a acompanhar a gravação.">
        {canManage && (
          <div className="music__actions">
            <button type="button" className="btn btn--primary" onClick={() => setOpen({ kind: 'add' })}>
              <Icon name="plus" />
              Adicionar música
            </button>
          </div>
        )}
      </PageHead>

      {state.failed ? (
        <EmptyState role="alert" title="Não foi possível carregar as músicas.">
          <button type="button" className="btn btn--primary" onClick={() => load()}>
            Tentar novamente
          </button>
        </EmptyState>
      ) : !songs ? (
        <Loading label="A carregar as músicas…" />
      ) : songs.length === 0 ? (
        <EmptyState icon="disc" title="Ainda não há músicas para aprender." />
      ) : (
        groups.map((album) => (
          <section key={album.id} className="learn-album" aria-labelledby={`learn-album-${album.id}`}>
            <h2 id={`learn-album-${album.id}`} className="learn-album__title">
              {album.title}
              {album.year && <span className="learn-album__year">{album.year}</span>}
            </h2>
            <ul className="albums">
              {album.songs.map((s) => (
                <li key={s.songId} className="album-card">
                  <a className="album-card__link" href={`/music/play/${s.songId}`}>
                    <span className="album-card__cover album-card__cover--empty" aria-hidden="true">
                      <Icon name="music" />
                    </span>
                    <span className="album-card__body">
                      <span className="album-card__title">{s.title}</span>
                      <span className="album-card__meta">
                        <span>{s.views === 1 ? '1 visualização' : `${s.views} visualizações`}</span>
                        {album.id === FAVORITES && <span>{s.albumTitle}</span>}
                      </span>
                    </span>
                  </a>
                  <button
                    type="button"
                    className={`album-card__star${s.starred ? ' is-on' : ''}`}
                    aria-pressed={s.starred}
                    title={s.starred ? 'Tirar das favoritas' : 'Marcar como favorita'}
                    onClick={() => void toggleStar(s)}
                  >
                    <Icon name="star" />
                    <span className="sr-only">{s.starred ? `Tirar das favoritas: ${s.title}` : `Marcar como favorita: ${s.title}`}</span>
                  </button>
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
