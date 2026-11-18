import { useEffect, useState, type FormEvent } from 'react';
import { FieldError } from './ui/FieldError';
import { DialogFooter } from './ui/DialogFooter';
import { Banner } from './ui/Banner';
import { CheckField } from './ui/CheckField';
import { IconAction } from './ui/IconAction';
import { Icon } from './icons';
import { Dialog } from './MusicUi';
import { musicApi, type FieldErrors, type SongInput } from '../data/musicApi';
import { failedMessage } from '../data/musicFormHelpers';
import { PlayAlongField } from './MusicPlayAlongField';
import { FormField, fieldProps } from './ui/FormField';

// ---------- song ----------

/** Create or edit a song: the song's fields (duration is kept as stored). */
export function SongForm({
  albumId,
  songId,
  canPlayAlong,
  onSaved,
  onClose,
}: {
  albumId: number;
  songId: number | null;
  /** Aprender charts are the Owner's (the server refuses anyone else). */
  canPlayAlong: boolean;
  onSaved: () => void;
  onClose: () => void;
}) {
  const [form, setForm] = useState<SongInput | null>(songId === null ? emptySong() : null);
  const [errors, setErrors] = useState<FieldErrors>({});
  const [banner, setBanner] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [hasPlayAlong, setHasPlayAlong] = useState(false);

  useEffect(() => {
    if (songId === null) return;
    musicApi.songForEdit(songId).then((r) => {
      if (r.kind !== 'ok') {
        setBanner('Não foi possível abrir esta música para edição.');
        return;
      }
      const s = r.data;
      setHasPlayAlong(s.hasPlayAlong);
      setForm({
        title: s.title,
        trackNumber: s.trackNumber,
        lyricAuthor: s.lyricAuthor ?? '',
        musicAuthor: s.musicAuthor ?? '',
        adaptation: s.adaptation ?? '',
        spotifyUrl: s.spotifyUrl ?? '',
        hasAudio: s.hasAudio,
        youTubeUrls: s.youTubeUrls.length ? s.youTubeUrls : [''],
        lyrics: s.lyrics ?? '',
      });
    });
  }, [songId]);

  const set = <K extends keyof SongInput>(key: K, value: SongInput[K]) => setForm((f) => (f ? { ...f, [key]: value } : f));

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!form) return;
    setBusy(true);
    setBanner(null);
    const r = await musicApi.saveSong(albumId, songId, { ...form, youTubeUrls: form.youTubeUrls.filter((u) => u.trim()) });
    setBusy(false);
    if (r.kind === 'ok') onSaved();
    else if (r.kind === 'invalid') setErrors(r.errors);
    else if (r.kind === 'forbidden') setBanner('Não tem permissão para guardar músicas.');
    else setBanner(failedMessage(r.kind === 'failed' ? r.title : undefined));
  };

  return (
    <Dialog
      title={songId === null ? 'Nova música' : 'Editar música'}
      size="lg"
      onClose={onClose}
      footer={<DialogFooter form="song-form" busy={busy} disabled={!form} onClose={onClose} />}
    >
      <Banner text={banner} />
      {!form ? (
        !banner && <p className="note">A carregar…</p>
      ) : (
        <form id="song-form" className="form" onSubmit={submit} noValidate>
          <div className="form__row">
            <FormField id="song-title" label="Título *" error={errors.title}>
              <input {...fieldProps('song-title', errors.title)} required maxLength={200} value={form.title} onChange={(e) => set('title', e.target.value)} />
            </FormField>
            <FormField id="song-track" label="Número da faixa" error={errors.trackNumber}>
              <input
                {...fieldProps('song-track', errors.trackNumber)}
                type="number"
                inputMode="numeric"
                min={1}
                max={999}
                value={form.trackNumber ?? ''}
                onChange={(e) => set('trackNumber', e.target.value === '' ? null : Number(e.target.value))}
              />
            </FormField>
          </div>
          <div className="form__row">
            <FormField id="song-lyric-author" label="Autor da letra" error={errors.lyricAuthor}>
              <input {...fieldProps('song-lyric-author', errors.lyricAuthor)} maxLength={200} value={form.lyricAuthor} onChange={(e) => set('lyricAuthor', e.target.value)} />
            </FormField>
            <FormField id="song-music-author" label="Autor da música" error={errors.musicAuthor}>
              <input {...fieldProps('song-music-author', errors.musicAuthor)} maxLength={200} value={form.musicAuthor} onChange={(e) => set('musicAuthor', e.target.value)} />
            </FormField>
          </div>
          <div className="form__row">
            <FormField id="song-adaptation" label="Adaptação" error={errors.adaptation}>
              <input {...fieldProps('song-adaptation', errors.adaptation)} maxLength={200} value={form.adaptation} onChange={(e) => set('adaptation', e.target.value)} />
            </FormField>
            <FormField id="song-spotify" label="Endereço no Spotify" error={errors.spotifyUrl}>
              <input
                {...fieldProps('song-spotify', errors.spotifyUrl)}
                type="url"
                inputMode="url"
                maxLength={500}
                placeholder="https://open.spotify.com/…"
                value={form.spotifyUrl}
                onChange={(e) => set('spotifyUrl', e.target.value)}
              />
            </FormField>
          </div>
          <CheckField checked={form.hasAudio} onChange={(c) => set('hasAudio', c)}>
            Tem ficheiro de áudio disponível
          </CheckField>

          <fieldset className="urls">
            <legend className="form__label">Endereços do YouTube</legend>
            {form.youTubeUrls.map((url, i) => (
              <div key={i} className="urls__row">
                <input
                  type="url"
                  inputMode="url"
                  maxLength={500}
                  aria-label={`Endereço do YouTube ${i + 1}`}
                  placeholder="https://www.youtube.com/…"
                  value={url}
                  onChange={(e) => set('youTubeUrls', form.youTubeUrls.map((u, j) => (j === i ? e.target.value : u)))}
                />
                <IconAction
                  icon="trash"
                  label={`Remover endereço ${i + 1}`}
                  title="Remover"
                  onClick={() => set('youTubeUrls', form.youTubeUrls.filter((_, j) => j !== i))}
                />
              </div>
            ))}
            {form.youTubeUrls.length < 10 && (
              <button type="button" className="btn btn--ghost btn--sm" onClick={() => set('youTubeUrls', [...form.youTubeUrls, ''])}>
                <Icon name="plus" />
                Adicionar endereço
              </button>
            )}
            <FieldError text={errors.youTubeUrls} alert={false} />
          </fieldset>

          <FormField id="song-lyrics" label="Letra" hint="Só quando a música não está no cancioneiro em PDF." error={errors.lyrics}>
            <textarea
              {...fieldProps('song-lyrics', errors.lyrics, 'hint')}
              rows={10}
              maxLength={10000}
              value={form.lyrics}
              onChange={(e) => set('lyrics', e.target.value)}
            />
          </FormField>

          {songId !== null && canPlayAlong && <PlayAlongField songId={songId} has={hasPlayAlong} onChange={setHasPlayAlong} />}
        </form>
      )}
    </Dialog>
  );
}

const emptySong = (): SongInput => ({
  title: '',
  trackNumber: null,
  lyricAuthor: '',
  musicAuthor: '',
  adaptation: '',
  spotifyUrl: '',
  hasAudio: false,
  youTubeUrls: [''],
  lyrics: '',
});
