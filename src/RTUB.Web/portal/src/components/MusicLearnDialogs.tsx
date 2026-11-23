// Owner dialogs of the Aprender page: add a play-along chart to a song and notify members.
import { Spinner } from './ui/Spinner';
import { useEffect, useState } from 'react';
import { Icon } from './icons';
import { PlayAlongField } from './MusicPlayAlongField';
import { Dialog } from './MusicUi';
import { musicApi, type AlbumSummary, type LearnSong, type Song } from '../data/musicApi';
import { FormField, fieldProps } from './ui/FormField';
import { SelectField } from './ui/SearchTools';

const MAX_NOTICE = 300;

/** Owner: pick an album and one of its songs without a chart, then send the .json (and tick dedilhado). */
export function AddDialog({ taken, onChanged, onClose }: { taken: Set<number>; onChanged: () => void; onClose: () => void }) {
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
    let live = true;
    // `taken` is the list as it was when the dialog opened: a song added here stays selected.
    musicApi.album(albumId).then((r) => live && r.kind === 'ok' && setSongs(r.data.songs.filter((s) => !taken.has(s.id))));
    return () => {
      live = false;
    };
  }, [albumId, taken]);

  return (
    <Dialog title="Adicionar música a Aprender" onClose={onClose}>
      <div className="form">
        <FormField id="learn-album" label="Álbum">
          <SelectField {...fieldProps('learn-album')} value={albumId ?? ''} onChange={(v) => setAlbumId(Number(v) || null)}>
            <option value="">Escolher álbum</option>
            {albums.map((a) => (
              <option key={a.id} value={a.id}>
                {a.title}
              </option>
            ))}
          </SelectField>
        </FormField>
        {albumId !== null && (
          <FormField id="learn-song" label="Música" hint={songs.length === 0 ? 'Todas as músicas deste álbum já têm pauta.' : undefined}>
            <SelectField
              {...fieldProps('learn-song', undefined, songs.length === 0 ? 'hint' : undefined)}
              value={songId ?? ''}
              onChange={(v) => {
                setSongId(Number(v) || null);
                setHas(false);
              }}
            >
              <option value="">Escolher música</option>
              {songs.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.title}
                </option>
              ))}
            </SelectField>
          </FormField>
        )}
        {songId !== null && (
          <PlayAlongField
            key={songId}
            songId={songId}
            has={has}
            confirm
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
export function NotifyDialog({ song, onClose }: { song: LearnSong; onClose: () => void }) {
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
              {busy ? <Spinner small /> : <Icon name="bell" />}
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
        <FormField id="learn-notice" label="Mensagem" hint={`${message.length} / ${MAX_NOTICE}`}>
          <textarea
            {...fieldProps('learn-notice', undefined, 'hint')}
            rows={3}
            maxLength={MAX_NOTICE}
            value={message}
            disabled={busy || sent}
            onChange={(e) => setMessage(e.target.value)}
          />
        </FormField>
        {result && (
          <p className={result.error ? 'form__banner' : 'form__hint'} role={result.error ? 'alert' : 'status'}>
            {result.text}
          </p>
        )}
      </div>
    </Dialog>
  );
}
