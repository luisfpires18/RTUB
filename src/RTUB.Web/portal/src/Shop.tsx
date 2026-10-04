import { useEffect, useId, useState, type FormEvent, type ReactNode } from 'react';
import { Loading } from './App';
import { portal } from './content';
import { Cropper } from './Cropper';
import { Dialog } from './Dialog';
import { call, type Outcome } from './eventsApi';
import { Icon } from './icons';
import { loginTo } from './musicApi';

// /api/shop (Endpoints/ShopEndpoints.cs, React track 021). Signed-in members only; Mod adds products, Admin and Owner
// manage them and every reservation. The server decides every rule (ProductShopService).

type Option = { value: string; label: string };
type Reservation = { id: number; username: string; displayName: string | null; size: string | null; createdAt: string };
type Product = {
  id: number;
  name: string;
  type: string;
  price: number;
  stock: number;
  isPublic: boolean;
  imageUrl: string | null;
  myReservation: Reservation | null;
  canReserve: boolean;
};
type Detail = {
  id: number;
  name: string;
  type: string;
  price: number;
  stock: number;
  isAvailable: boolean;
  isPublic: boolean;
  description: string | null;
  imageUrl: string | null;
};
type Shop = {
  fiscalYears: Option[];
  fiscalYear: string;
  types: string[];
  products: Product[];
  sizes: string[];
  canCreate: boolean;
  canManage: boolean;
};
type ProductInput = { name: string; type: string; price: number; stock: number; isPublic: boolean; description: string };

const SIZE_LABELS: Record<string, string> = {
  XS: 'XS - Extra Small',
  S: 'S - Small',
  M: 'M - Medium',
  L: 'L - Large',
  XL: 'XL - Extra Large',
  XXL: 'XXL - 2X Large',
  XXXL: 'XXXL - 3X Large',
};

const api = {
  // fiscalYear: null = the server's default (this year); '' = every year.
  shop: (fiscalYear: string | null, q: string, type: string) => {
    const p = new URLSearchParams();
    if (fiscalYear !== null) p.set('fiscalYear', fiscalYear);
    if (q) p.set('q', q);
    if (type) p.set('type', type);
    const s = p.toString();
    return call<Shop>('GET', `/api/shop${s ? `?${s}` : ''}`);
  },
  product: (id: number) => call<Detail>('GET', `/api/shop/products/${id}`),
  create: (input: ProductInput) => call<{ id: number }>('POST', '/api/shop/products', input),
  update: (id: number, input: ProductInput) => call<Detail>('PUT', `/api/shop/products/${id}`, input),
  remove: (id: number) => call<void>('DELETE', `/api/shop/products/${id}`),
  image: (id: number, image: Blob) => {
    const form = new FormData();
    form.append('image', image, 'product-image.webp');
    return call<Detail>('POST', `/api/shop/products/${id}/image`, form);
  },
  reserve: (id: number, body: { hasSizes: boolean; size: string | null; displayName: string | null }) =>
    call<Reservation>('POST', `/api/shop/products/${id}/reservations`, body),
  reservations: (id: number) => call<Reservation[]>('GET', `/api/shop/products/${id}/reservations`),
  cancel: (id: number) => call<void>('DELETE', `/api/shop/reservations/${id}`),
};

const PAGE = 12;
const MAX_SOURCE = 10 * 1024 * 1024;
const CROP_TEXT = {
  title: 'Recortar a imagem',
  label: 'Pré-visualização da imagem quadrada do produto. Arraste ou use as setas para enquadrar.',
  note: 'Os produtos aparecem sempre em quadrado.',
};

const euros = (n: number) => `${n.toLocaleString('pt-PT', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} €`;
const when = (iso: string) => {
  const d = new Date(/[zZ]|[+-]\d\d:?\d\d$/.test(iso) ? iso : `${iso}Z`);
  return `${d.toLocaleDateString('pt-PT', { day: '2-digit', month: '2-digit', year: 'numeric' })} ${d.toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit' })}`;
};

const problem = (o: Outcome<unknown>, what: string) =>
  o.kind === 'invalid'
    ? Object.values(o.errors)[0]
    : o.kind === 'forbidden'
      ? 'Não tem permissão para esta ação.'
      : o.kind === 'notfound'
        ? 'Já não existe.'
        : o.kind === 'signin'
          ? 'A sessão terminou. Entra outra vez.'
          : `Não foi possível ${what}. Tenta outra vez daqui a pouco.`;

type Open =
  | { kind: 'detail' | 'reserve' | 'mine' | 'reservations' | 'delete'; product: Product }
  | { kind: 'form'; id?: number };

/**
 * /shop - Loja RTUB (React track 021; was the Blazor page). Signed-in members only. The products of the fiscal year
 * (this one by default), by type then name, with stock and price; members reserve members-only products in stock and
 * cancel their own reservation. Mod adds products; Admin and Owner edit, delete and see every reservation.
 */
export default function ShopPage() {
  const [shop, setShop] = useState<Shop | 'signin' | null>();
  const [fiscalYear, setFiscalYear] = useState<string | null>(null);
  const [text, setText] = useState('');
  const [q, setQ] = useState('');
  const [type, setType] = useState('');
  const [shown, setShown] = useState(PAGE);
  const [open, setOpen] = useState<Open>();
  const ids = { q: useId(), type: useId(), year: useId() };

  useEffect(() => {
    document.title = 'Loja · RTUB';
  }, []);

  useEffect(() => {
    const timer = setTimeout(() => setQ(text.trim()), 250);
    return () => clearTimeout(timer);
  }, [text]);

  const load = () => api.shop(fiscalYear, q, type).then((o) => setShop(o.kind === 'ok' ? o.data : o.kind === 'signin' ? 'signin' : null));

  useEffect(() => {
    setShown(PAGE);
    load();
  }, [fiscalYear, q, type]);

  const data = shop && shop !== 'signin' ? shop : null;
  const close = () => setOpen(undefined);

  return (
    <section className="page wrap events-page shop-page" aria-labelledby="shop-title">
      <header className="page__head events-page__head">
        <div>
          <p className="eyebrow">Área de membros</p>
          <h1 id="shop-title" className="page__title">
            Loja RTUB
          </h1>
          <p className="page__lead">Os produtos da tuna, com o preço e o que ainda há em stock.</p>
        </div>
        {data?.canCreate && (
          <div className="events-page__actions">
            <button type="button" className="btn btn--primary btn--sm" onClick={() => setOpen({ kind: 'form' })}>
              <Icon name="plus" />
              Adicionar produto
            </button>
          </div>
        )}
      </header>

      {shop === 'signin' ? (
        <div className="notice" role="status">
          <p>A loja é da área de membros.</p>
          <a className="btn btn--primary btn--sm" href={loginTo(portal.shop)}>
            <Icon name="login" />
            Entrar
          </a>
        </div>
      ) : shop === null ? (
        <div className="notice" role="status">
          <p>Não conseguimos abrir a loja agora.</p>
          <button type="button" className="btn btn--ghost btn--sm" onClick={load}>
            Tentar novamente
          </button>
        </div>
      ) : !data ? (
        <Loading label="A carregar os produtos…" />
      ) : (
        <>
          <div className="events-tools" role="search">
            <label className="control" htmlFor={ids.q}>
              <span className="sr-only">Procurar produto</span>
              <Icon name="search" />
              <input id={ids.q} type="search" placeholder="Procurar por nome ou tipo" value={text} onChange={(e) => setText(e.target.value)} />
            </label>
            <label className="control control--select" htmlFor={ids.type}>
              <span className="sr-only">Tipo</span>
              <select id={ids.type} value={type} onChange={(e) => setType(e.target.value)}>
                <option value="">Todos os tipos</option>
                {data.types.map((t) => (
                  <option key={t} value={t}>
                    {t}
                  </option>
                ))}
              </select>
            </label>
            <label className="control control--select" htmlFor={ids.year}>
              <span className="sr-only">Ano letivo</span>
              <select id={ids.year} value={data.fiscalYear} onChange={(e) => setFiscalYear(e.target.value)}>
                <option value="">Todos os anos</option>
                {data.fiscalYear && !data.fiscalYears.some((y) => y.value === data.fiscalYear) && (
                  <option value={data.fiscalYear}>{data.fiscalYear} (ATUAL)</option>
                )}
                {data.fiscalYears.map((y) => (
                  <option key={y.value} value={y.value}>
                    {y.label}
                  </option>
                ))}
              </select>
            </label>
          </div>

          {data.products.length === 0 ? (
            <p className="note">{type ? 'Neste momento não há produtos deste tipo.' : 'Neste momento não há produtos na loja.'}</p>
          ) : (
            <>
              <ul className="shop-grid">
                {data.products.slice(0, shown).map((p) => (
                  <li key={p.id} className="shop-card">
                    {p.imageUrl ? (
                      <img className="shop-card__image" src={p.imageUrl} alt="" loading="lazy" />
                    ) : (
                      <span className="shop-card__image shop-card__image--empty" aria-hidden="true">
                        <Icon name="images" />
                      </span>
                    )}
                    <div className="shop-card__body">
                      <strong>{p.name}</strong>
                      <span className="member-badges">
                        <span className="member-badge member-badge--position">{p.type}</span>
                        {p.stock > 0 ? (
                          <span className="member-badge">{p.stock} em stock</span>
                        ) : (
                          <span className="member-badge member-badge--retired">Esgotado</span>
                        )}
                        {!p.isPublic && <span className="member-badge member-badge--active">Apenas membros</span>}
                      </span>
                      <span className="shop-card__price">{euros(p.price)}</span>
                      <span className="shop-card__actions">
                        <button type="button" className="btn btn--ghost btn--sm" onClick={() => setOpen({ kind: 'detail', product: p })}>
                          <Icon name="search" />
                          Ver detalhes
                        </button>
                        {p.myReservation ? (
                          <>
                            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setOpen({ kind: 'mine', product: p })}>
                              <Icon name="check" />
                              A minha reserva
                            </button>
                          </>
                        ) : (
                          p.canReserve && (
                            <button type="button" className="btn btn--primary btn--sm" onClick={() => setOpen({ kind: 'reserve', product: p })}>
                              <Icon name="calendar" />
                              Reservar
                            </button>
                          )
                        )}
                      </span>
                      {data.canManage && (
                        <span className="shop-card__actions">
                          {!p.isPublic && (
                            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setOpen({ kind: 'reservations', product: p })}>
                              <Icon name="person" />
                              Ver reservas
                            </button>
                          )}
                          <button type="button" className="icon-btn" onClick={() => setOpen({ kind: 'form', id: p.id })}>
                            <Icon name="pencil" />
                            <span className="sr-only">Editar {p.name}</span>
                          </button>
                          <button type="button" className="icon-btn icon-btn--danger" onClick={() => setOpen({ kind: 'delete', product: p })}>
                            <Icon name="trash" />
                            <span className="sr-only">Eliminar {p.name}</span>
                          </button>
                        </span>
                      )}
                    </div>
                  </li>
                ))}
              </ul>
              {data.products.length > shown && (
                <button type="button" className="btn btn--ghost btn--sm members-more" onClick={() => setShown(shown + PAGE)}>
                  Mostrar mais ({data.products.length - shown})
                </button>
              )}
            </>
          )}

          {open?.kind === 'detail' && <DetailDialog id={open.product.id} onClose={close} />}
          {open?.kind === 'reserve' && <ReserveDialog product={open.product} sizes={data.sizes} onClose={close} onDone={load} />}
          {open?.kind === 'mine' && open.product.myReservation && (
            <MineDialog product={open.product} reservation={open.product.myReservation} onClose={close} onDone={load} />
          )}
          {open?.kind === 'reservations' && <ReservationsDialog product={open.product} onClose={close} onChanged={load} />}
          {open?.kind === 'delete' && (
            <Confirm
              title="Eliminar produto"
              label="Eliminar"
              run={() => api.remove(open.product.id)}
              onClose={close}
              onDone={load}
            >
              <p>
                Eliminar <strong>{open.product.name}</strong> da loja?
              </p>
              <p className="warning">
                <Icon name="warning" />
                Apaga também a imagem e as reservas deste produto. Não dá para desfazer.
              </p>
            </Confirm>
          )}
          {open?.kind === 'form' && <ProductForm id={open.id} canManage={data.canManage} onClose={close} onSaved={load} />}
        </>
      )}
    </section>
  );
}

function DetailDialog({ id, onClose }: { id: number; onClose: () => void }) {
  const [detail, setDetail] = useState<Detail | 'missing' | null>();
  useEffect(() => {
    api.product(id).then((o) => setDetail(o.kind === 'ok' ? o.data : o.kind === 'notfound' ? 'missing' : null));
  }, [id]);

  const field = (label: string, value: ReactNode) => (
    <div className="member-field">
      <dt>{label}</dt>
      <dd>{value}</dd>
    </div>
  );

  return (
    <Dialog title="Detalhes do produto" onClose={onClose}>
      {detail === undefined ? (
        <Loading label="A carregar…" />
      ) : detail === 'missing' ? (
        <p className="form__banner">Este produto já não existe.</p>
      ) : detail === null ? (
        <p className="form__banner">Não foi possível carregar o produto.</p>
      ) : (
        <div className="member-detail">
          <div className="member-detail__head">
            {detail.imageUrl && <img className="shop-detail__image" src={detail.imageUrl} alt="" />}
            <p className="member-detail__name">{detail.name}</p>
          </div>
          <dl className="member-fields">
            {field('Tipo', detail.type)}
            {field('Preço', euros(detail.price))}
            {field('Stock', detail.stock > 0 ? detail.stock : 'Esgotado')}
            {field('Disponível', detail.isAvailable ? 'Sim' : 'Não')}
            {!detail.isPublic && field('Visibilidade', 'Apenas membros')}
          </dl>
          {detail.description && <p className="shop-description">{detail.description}</p>}
        </div>
      )}
    </Dialog>
  );
}

function ReserveDialog({ product, sizes, onClose, onDone }: { product: Product; sizes: string[]; onClose: () => void; onDone: () => void }) {
  const id = useId();
  const [hasSizes, setHasSizes] = useState(false);
  const [size, setSize] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (hasSizes && !size) return setErrors({ size: 'O tamanho é obrigatório quando o produto tem tamanhos' });
    setBusy(true);
    const o = await api.reserve(product.id, { hasSizes, size: hasSizes ? size : null, displayName: displayName.trim() || null });
    setBusy(false);
    if (o.kind === 'ok') {
      onDone();
      onClose();
    } else setErrors(o.kind === 'invalid' ? o.errors : { product: problem(o, 'reservar') });
  };

  return (
    <Dialog
      title="Reservar produto"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="submit" form={`${id}-form`} className="btn btn--primary" disabled={busy}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            Reservar
          </button>
        </>
      }
    >
      <form id={`${id}-form`} className="form" onSubmit={submit} noValidate>
        <p className="answer-event">
          <strong>{product.name}</strong>
          <span>
            {product.type} · {euros(product.price)} · {product.stock} disponíveis
          </span>
        </p>
        {(errors.product || errors.hasSizes) && (
          <p className="form__banner" role="alert">
            {errors.product ?? errors.hasSizes}
          </p>
        )}
        <div className={errors.displayName ? 'form__field form__field--error' : 'form__field'}>
          <label htmlFor={`${id}-name`}>Nome para apresentação (opcional)</label>
          <input id={`${id}-name`} type="text" maxLength={200} value={displayName} onChange={(e) => setDisplayName(e.target.value)} />
          <p className="form__hint">Em branco, fica o teu nome de membro.</p>
          {errors.displayName && <p className="form__error">{errors.displayName}</p>}
        </div>
        <label className="form__check" htmlFor={`${id}-sizes`}>
          <input id={`${id}-sizes`} type="checkbox" checked={hasSizes} onChange={(e) => setHasSizes(e.target.checked)} />
          Este produto tem tamanhos
        </label>
        {hasSizes && (
          <div className={errors.size ? 'form__field form__field--error' : 'form__field'}>
            <label htmlFor={`${id}-size`}>Tamanho</label>
            <span className="control control--select">
              <select id={`${id}-size`} value={size} onChange={(e) => setSize(e.target.value)}>
                <option value="">Escolher o tamanho</option>
                {sizes.map((s) => (
                  <option key={s} value={s}>
                    {SIZE_LABELS[s] ?? s}
                  </option>
                ))}
              </select>
            </span>
            {errors.size && (
              <p className="form__error" role="alert">
                {errors.size}
              </p>
            )}
          </div>
        )}
      </form>
    </Dialog>
  );
}

function ReservationLines({ r }: { r: Reservation }) {
  return (
    <dl className="member-fields">
      <div className="member-field">
        <dt>Reservado por</dt>
        <dd>{r.username}</dd>
      </div>
      {r.displayName && (
        <div className="member-field">
          <dt>Nome para apresentação</dt>
          <dd>{r.displayName}</dd>
        </div>
      )}
      {r.size && (
        <div className="member-field">
          <dt>Tamanho</dt>
          <dd>{r.size}</dd>
        </div>
      )}
      <div className="member-field">
        <dt>Data da reserva</dt>
        <dd>{when(r.createdAt)}</dd>
      </div>
    </dl>
  );
}

/** "Minha Reserva", with "Anular" for the member. */
function MineDialog({ product, reservation, onClose, onDone }: { product: Product; reservation: Reservation; onClose: () => void; onDone: () => void }) {
  const [confirming, setConfirming] = useState(false);
  return (
    <>
      <Dialog
        title="A minha reserva"
        onClose={onClose}
        footer={
          <button type="button" className="btn btn--danger" onClick={() => setConfirming(true)}>
            <Icon name="close" />
            Anular reserva
          </button>
        }
      >
        <p className="answer-event">
          <strong>{product.name}</strong>
          <span>
            {product.type} · {euros(product.price)}
          </span>
        </p>
        <ReservationLines r={reservation} />
      </Dialog>
      {confirming && (
        <Confirm
          title="Anular reserva"
          label="Anular reserva"
          run={() => api.cancel(reservation.id)}
          onClose={() => setConfirming(false)}
          onDone={() => {
            onDone();
            onClose();
          }}
        >
          <p>
            Anular a reserva de <strong>{product.name}</strong> feita a {when(reservation.createdAt)}? Não dá para desfazer.
          </p>
        </Confirm>
      )}
    </>
  );
}

/** "Reservas do Produto" (Admin and Owner): every reservation, newest first, each deletable. */
function ReservationsDialog({ product, onClose, onChanged }: { product: Product; onClose: () => void; onChanged: () => void }) {
  const [list, setList] = useState<Reservation[] | null>();
  const [deleting, setDeleting] = useState<Reservation>();
  const load = () => api.reservations(product.id).then((o) => setList(o.kind === 'ok' ? o.data : null));
  useEffect(() => {
    load();
  }, [product.id]);

  return (
    <>
      <Dialog title={`Reservas · ${product.name}`} size="lg" onClose={onClose}>
        {list === undefined ? (
          <Loading label="A carregar as reservas…" />
        ) : list === null ? (
          <p className="form__banner">Não foi possível carregar as reservas.</p>
        ) : list.length === 0 ? (
          <p className="note">Não existem reservas para este produto.</p>
        ) : (
          <ul className="member-rows">
            {list.map((r) => (
              <li key={r.id} className="member-row shop-reservation">
                <span className="member-row__who">
                  <strong>{r.username}</strong>
                  {r.displayName && <small>Nome: {r.displayName}</small>}
                  <small>
                    {r.size ? `Tamanho ${r.size} · ` : ''}
                    {when(r.createdAt)}
                  </small>
                </span>
                <button type="button" className="icon-btn icon-btn--danger" onClick={() => setDeleting(r)}>
                  <Icon name="trash" />
                  <span className="sr-only">Eliminar a reserva de {r.username}</span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </Dialog>
      {deleting && (
        <Confirm
          title="Eliminar reserva"
          label="Eliminar reserva"
          run={() => api.cancel(deleting.id)}
          onClose={() => setDeleting(undefined)}
          onDone={() => {
            load();
            onChanged();
          }}
        >
          <p>
            Eliminar a reserva de <strong>{deleting.username}</strong> para {product.name} ({when(deleting.createdAt)})? Não dá para desfazer.
          </p>
        </Confirm>
      )}
    </>
  );
}

function Confirm({
  title,
  label,
  run,
  onClose,
  onDone,
  children,
}: {
  title: string;
  label: string;
  run: () => Promise<Outcome<unknown>>;
  onClose: () => void;
  onDone: () => void;
  children: ReactNode;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const go = async () => {
    setBusy(true);
    const o = await run();
    setBusy(false);
    if (o.kind === 'ok' || o.kind === 'notfound') {
      onDone();
      onClose();
    } else setError(problem(o, title.toLowerCase()));
  };
  return (
    <Dialog
      title={title}
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="button" className="btn btn--danger" onClick={go} disabled={busy}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            {label}
          </button>
        </>
      }
    >
      {children}
      {error && (
        <p className="form__banner" role="alert">
          {error}
        </p>
      )}
    </Dialog>
  );
}

const blank = (): ProductInput => ({ name: '', type: '', price: 0, stock: 0, isPublic: true, description: '' });

/** "Novo produto" (Mod, Admin, Owner) / "Editar produto" (Admin, Owner), with the square image. */
function ProductForm({ id, canManage, onClose, onSaved }: { id?: number; canManage: boolean; onClose: () => void; onSaved: () => void }) {
  const [form, setForm] = useState<ProductInput | 'failed' | undefined>(id === undefined ? blank() : undefined);
  const [priceText, setPriceText] = useState('');
  const [savedId, setSavedId] = useState(id);
  const [currentImage, setCurrentImage] = useState<string | null>(null);
  const [picked, setPicked] = useState<File | null>(null);
  const [image, setImage] = useState<{ blob: Blob; preview: string } | null>(null);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [banner, setBanner] = useState<string>();
  const [busy, setBusy] = useState(false);
  const fid = useId();

  useEffect(() => {
    if (id === undefined) return;
    api.product(id).then((o) => {
      if (o.kind !== 'ok') return setForm('failed');
      const d = o.data;
      setForm({ name: d.name, type: d.type, price: d.price, stock: d.stock, isPublic: d.isPublic, description: d.description ?? '' });
      setPriceText(d.price.toFixed(2));
      setCurrentImage(d.imageUrl);
    });
  }, [id]);

  if (form === undefined || form === 'failed') {
    return (
      <Dialog title="Editar produto" onClose={onClose}>
        {form === undefined ? <Loading label="A carregar…" /> : <p className="form__banner">Não foi possível abrir este produto.</p>}
      </Dialog>
    );
  }

  const set = (next: Partial<ProductInput>) => setForm({ ...form, ...next });
  // Only Admin and Owner change an image; a Mod adding a product gives it its first one.
  const canPickImage = canManage || (savedId === undefined && !currentImage);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setBanner(undefined);
    const input = { ...form, price: Number(priceText.replace(',', '.')) || 0 };
    const saved = savedId === undefined ? await api.create(input) : await api.update(savedId, input);
    if (saved.kind !== 'ok') {
      setBusy(false);
      if (saved.kind === 'invalid') return setErrors(saved.errors);
      return setBanner(problem(saved, 'guardar'));
    }
    const newId = savedId ?? (saved.data as { id: number }).id;
    setSavedId(newId);
    onSaved();
    if (image) {
      const uploaded = await api.image(newId, image.blob);
      setBusy(false);
      if (uploaded.kind !== 'ok') {
        setErrors(uploaded.kind === 'invalid' ? { image: Object.values(uploaded.errors)[0] } : {});
        return setBanner('O produto foi guardado, mas a imagem não.');
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

  return (
    <>
      <Dialog
        title={savedId === undefined ? 'Novo produto' : 'Editar produto'}
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
            <div className={field('name')}>
              <label htmlFor={`${fid}-name`}>Nome</label>
              <input id={`${fid}-name`} type="text" maxLength={200} value={form.name} onChange={(e) => set({ name: e.target.value })} />
              {error('name')}
            </div>
            <div className={field('type')}>
              <label htmlFor={`${fid}-type`}>Tipo</label>
              <input id={`${fid}-type`} type="text" maxLength={50} placeholder="Roupa, Álbum, Pin, Outro…" value={form.type} onChange={(e) => set({ type: e.target.value })} />
              {error('type')}
            </div>
          </div>
          <div className="form__row">
            <div className={field('price')}>
              <label htmlFor={`${fid}-price`}>Preço (€)</label>
              <input id={`${fid}-price`} type="number" min="0.01" step="0.01" inputMode="decimal" value={priceText} onChange={(e) => setPriceText(e.target.value)} />
              {error('price')}
            </div>
            <div className={field('stock')}>
              <label htmlFor={`${fid}-stock`}>Stock</label>
              <input id={`${fid}-stock`} type="number" min="0" step="1" value={form.stock} onChange={(e) => set({ stock: Math.trunc(Number(e.target.value) || 0) })} />
              <p className="form__hint">Com stock 0 o produto fica esgotado e indisponível.</p>
              {error('stock')}
            </div>
          </div>
          <label className="form__check" htmlFor={`${fid}-public`}>
            <input id={`${fid}-public`} type="checkbox" checked={form.isPublic} onChange={(e) => set({ isPublic: e.target.checked })} />
            Público (não membros). Sem esta opção, o produto é só para membros e pode ser reservado.
          </label>
          <div className={field('description')}>
            <label htmlFor={`${fid}-description`}>Descrição</label>
            <textarea id={`${fid}-description`} rows={3} maxLength={1000} value={form.description} onChange={(e) => set({ description: e.target.value })} />
            {error('description')}
          </div>
          {canPickImage && (
            <div className={field('image')}>
              <span className="form__label">Imagem</span>
              <span className="inventory-image-pick">
                {image ? (
                  <img className="shop-thumb" src={image.preview} alt="" width={72} height={72} />
                ) : (
                  currentImage && <img className="shop-thumb" src={currentImage} alt="" width={72} height={72} />
                )}
                <label className="btn btn--ghost btn--sm" htmlFor={`${fid}-file`}>
                  <Icon name="upload" />
                  {image || currentImage ? 'Trocar imagem' : 'Escolher imagem'}
                </label>
                <input
                  id={`${fid}-file`}
                  className="sr-only"
                  type="file"
                  accept="image/*"
                  onChange={(e) => {
                    const file = e.target.files?.[0];
                    e.target.value = '';
                    if (!file) return;
                    if (!file.type.startsWith('image/')) return setErrors({ ...errors, image: 'Escolha um ficheiro de imagem.' });
                    if (file.size > MAX_SOURCE) return setErrors({ ...errors, image: 'A imagem não pode exceder 10 MB.' });
                    setPicked(file);
                  }}
                />
              </span>
              <p className="form__hint">A imagem é recortada em quadrado.</p>
              {error('image')}
            </div>
          )}
        </form>
      </Dialog>
      {picked && (
        <Cropper
          file={picked}
          text={CROP_TEXT}
          outWidth={800}
          onCancel={() => setPicked(null)}
          onDone={(blob, preview) => {
            setImage({ blob, preview });
            setPicked(null);
          }}
        />
      )}
    </>
  );
}
