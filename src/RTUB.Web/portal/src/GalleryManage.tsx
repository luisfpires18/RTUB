import { useEffect, useId, useState, type FormEvent } from 'react';
import { Loading } from './App';
import type { GalleryItem, GalleryPerson } from './api';
import { Dialog } from './Dialog';
import { antiforgeryToken, call, type Outcome } from './eventsApi';
import { Icon } from './icons';

// Gallery management (React track 015; was the Blazor /member/gallery). Any signed-in member uploads; the
// uploader, Admin or Owner edit and delete. The server decides and validates everything (GalleryManagementService).

const MAX_IMAGE = 10 * 1024 * 1024;
const MAX_VIDEO = 100 * 1024 * 1024;

type Taggable = { id: string; name: string; fullName: string | null; avatarUrl: string };
type EditData = { id: number; title: string; type: 'image' | 'video'; url: string | null; date: string; membersOnly: boolean; people: GalleryPerson[] };
type Form = { title: string; date: string; membersOnly: boolean; people: GalleryPerson[] };

const galleryApi = {
  people: (q: string) => call<Taggable[]>('GET', `/api/gallery/people?q=${encodeURIComponent(q)}`),
  forEdit: (id: number) => call<EditData>('GET', `/api/gallery/items/${id}/edit`),
  update: (id: number, form: Form) =>
    call<GalleryItem>('PUT', `/api/gallery/items/${id}`, { title: form.title, date: form.date, membersOnly: form.membersOnly, personIds: form.people.map((p) => p.id) }),
  remove: (id: number) => call<void>('DELETE', `/api/gallery/items/${id}`),
};

const today = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
};

const problem = (o: Outcome<unknown>, what: string) =>
  o.kind === 'forbidden'
    ? 'Só quem carregou, Admin ou Owner podem alterar esta media.'
    : o.kind === 'notfound'
      ? 'Esta media já não existe.'
      : o.kind === 'signin'
        ? 'A sessão terminou. Entra outra vez.'
        : `Não foi possível ${what}. Tenta outra vez daqui a pouco.`;

type UploadOutcome = { kind: 'ok' } | { kind: 'invalid'; errors: Record<string, string> } | { kind: 'failed'; message: string };

/** POST /api/gallery as multipart, through XHR so the upload can report its progress. */
async function upload(file: File, form: Form, onProgress: (share: number) => void, retried = false): Promise<UploadOutcome> {
  const body = new FormData();
  body.set('file', file);
  body.set('title', form.title);
  body.set('date', form.date);
  body.set('membersOnly', String(form.membersOnly));
  form.people.forEach((p) => body.append('personIds', p.id));
  let token: string;
  try {
    token = await antiforgeryToken(retried);
  } catch {
    return { kind: 'failed', message: problem({ kind: 'failed' }, 'carregar') };
  }

  const answer = await new Promise<{ status: number; text: string }>((resolve) => {
    const xhr = new XMLHttpRequest();
    xhr.open('POST', '/api/gallery');
    xhr.setRequestHeader('X-CSRF-TOKEN', token);
    xhr.setRequestHeader('Accept', 'application/json');
    xhr.upload.onprogress = (e) => e.lengthComputable && onProgress(e.loaded / e.total);
    xhr.onload = () => resolve({ status: xhr.status, text: xhr.responseText });
    xhr.onerror = () => resolve({ status: 0, text: '' });
    xhr.send(body);
  });

  if (answer.status >= 200 && answer.status < 300) return { kind: 'ok' };
  const json = (() => {
    try {
      return JSON.parse(answer.text) as { errors?: Record<string, string[]> };
    } catch {
      return {};
    }
  })();
  if (answer.status === 400 && json.errors) {
    const errors: Record<string, string> = {};
    for (const [k, v] of Object.entries(json.errors)) errors[k.charAt(0).toLowerCase() + k.slice(1)] = v[0];
    return { kind: 'invalid', errors };
  }
  if (answer.status === 400 && !retried) return upload(file, form, onProgress, true); // a refused token: fetch a fresh one
  if (answer.status === 413) return { kind: 'failed', message: 'O ficheiro é demasiado grande.' };
  if (answer.status === 401) return { kind: 'failed', message: problem({ kind: 'signin' }, 'carregar') };
  return { kind: 'failed', message: problem({ kind: 'failed' }, 'carregar') };
}

/** Who appears: chosen chips plus a search over members (not expelled). */
function PeoplePicker({ people, onChange }: { people: GalleryPerson[]; onChange: (people: GalleryPerson[]) => void }) {
  const [query, setQuery] = useState('');
  const [found, setFound] = useState<Taggable[]>();
  const id = useId();

  useEffect(() => {
    if (!query.trim()) return setFound(undefined);
    const timer = setTimeout(() => {
      galleryApi.people(query.trim()).then((o) => setFound(o.kind === 'ok' ? o.data.filter((m) => !people.some((p) => p.id === m.id)) : []));
    }, 250);
    return () => clearTimeout(timer);
  }, [query, people]);

  return (
    <div className="form__field">
      <label htmlFor={id}>Quem aparece</label>
      {people.length > 0 && (
        <ul className="chips" aria-label="Pessoas marcadas">
          {people.map((p) => (
            <li key={p.id} className="chip chip--removable">
              {p.name}
              <button type="button" className="chip__remove" onClick={() => onChange(people.filter((x) => x.id !== p.id))} aria-label={`Tirar ${p.name}`}>
                <Icon name="close" />
              </button>
            </li>
          ))}
        </ul>
      )}
      <span className="control control--sm">
        <Icon name="search" />
        <input id={id} type="search" placeholder="Procurar por alcunha ou nome" value={query} onChange={(e) => setQuery(e.target.value)} />
      </span>
      {found && found.length === 0 && <p className="note">Ninguém encontrado.</p>}
      {found && found.length > 0 && (
        <ul className="talk__picks">
          {found.map((m) => (
            <li key={m.id}>
              <button
                type="button"
                onClick={() => {
                  onChange([...people, { id: m.id, name: m.name }]);
                  setQuery('');
                }}
              >
                <img src={m.avatarUrl} alt="" width="28" height="28" loading="lazy" />
                <span>
                  {m.name}
                  {m.fullName && m.fullName !== m.name && <small>{m.fullName}</small>}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function Details({ form, set, errors, idPrefix }: { form: Form; set: (f: Partial<Form>) => void; errors: Record<string, string>; idPrefix: string }) {
  return (
    <>
      <div className={errors.title ? 'form__field form__field--error' : 'form__field'}>
        <label htmlFor={`${idPrefix}-title`}>Título</label>
        <input id={`${idPrefix}-title`} maxLength={200} value={form.title} onChange={(e) => set({ title: e.target.value })} />
        {errors.title && <p className="form__error">{errors.title}</p>}
      </div>
      <div className={errors.date ? 'form__field form__field--error' : 'form__field'}>
        <label htmlFor={`${idPrefix}-date`}>Data</label>
        <input id={`${idPrefix}-date`} type="date" value={form.date} onChange={(e) => set({ date: e.target.value })} />
        <p className="form__hint">Sem a data exata? Escolhe o dia 1 do mês (guarda só o mês) ou o 1 de janeiro (guarda só o ano).</p>
        {errors.date && <p className="form__error">{errors.date}</p>}
      </div>
      <label className="check">
        <input type="checkbox" checked={form.membersOnly} onChange={(e) => set({ membersOnly: e.target.checked })} />
        <Icon name="lock" />
        Só para membros
      </label>
      <p className="form__hint">
        {form.membersOnly ? 'Só membros com sessão iniciada a veem na galeria.' : 'Qualquer visitante a vê na galeria pública.'}
      </p>
    </>
  );
}

/** Upload one photo or video; the people tagged get a push notification, as before. */
export function UploadDialog({ onClose, onDone }: { onClose: () => void; onDone: () => void }) {
  const [file, setFile] = useState<File>();
  const [preview, setPreview] = useState<string>();
  const [form, setForm] = useState<Form>({ title: '', date: today(), membersOnly: true, people: [] });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [progress, setProgress] = useState<number>();
  const id = useId();
  const set = (next: Partial<Form>) => setForm((f) => ({ ...f, ...next }));

  useEffect(() => () => void (preview && URL.revokeObjectURL(preview)), [preview]);

  const pick = (f?: File) => {
    if (!f) return;
    const video = f.type.startsWith('video/');
    if (!video && !f.type.startsWith('image/')) return setErrors({ file: 'Só dá para carregar fotos e vídeos.' });
    if (f.size > (video ? MAX_VIDEO : MAX_IMAGE)) return setErrors({ file: `O tamanho do ficheiro deve ser inferior a ${video ? 100 : 10} MB.` });
    setErrors({});
    setFile(f);
    setPreview(URL.createObjectURL(f));
    if (!form.title) set({ title: f.name.replace(/\.[^.]+$/, '').slice(0, 200) });
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!file) return setErrors({ file: 'Escolhe uma foto ou um vídeo.' });
    setProgress(0);
    const o = await upload(file, form, setProgress);
    setProgress(undefined);
    if (o.kind === 'ok') {
      onDone();
      onClose();
    } else setErrors(o.kind === 'invalid' ? o.errors : { form: o.message });
  };

  const busy = progress !== undefined;
  return (
    <Dialog
      title="Carregar para a galeria"
      onClose={busy ? () => undefined : onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="submit" form={`${id}-form`} className="btn btn--primary" disabled={busy || !file}>
            <Icon name="upload" />
            {busy ? `A carregar… ${Math.round((progress ?? 0) * 100)}%` : 'Carregar'}
          </button>
        </>
      }
    >
      <form id={`${id}-form`} className="form" onSubmit={submit} noValidate>
        <div className={errors.file ? 'form__field form__field--error' : 'form__field'}>
          <label htmlFor={`${id}-file`}>Foto ou vídeo</label>
          <input id={`${id}-file`} type="file" accept="image/*,video/*" onChange={(e) => pick(e.target.files?.[0])} disabled={busy} />
          <p className="form__hint">Imagens até 10 MB, vídeos até 100 MB.</p>
          {errors.file && <p className="form__error">{errors.file}</p>}
        </div>
        {file && preview && (
          <div className="gallery-manage__preview">
            {file.type.startsWith('video/') ? <video src={preview} controls playsInline preload="metadata" /> : <img src={preview} alt="" />}
          </div>
        )}
        <Details form={form} set={set} errors={errors} idPrefix={id} />
        <PeoplePicker people={form.people} onChange={(people) => set({ people })} />
        {errors.personIds && <p className="form__error">{errors.personIds}</p>}
        {form.people.length > 0 && (
          <p className="note">
            <Icon name="bell" /> {form.people.length === 1 ? 'A pessoa marcada recebe' : `As ${form.people.length} pessoas marcadas recebem`} uma notificação.
          </p>
        )}
        {busy && <progress className="gallery-manage__progress" max={1} value={progress} aria-label="Progresso do carregamento" />}
        {errors.form && <p className="form__error">{errors.form}</p>}
      </form>
    </Dialog>
  );
}

/** Edit title, date, members-only and who appears (no notification); delete behind a confirm. */
export function EditDialog({ itemId, onClose, onDone }: { itemId: number; onClose: () => void; onDone: () => void }) {
  const [data, setData] = useState<EditData | null>();
  const [form, setForm] = useState<Form>();
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [confirming, setConfirming] = useState(false);
  const [busy, setBusy] = useState(false);
  const id = useId();

  useEffect(() => {
    galleryApi.forEdit(itemId).then((o) => {
      if (o.kind !== 'ok') return setData(null);
      setData(o.data);
      setForm({ title: o.data.title, date: o.data.date, membersOnly: o.data.membersOnly, people: o.data.people });
    });
  }, [itemId]);

  const set = (next: Partial<Form>) => setForm((f) => (f ? { ...f, ...next } : f));

  const save = async (e: FormEvent) => {
    e.preventDefault();
    if (!form) return;
    setBusy(true);
    const o = await galleryApi.update(itemId, form);
    setBusy(false);
    if (o.kind !== 'ok') return setErrors(o.kind === 'invalid' ? o.errors : { form: problem(o, 'guardar') });
    onDone();
    onClose();
  };

  const remove = async () => {
    setBusy(true);
    const o = await galleryApi.remove(itemId);
    setBusy(false);
    setConfirming(false);
    if (o.kind !== 'ok') return setErrors({ form: problem(o, 'apagar') });
    onDone();
    onClose();
  };

  return (
    <Dialog
      title="Editar media"
      onClose={onClose}
      footer={
        form &&
        (confirming ? (
          <>
            <span>Apagar de vez? O ficheiro também é apagado.</span>
            <button type="button" className="btn btn--ghost" onClick={() => setConfirming(false)}>
              Não
            </button>
            <button type="button" className="btn btn--danger" disabled={busy} onClick={remove}>
              Apagar
            </button>
          </>
        ) : (
          <>
            <button type="button" className="btn btn--ghost gallery-manage__delete" onClick={() => setConfirming(true)}>
              <Icon name="trash" />
              Apagar
            </button>
            <button type="button" className="btn btn--ghost" onClick={onClose}>
              Cancelar
            </button>
            <button type="submit" form={`${id}-form`} className="btn btn--primary" disabled={busy}>
              Guardar
            </button>
          </>
        ))
      }
    >
      {data === undefined ? (
        <Loading label="A carregar…" />
      ) : data === null || !form ? (
        <p>Não foi possível abrir esta media para edição.</p>
      ) : (
        <form id={`${id}-form`} className="form" onSubmit={save} noValidate>
          {data.url && (
            <div className="gallery-manage__preview">
              {data.type === 'video' ? <video src={data.url} controls playsInline preload="metadata" /> : <img src={data.url} alt="" />}
            </div>
          )}
          <Details form={form} set={set} errors={errors} idPrefix={id} />
          <PeoplePicker people={form.people} onChange={(people) => set({ people })} />
          {errors.personIds && <p className="form__error">{errors.personIds}</p>}
          <p className="form__hint">Mudar quem aparece não envia notificações.</p>
          {errors.form && <p className="form__error">{errors.form}</p>}
        </form>
      )}
    </Dialog>
  );
}
