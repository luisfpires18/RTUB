import { useEffect, useId, useState, type FormEvent } from 'react';
import { Loading } from './App';
import { Cropper } from './Cropper';
import { portal } from './content';
import { Dialog } from './Dialog';
import { call, type Outcome } from './eventsApi';
import { Icon } from './icons';
import { loginTo } from './musicApi';

// /api/inventory (Endpoints/InventoryEndpoints.cs, React track 020). Signed-in members only; Mod, Admin and Owner
// manage. The server decides every rule (InstrumentInventoryService); hiding a button here is only a convenience.

type Option = { value: string; label: string };
type Card = {
  id: number;
  name: string;
  category: string;
  categoryLabel: string;
  condition: string;
  conditionLabel: string;
  brand: string | null;
  location: string | null;
  thumbnailUrl: string | null;
};
type Detail = Omit<Card, 'thumbnailUrl'> & {
  serialNumber: string | null;
  lastMaintenanceDate: string | null;
  maintenanceNotes: string | null;
  imageUrl: string | null;
  thumbnailUrl: string | null;
};
type List = {
  total: number;
  stats: { condition: string; label: string; count: number }[];
  instruments: Card[];
  categories: Option[];
  conditions: Option[];
  canManage: boolean;
};
type Input = {
  name: string;
  category: string;
  condition: string;
  brand: string;
  serialNumber: string;
  location: string;
  lastMaintenanceDate: string | null;
  maintenanceNotes: string;
};

const query = (params: Record<string, string>) => {
  const p = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) if (v) p.set(k, v);
  const s = p.toString();
  return s ? `?${s}` : '';
};

const api = {
  list: (q: string, category: string, condition: string) => call<List>('GET', `/api/inventory${query({ q, category, condition })}`),
  get: (id: number) => call<Detail>('GET', `/api/inventory/${id}`),
  create: (input: Input) => call<{ id: number }>('POST', '/api/inventory', input),
  update: (id: number, input: Input) => call<Detail>('PUT', `/api/inventory/${id}`, input),
  remove: (id: number) => call<void>('DELETE', `/api/inventory/${id}`),
  image: (id: number, image: File, thumbnail: Blob) => {
    const form = new FormData();
    form.append('image', image, image.name);
    form.append('thumbnail', thumbnail, 'instrument-thumbnail.webp');
    return call<Detail>('POST', `/api/inventory/${id}/image`, form);
  },
};

const PAGE = 12;
const MAX_IMAGE = 10 * 1024 * 1024;
const IMAGE_TYPES = ['image/webp', 'image/jpeg', 'image/png'];
const CROP_TEXT = {
  title: 'Recortar a miniatura',
  label: 'Pré-visualização da miniatura quadrada. Arraste ou use as setas para enquadrar.',
  note: 'A imagem fica inteira nos detalhes; o recorte quadrado é a miniatura da lista.',
};

const problem = (o: Outcome<unknown>, what: string) =>
  o.kind === 'invalid'
    ? Object.values(o.errors)[0]
    : o.kind === 'forbidden'
      ? 'Só Mod, Admin ou Owner gerem os instrumentos.'
      : o.kind === 'notfound'
        ? 'Este instrumento já não existe.'
        : o.kind === 'signin'
          ? 'A sessão terminou. Entra outra vez.'
          : `Não foi possível ${what}. Tenta outra vez daqui a pouco.`;

const conditionClass = (c: string) => `inventory-condition inventory-condition--${c.toLowerCase()}`;

function Thumb({ url, size }: { url: string | null; size: number }) {
  return url ? (
    <img className="inventory-thumb" src={url} alt="" width={size} height={size} loading="lazy" />
  ) : (
    <span className="inventory-thumb inventory-thumb--empty" style={{ width: size, height: size }} aria-hidden="true">
      <Icon name="music" />
    </span>
  );
}

/**
 * /inventory - Instrumentos (React track 020; was the Blazor page). Signed-in members only. Every instrument of the
 * RTUB by name, with the old counters, search (name, type, brand) and type / condition filters; a card opens its
 * details. Mod, Admin and Owner add, edit (image included) and delete.
 */
export default function Inventory() {
  const [list, setList] = useState<List | 'signin' | null>();
  const [text, setText] = useState('');
  const [q, setQ] = useState('');
  const [category, setCategory] = useState('');
  const [condition, setCondition] = useState('');
  const [shown, setShown] = useState(PAGE);
  const [open, setOpen] = useState<number>();
  const [form, setForm] = useState<{ id?: number }>();
  const [deleting, setDeleting] = useState<Card>();
  const ids = { q: useId(), category: useId(), condition: useId() };

  useEffect(() => {
    document.title = 'Instrumentos · RTUB';
  }, []);

  useEffect(() => {
    const timer = setTimeout(() => setQ(text.trim()), 250);
    return () => clearTimeout(timer);
  }, [text]);

  const load = () => api.list(q, category, condition).then((o) => setList(o.kind === 'ok' ? o.data : o.kind === 'signin' ? 'signin' : null));

  useEffect(() => {
    setShown(PAGE);
    load();
  }, [q, category, condition]);

  const data = list && list !== 'signin' ? list : null;

  return (
    <section className="page wrap events-page inventory-page" aria-labelledby="inventory-title">
      <header className="page__head events-page__head">
        <div>
          <p className="eyebrow">Área de membros</p>
          <h1 id="inventory-title" className="page__title">
            Instrumentos
          </h1>
          <p className="page__lead">O material da tuna: onde está, de que marca é e em que estado.</p>
        </div>
        {data?.canManage && (
          <div className="events-page__actions">
            <button type="button" className="btn btn--primary btn--sm" onClick={() => setForm({})}>
              <Icon name="plus" />
              Adicionar instrumento
            </button>
          </div>
        )}
      </header>

      {list === 'signin' ? (
        <div className="notice" role="status">
          <p>O inventário é da área de membros.</p>
          <a className="btn btn--primary btn--sm" href={loginTo(portal.inventory)}>
            <Icon name="login" />
            Entrar
          </a>
        </div>
      ) : list === null ? (
        <div className="notice" role="status">
          <p>Não conseguimos abrir o inventário agora.</p>
          <button type="button" className="btn btn--ghost btn--sm" onClick={load}>
            Tentar novamente
          </button>
        </div>
      ) : !data ? (
        <Loading label="A carregar os instrumentos…" />
      ) : (
        <>
          {data.total > 0 && (
            <dl className="inventory-stats">
              <div>
                <dt>Total</dt>
                <dd>{data.total}</dd>
              </div>
              {data.stats.map((s) => (
                <div key={s.condition}>
                  <dt>{s.label}</dt>
                  <dd>{s.count}</dd>
                </div>
              ))}
            </dl>
          )}

          <div className="events-tools" role="search">
            <label className="control" htmlFor={ids.q}>
              <span className="sr-only">Procurar instrumento</span>
              <Icon name="search" />
              <input id={ids.q} type="search" placeholder="Procurar por nome, tipo ou marca" value={text} onChange={(e) => setText(e.target.value)} />
            </label>
            <label className="control control--select" htmlFor={ids.category}>
              <span className="sr-only">Tipo</span>
              <select id={ids.category} value={category} onChange={(e) => setCategory(e.target.value)}>
                <option value="">Todos os tipos</option>
                {data.categories.map((c) => (
                  <option key={c.value} value={c.value}>
                    {c.label}
                  </option>
                ))}
              </select>
            </label>
            <label className="control control--select" htmlFor={ids.condition}>
              <span className="sr-only">Condição</span>
              <select id={ids.condition} value={condition} onChange={(e) => setCondition(e.target.value)}>
                <option value="">Todas as condições</option>
                {data.conditions.map((c) => (
                  <option key={c.value} value={c.value}>
                    {c.label}
                  </option>
                ))}
              </select>
            </label>
          </div>

          {data.total === 0 ? (
            <p className="note">Ainda não há instrumentos registados.</p>
          ) : data.instruments.length === 0 ? (
            <p className="note">Nenhum instrumento corresponde à pesquisa.</p>
          ) : (
            <>
              <ul className="inventory-grid">
                {data.instruments.slice(0, shown).map((i) => (
                  <li key={i.id} className="inventory-card">
                    <button type="button" className="inventory-card__open" onClick={() => setOpen(i.id)}>
                      <Thumb url={i.thumbnailUrl} size={96} />
                      <strong className="inventory-card__name">{i.name}</strong>
                      <span className="member-badges">
                        <span className="member-badge member-badge--position">{i.categoryLabel}</span>
                        <span className={conditionClass(i.condition)}>{i.conditionLabel}</span>
                      </span>
                      {i.brand && <small>Marca: {i.brand}</small>}
                      {i.location && <small>Local: {i.location}</small>}
                    </button>
                    {data.canManage && (
                      <span className="inventory-card__tools">
                        <button type="button" className="icon-btn" onClick={() => setForm({ id: i.id })}>
                          <Icon name="pencil" />
                          <span className="sr-only">Editar {i.name}</span>
                        </button>
                        <button type="button" className="icon-btn icon-btn--danger" onClick={() => setDeleting(i)}>
                          <Icon name="trash" />
                          <span className="sr-only">Eliminar {i.name}</span>
                        </button>
                      </span>
                    )}
                  </li>
                ))}
              </ul>
              {data.instruments.length > shown && (
                <button type="button" className="btn btn--ghost btn--sm members-more" onClick={() => setShown(shown + PAGE)}>
                  Mostrar mais ({data.instruments.length - shown})
                </button>
              )}
            </>
          )}

          {open !== undefined && <DetailDialog id={open} onClose={() => setOpen(undefined)} />}
          {form && (
            <FormDialog
              id={form.id}
              categories={data.categories}
              conditions={data.conditions}
              onClose={() => setForm(undefined)}
              onSaved={load}
            />
          )}
          {deleting && <DeleteDialog instrument={deleting} onClose={() => setDeleting(undefined)} onDone={load} />}
        </>
      )}
    </section>
  );
}

/** "Detalhes do Instrumento": every field the old modal showed any member. */
function DetailDialog({ id, onClose }: { id: number; onClose: () => void }) {
  const [detail, setDetail] = useState<Detail | 'missing' | null>();

  useEffect(() => {
    api.get(id).then((o) => setDetail(o.kind === 'ok' ? o.data : o.kind === 'notfound' ? 'missing' : null));
  }, [id]);

  const field = (label: string, value: string | null) => (
    <div className="member-field">
      <dt>{label}</dt>
      <dd>{value ?? <span className="note">Não definido</span>}</dd>
    </div>
  );

  return (
    <Dialog title="Detalhes do instrumento" size="lg" onClose={onClose}>
      {detail === undefined ? (
        <Loading label="A carregar…" />
      ) : detail === 'missing' ? (
        <p className="form__banner" role="alert">
          Este instrumento já não existe.
        </p>
      ) : detail === null ? (
        <p className="form__banner" role="alert">
          Não foi possível carregar o instrumento.
        </p>
      ) : (
        <div className="member-detail">
          <div className="member-detail__head">
            {detail.imageUrl ? <img className="inventory-image" src={detail.imageUrl} alt="" /> : <Thumb url={null} size={96} />}
            <p className="member-detail__name">{detail.name}</p>
            <span className="member-badges">
              <span className="member-badge member-badge--position">{detail.categoryLabel}</span>
              <span className={conditionClass(detail.condition)}>{detail.conditionLabel}</span>
            </span>
          </div>
          <section className="member-section" aria-label="Informações do instrumento">
            <h3 className="member-section__title">Informações do instrumento</h3>
            <dl className="member-fields">
              {field('Tipo', detail.categoryLabel)}
              {field('Marca', detail.brand)}
              {field('Número de série', detail.serialNumber)}
              {field('Localização', detail.location)}
              {field('Condição', detail.conditionLabel)}
              {field('Última manutenção', detail.lastMaintenanceDate && detail.lastMaintenanceDate.split('-').reverse().join('/'))}
            </dl>
          </section>
          {detail.maintenanceNotes && (
            <section className="member-section" aria-label="Notas de manutenção">
              <h3 className="member-section__title">Notas de manutenção</h3>
              <p className="inventory-notes">{detail.maintenanceNotes}</p>
            </section>
          )}
        </div>
      )}
    </Dialog>
  );
}

const blank = (): Input => ({
  name: '',
  category: '',
  condition: 'Good',
  brand: '',
  serialNumber: '',
  location: '',
  lastMaintenanceDate: null,
  maintenanceNotes: '',
});

/** "Novo instrumento" / "Editar instrumento": the old fields; the type is chosen once, when the instrument is added. */
function FormDialog({
  id,
  categories,
  conditions,
  onClose,
  onSaved,
}: {
  id?: number;
  categories: Option[];
  conditions: Option[];
  onClose: () => void;
  onSaved: () => void;
}) {
  const [form, setForm] = useState<Input | 'failed' | undefined>(id === undefined ? blank() : undefined);
  const [savedId, setSavedId] = useState(id);
  const [currentImage, setCurrentImage] = useState<string | null>(null);
  const [picked, setPicked] = useState<File | null>(null);
  const [image, setImage] = useState<{ file: File; thumbnail: Blob; preview: string } | null>(null);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [banner, setBanner] = useState<string>();
  const [busy, setBusy] = useState(false);
  const fid = useId();

  useEffect(() => {
    if (id === undefined) return;
    api.get(id).then((o) => {
      if (o.kind !== 'ok') return setForm('failed');
      const d = o.data;
      setForm({
        name: d.name,
        category: d.category,
        condition: d.condition,
        brand: d.brand ?? '',
        serialNumber: d.serialNumber ?? '',
        location: d.location ?? '',
        lastMaintenanceDate: d.lastMaintenanceDate,
        maintenanceNotes: d.maintenanceNotes ?? '',
      });
      setCurrentImage(d.thumbnailUrl ?? d.imageUrl);
    });
  }, [id]);

  if (form === undefined || form === 'failed') {
    return (
      <Dialog title="Editar instrumento" onClose={onClose}>
        {form === undefined ? <Loading label="A carregar…" /> : <p className="form__banner">Não foi possível abrir este instrumento.</p>}
      </Dialog>
    );
  }

  const set = (next: Partial<Input>) => setForm({ ...form, ...next });

  const pick = (file: File | undefined) => {
    if (!file) return;
    if (!IMAGE_TYPES.includes(file.type)) return setErrors({ ...errors, image: 'A imagem tem de ser WebP, JPEG ou PNG.' });
    if (file.size > MAX_IMAGE) return setErrors({ ...errors, image: 'A imagem não pode exceder 10 MB.' });
    const { image: _, ...rest } = errors;
    setErrors(rest);
    setPicked(file);
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setBanner(undefined);
    const saved = savedId === undefined ? await api.create(form) : await api.update(savedId, form);
    if (saved.kind !== 'ok') {
      setBusy(false);
      if (saved.kind === 'invalid') return setErrors(saved.errors);
      return setBanner(problem(saved, 'guardar'));
    }
    const newId = savedId ?? (saved.data as { id: number }).id;
    setSavedId(newId);
    onSaved();
    if (image) {
      const uploaded = await api.image(newId, image.file, image.thumbnail);
      setBusy(false);
      if (uploaded.kind !== 'ok') {
        setErrors(uploaded.kind === 'invalid' ? { image: Object.values(uploaded.errors)[0] } : {});
        return setBanner(`O instrumento foi guardado, mas a imagem não. ${uploaded.kind === 'invalid' ? '' : problem(uploaded, 'guardar a imagem')}`.trim());
      }
      onSaved();
    }
    setBusy(false);
    onClose();
  };

  const field = (key: string) => (errors[key] ? 'form__field form__field--error' : 'form__field');
  const error = (key: string) =>
    errors[key] && (
      <p className="form__error" role="alert">
        {errors[key]}
      </p>
    );
  const text = (key: 'name' | 'brand' | 'serialNumber' | 'location', label: string, max: number) => (
    <div className={field(key)}>
      <label htmlFor={`${fid}-${key}`}>{label}</label>
      <input id={`${fid}-${key}`} type="text" maxLength={max} value={form[key]} onChange={(e) => set({ [key]: e.target.value })} />
      {error(key)}
    </div>
  );

  return (
    <>
      <Dialog
        title={savedId === undefined ? 'Novo instrumento' : 'Editar instrumento'}
        size="lg"
        onClose={onClose}
        footer={
          <>
            <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
              Cancelar
            </button>
            <button type="submit" form={`${fid}-form`} className="btn btn--primary" disabled={busy}>
              {busy && <span className="spinner spinner--small" aria-hidden="true" />}
              Guardar
            </button>
          </>
        }
      >
        <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
          {banner && (
            <p className="form__banner" role="alert">
              {banner}
            </p>
          )}
          <div className="form__row">
            {text('name', 'Nome', 100)}
            <div className={field('category')}>
              <label htmlFor={`${fid}-category`}>Tipo</label>
              <span className="control control--select">
                <select id={`${fid}-category`} value={form.category} disabled={savedId !== undefined} onChange={(e) => set({ category: e.target.value })}>
                  <option value="">Escolher…</option>
                  {categories.map((c) => (
                    <option key={c.value} value={c.value}>
                      {c.label}
                    </option>
                  ))}
                  {form.category && !categories.some((c) => c.value === form.category) && <option value={form.category}>{form.category}</option>}
                </select>
              </span>
              {savedId !== undefined && <p className="form__hint">O tipo fica como foi escolhido ao adicionar.</p>}
              {error('category')}
            </div>
          </div>
          <div className="form__row">
            {text('brand', 'Marca', 100)}
            {text('serialNumber', 'Número de série', 100)}
          </div>
          <div className="form__row">
            <div className={field('condition')}>
              <label htmlFor={`${fid}-condition`}>Condição</label>
              <span className="control control--select">
                <select id={`${fid}-condition`} value={form.condition} onChange={(e) => set({ condition: e.target.value })}>
                  {conditions.map((c) => (
                    <option key={c.value} value={c.value}>
                      {c.label}
                    </option>
                  ))}
                </select>
              </span>
              {error('condition')}
            </div>
            {text('location', 'Localização', 200)}
          </div>
          <div className="form__row">
            <div className={field('lastMaintenanceDate')}>
              <label htmlFor={`${fid}-date`}>Última manutenção</label>
              <input id={`${fid}-date`} type="date" value={form.lastMaintenanceDate ?? ''} onChange={(e) => set({ lastMaintenanceDate: e.target.value || null })} />
              {error('lastMaintenanceDate')}
            </div>
            <div className={field('maintenanceNotes')}>
              <label htmlFor={`${fid}-notes`}>Notas de manutenção</label>
              <textarea id={`${fid}-notes`} rows={3} maxLength={500} value={form.maintenanceNotes} onChange={(e) => set({ maintenanceNotes: e.target.value })} />
              {error('maintenanceNotes')}
            </div>
          </div>
          <div className={field('image')}>
            <span className="form__label">Imagem</span>
            <span className="inventory-image-pick">
              {image ? (
                <img className="inventory-thumb" src={image.preview} alt="" width={72} height={72} />
              ) : (
                <Thumb url={currentImage} size={72} />
              )}
              <label className="btn btn--ghost btn--sm" htmlFor={`${fid}-file`}>
                <Icon name="upload" />
                {image || currentImage ? 'Trocar imagem' : 'Escolher imagem'}
              </label>
              <input
                id={`${fid}-file`}
                className="sr-only"
                type="file"
                accept={IMAGE_TYPES.join(',')}
                onChange={(e) => {
                  pick(e.target.files?.[0]);
                  e.target.value = '';
                }}
              />
            </span>
            <p className="form__hint">WebP, JPEG ou PNG até 10 MB. Depois recortas a miniatura quadrada.</p>
            {error('image')}
          </div>
        </form>
      </Dialog>
      {picked && (
        <Cropper
          file={picked}
          text={CROP_TEXT}
          outWidth={600}
          onCancel={() => setPicked(null)}
          onDone={(thumbnail, preview) => {
            setImage({ file: picked, thumbnail, preview });
            setPicked(null);
          }}
        />
      )}
    </>
  );
}

function DeleteDialog({ instrument, onClose, onDone }: { instrument: Card; onClose: () => void; onDone: () => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();

  const remove = async () => {
    setBusy(true);
    const o = await api.remove(instrument.id);
    setBusy(false);
    if (o.kind === 'ok' || o.kind === 'notfound') {
      onDone();
      onClose();
    } else setError(problem(o, 'eliminar'));
  };

  return (
    <Dialog
      title="Eliminar instrumento"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="button" className="btn btn--danger" onClick={remove} disabled={busy}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            Eliminar
          </button>
        </>
      }
    >
      <p>
        Eliminar <strong>{instrument.name}</strong> do inventário?
      </p>
      <p className="warning">
        <Icon name="warning" />
        Apaga também a imagem. Não dá para desfazer.
      </p>
      {error && (
        <p className="form__banner" role="alert">
          {error}
        </p>
      )}
    </Dialog>
  );
}
