import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { Cropper } from './Cropper';
import { Icon } from './icons';
import { Dialog, Field, fieldProps } from './MusicUi';
import { musicApi, type AlbumEdit, type AlbumInput, type FieldErrors, type Member, type SongInput } from './musicApi';

const failedMessage = (title?: string) => title ?? 'Não foi possível guardar. Tente novamente.';

// ---------- album ----------

/**
 * Create or edit an album. A new cover always goes through the square cropper first; private is
 * for any manager, exclusive and its member list only for the Owner (the server enforces both).
 */
export function AlbumForm({
  albumId,
  canManageExclusive,
  onSaved,
  onClose,
}: {
  albumId: number | null;
  canManageExclusive: boolean;
  onSaved: () => void;
  onClose: () => void;
}) {
  const [loaded, setLoaded] = useState<AlbumEdit | null>(albumId === null ? emptyAlbum : null);
  const [form, setForm] = useState<AlbumInput>(toInput(emptyAlbum));
  const [access, setAccess] = useState<Member[]>([]);
  const [members, setMembers] = useState<Member[] | null>(null);
  const [search, setSearch] = useState('');
  const [picked, setPicked] = useState<File | null>(null);
  const [cover, setCover] = useState<{ blob: Blob; preview: string } | null>(null);
  const [errors, setErrors] = useState<FieldErrors>({});
  const [banner, setBanner] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (albumId === null) return;
    musicApi.albumForEdit(albumId).then((r) => {
      if (r.kind === 'ok') {
        setLoaded(r.data);
        setForm(toInput(r.data));
        setAccess(r.data.accessMembers);
      } else setBanner('Não foi possível abrir este álbum para edição.');
    });
  }, [albumId]);

  useEffect(() => {
    if (canManageExclusive) musicApi.members().then((r) => r.kind === 'ok' && setMembers(r.data));
  }, [canManageExclusive]);

  const matches = useMemo(() => {
    const q = normalize(search);
    if (!q || !members) return [];
    return members.filter((m) => !access.some((a) => a.id === m.id) && normalize(m.displayName).includes(q)).slice(0, 10);
  }, [search, members, access]);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setBanner(null);
    const r = await musicApi.saveAlbum(albumId, { ...form, accessUserIds: access.map((a) => a.id) }, cover?.blob ?? null);
    setBusy(false);
    if (r.kind === 'ok') onSaved();
    else if (r.kind === 'invalid') setErrors(r.errors);
    else if (r.kind === 'forbidden') setBanner('Não tem permissão para guardar álbuns.');
    else setBanner(failedMessage(r.kind === 'failed' ? r.title : undefined));
  };

  const set = <K extends keyof AlbumInput>(key: K, value: AlbumInput[K]) => setForm((f) => ({ ...f, [key]: value }));
  const currentCover = cover?.preview ?? loaded?.coverUrl ?? null;

  return (
    <>
      <Dialog
        title={albumId === null ? 'Novo álbum' : 'Editar álbum'}
        onClose={onClose}
        footer={
          <>
            <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
              Cancelar
            </button>
            <button type="submit" form="album-form" className="btn btn--primary" disabled={busy || !loaded}>
              {busy && <span className="spinner spinner--small" aria-hidden="true" />}
              Guardar
            </button>
          </>
        }
      >
        {!loaded && !banner ? (
          <p className="note">A carregar…</p>
        ) : (
          <form id="album-form" className="form" onSubmit={submit} noValidate>
            {banner && (
              <p className="form__banner" role="alert">
                {banner}
              </p>
            )}
            <Field id="album-title" label="Título *" error={errors.title}>
              <input {...fieldProps('album-title', errors.title)} required maxLength={200} value={form.title} onChange={(e) => set('title', e.target.value)} />
            </Field>
            <Field id="album-year" label="Ano" error={errors.year}>
              <input
                {...fieldProps('album-year', errors.year)}
                type="number"
                inputMode="numeric"
                min={1900}
                max={new Date().getFullYear()}
                value={form.year}
                onChange={(e) => set('year', e.target.value)}
              />
            </Field>
            <Field id="album-description" label="Descrição" error={errors.description}>
              <textarea
                {...fieldProps('album-description', errors.description)}
                rows={3}
                maxLength={1000}
                value={form.description}
                onChange={(e) => set('description', e.target.value)}
              />
            </Field>
            <label className="form__check">
              <input type="checkbox" checked={form.isPrivate} onChange={(e) => set('isPrivate', e.target.checked)} />
              Privado: só membros com sessão iniciada
            </label>

            {canManageExclusive && (
              <fieldset className="access">
                <label className="form__check">
                  <input type="checkbox" checked={form.isExclusive} onChange={(e) => set('isExclusive', e.target.checked)} />
                  Exclusivo: só os membros escolhidos
                </label>
                {form.isExclusive && (
                  <>
                    {access.length > 0 && (
                      <ul className="access__chips" aria-label="Membros com acesso">
                        {access.map((m) => (
                          <li key={m.id} className="chip chip--removable">
                            <img src={m.avatarUrl} alt="" width="24" height="24" />
                            {m.displayName}
                            <button type="button" className="chip__remove" onClick={() => setAccess((a) => a.filter((x) => x.id !== m.id))}>
                              <Icon name="close" />
                              <span className="sr-only">Remover {m.displayName}</span>
                            </button>
                          </li>
                        ))}
                      </ul>
                    )}
                    <Field id="album-access" label="Adicionar membro">
                      <input id="album-access" type="search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Pesquisar por nome…" />
                    </Field>
                    {search && (
                      <ul className="access__results">
                        {matches.length === 0 ? (
                          <li className="note">Nenhum membro encontrado.</li>
                        ) : (
                          matches.map((m) => (
                            <li key={m.id}>
                              <button
                                type="button"
                                className="access__pick"
                                onClick={() => {
                                  setAccess((a) => [...a, m]);
                                  setSearch('');
                                }}
                              >
                                <img src={m.avatarUrl} alt="" width="28" height="28" />
                                {m.displayName}
                              </button>
                            </li>
                          ))
                        )}
                      </ul>
                    )}
                  </>
                )}
              </fieldset>
            )}

            <div className="form__field">
              <span className="form__label">Capa</span>
              <div className="cover-pick">
                {currentCover ? (
                  <img className="cover-pick__img" src={currentCover} alt="Capa atual" width="120" height="120" />
                ) : (
                  <span className="cover-pick__img cover-pick__img--empty">
                    <Icon name="disc" />
                  </span>
                )}
                <div className="cover-pick__actions">
                  <label className="btn btn--ghost btn--sm">
                    <Icon name="upload" />
                    {currentCover ? 'Trocar imagem' : 'Escolher imagem'}
                    <input
                      className="sr-only"
                      type="file"
                      accept="image/*"
                      onChange={(e) => {
                        const file = e.target.files?.[0];
                        e.target.value = '';
                        if (file) setPicked(file);
                      }}
                    />
                  </label>
                  {cover && (
                    <button type="button" className="btn btn--ghost btn--sm" onClick={() => setCover(null)}>
                      Desfazer
                    </button>
                  )}
                  <p className="form__hint">A imagem é recortada em quadrado antes de guardar.</p>
                  {errors.cover && <p className="form__error">{errors.cover}</p>}
                </div>
              </div>
            </div>
          </form>
        )}
      </Dialog>

      {picked && (
        <Cropper
          file={picked}
          onCancel={() => setPicked(null)}
          onDone={(blob, preview) => {
            setCover({ blob, preview });
            setPicked(null);
          }}
        />
      )}
    </>
  );
}

const emptyAlbum: AlbumEdit = {
  id: 0,
  title: '',
  year: new Date().getFullYear(),
  description: null,
  isPrivate: false,
  isExclusive: false,
  coverUrl: null,
  accessMembers: [],
};

const toInput = (a: AlbumEdit): AlbumInput => ({
  title: a.title,
  year: a.year === null ? '' : String(a.year),
  description: a.description ?? '',
  isPrivate: a.isPrivate,
  isExclusive: a.isExclusive,
  accessUserIds: a.accessMembers.map((m) => m.id),
});

export const normalize = (s: string) =>
  s
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .trim();

// ---------- song ----------

/** Create or edit a song: the same fields the retired page had (duration is kept as stored). */
export function SongForm({
  albumId,
  songId,
  onSaved,
  onClose,
}: {
  albumId: number;
  songId: number | null;
  onSaved: () => void;
  onClose: () => void;
}) {
  const [form, setForm] = useState<SongInput | null>(songId === null ? emptySong() : null);
  const [errors, setErrors] = useState<FieldErrors>({});
  const [banner, setBanner] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (songId === null) return;
    musicApi.songForEdit(songId).then((r) => {
      if (r.kind !== 'ok') {
        setBanner('Não foi possível abrir esta música para edição.');
        return;
      }
      const s = r.data;
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
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="submit" form="song-form" className="btn btn--primary" disabled={busy || !form}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            Guardar
          </button>
        </>
      }
    >
      {banner && (
        <p className="form__banner" role="alert">
          {banner}
        </p>
      )}
      {!form ? (
        !banner && <p className="note">A carregar…</p>
      ) : (
        <form id="song-form" className="form" onSubmit={submit} noValidate>
          <div className="form__row">
            <Field id="song-title" label="Título *" error={errors.title}>
              <input {...fieldProps('song-title', errors.title)} required maxLength={200} value={form.title} onChange={(e) => set('title', e.target.value)} />
            </Field>
            <Field id="song-track" label="Número da faixa" error={errors.trackNumber}>
              <input
                {...fieldProps('song-track', errors.trackNumber)}
                type="number"
                inputMode="numeric"
                min={1}
                max={999}
                value={form.trackNumber ?? ''}
                onChange={(e) => set('trackNumber', e.target.value === '' ? null : Number(e.target.value))}
              />
            </Field>
          </div>
          <div className="form__row">
            <Field id="song-lyric-author" label="Autor da letra" error={errors.lyricAuthor}>
              <input {...fieldProps('song-lyric-author', errors.lyricAuthor)} maxLength={200} value={form.lyricAuthor} onChange={(e) => set('lyricAuthor', e.target.value)} />
            </Field>
            <Field id="song-music-author" label="Autor da música" error={errors.musicAuthor}>
              <input {...fieldProps('song-music-author', errors.musicAuthor)} maxLength={200} value={form.musicAuthor} onChange={(e) => set('musicAuthor', e.target.value)} />
            </Field>
          </div>
          <div className="form__row">
            <Field id="song-adaptation" label="Adaptação" error={errors.adaptation}>
              <input {...fieldProps('song-adaptation', errors.adaptation)} maxLength={200} value={form.adaptation} onChange={(e) => set('adaptation', e.target.value)} />
            </Field>
            <Field id="song-spotify" label="Endereço no Spotify" error={errors.spotifyUrl}>
              <input
                {...fieldProps('song-spotify', errors.spotifyUrl)}
                type="url"
                inputMode="url"
                maxLength={500}
                placeholder="https://open.spotify.com/…"
                value={form.spotifyUrl}
                onChange={(e) => set('spotifyUrl', e.target.value)}
              />
            </Field>
          </div>
          <label className="form__check">
            <input type="checkbox" checked={form.hasAudio} onChange={(e) => set('hasAudio', e.target.checked)} />
            Tem ficheiro de áudio disponível
          </label>

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
                <button
                  type="button"
                  className="icon-btn icon-btn--sm"
                  onClick={() => set('youTubeUrls', form.youTubeUrls.filter((_, j) => j !== i))}
                  title="Remover"
                >
                  <Icon name="trash" />
                  <span className="sr-only">Remover endereço {i + 1}</span>
                </button>
              </div>
            ))}
            {form.youTubeUrls.length < 10 && (
              <button type="button" className="btn btn--ghost btn--sm" onClick={() => set('youTubeUrls', [...form.youTubeUrls, ''])}>
                <Icon name="plus" />
                Adicionar endereço
              </button>
            )}
            {errors.youTubeUrls && <p className="form__error">{errors.youTubeUrls}</p>}
          </fieldset>

          <Field id="song-lyrics" label="Letra" hint="Só quando a música não está no cancioneiro em PDF." error={errors.lyrics}>
            <textarea
              {...fieldProps('song-lyrics', errors.lyrics, 'hint')}
              rows={10}
              maxLength={10000}
              value={form.lyrics}
              onChange={(e) => set('lyrics', e.target.value)}
            />
          </Field>
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
