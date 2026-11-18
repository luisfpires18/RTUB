// Dialog to create or edit an album (cover, visibility, exclusive member list).
import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { Cropper } from './Cropper';
import { Icon } from './icons';
import { Dialog } from './MusicUi';
import { musicApi, type AlbumEdit, type AlbumInput, type FieldErrors, type Member } from '../data/musicApi';
import { failedMessage, normalize } from '../data/musicFormHelpers';
import { FormField, fieldProps } from './ui/FormField';

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
            <FormField id="album-title" label="Título *" error={errors.title}>
              <input {...fieldProps('album-title', errors.title)} required maxLength={200} value={form.title} onChange={(e) => set('title', e.target.value)} />
            </FormField>
            <FormField id="album-year" label="Ano" error={errors.year}>
              <input
                {...fieldProps('album-year', errors.year)}
                type="number"
                inputMode="numeric"
                min={1900}
                max={new Date().getFullYear()}
                value={form.year}
                onChange={(e) => set('year', e.target.value)}
              />
            </FormField>
            <FormField id="album-description" label="Descrição" error={errors.description}>
              <textarea
                {...fieldProps('album-description', errors.description)}
                rows={3}
                maxLength={1000}
                value={form.description}
                onChange={(e) => set('description', e.target.value)}
              />
            </FormField>
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
                    <FormField id="album-access" label="Adicionar membro">
                      <input id="album-access" type="search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Pesquisar por nome…" />
                    </FormField>
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
