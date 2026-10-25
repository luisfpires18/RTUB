import { useEffect, useId, useState, type FormEvent } from 'react';
import { Dialog } from './Dialog';
import { call, type Outcome } from './eventsApi';
import { Icon } from './icons';
import { MemberBadge } from './MemberBadge';

// /api/shop/promotions: bundle offers ("3 CDs por 20 € em vez de 30 €"). Visitors see offers made of public products,
// members all of them; Mod, Admin and Owner create, edit and delete. The server decides and validates everything.

type PromoItem = { productId: number; name: string; type: string; quantity: number; unitPrice: number; imageUrl: string | null };
type Promo = { id: number; name: string; price: number; normalPrice: number; inStock: boolean; items: PromoItem[] };
type Promos = { promotions: Promo[]; canManage: boolean };
type ProductChoice = { id: number; name: string; type: string; price: number };
type Input = { name: string; price: number; items: { productId: number; quantity: number }[] };

const api = {
  list: () => call<Promos>('GET', '/api/shop/promotions'),
  create: (input: Input) => call<Promo>('POST', '/api/shop/promotions', input),
  update: (id: number, input: Input) => call<Promo>('PUT', `/api/shop/promotions/${id}`, input),
  remove: (id: number) => call<void>('DELETE', `/api/shop/promotions/${id}`),
  // Every product of every year, so a bundle can mix products from different fiscal years.
  products: () => call<{ products: ProductChoice[] }>('GET', '/api/shop?fiscalYear='),
};

const euros = (n: number) => `${n.toLocaleString('pt-PT', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} €`;

const problem = (o: Outcome<unknown>) =>
  o.kind === 'invalid'
    ? Object.values(o.errors)[0]
    : o.kind === 'forbidden'
      ? 'Não tem permissão para esta ação.'
      : o.kind === 'notfound'
        ? 'Esta promoção já não existe.'
        : o.kind === 'signin'
          ? 'A sessão terminou. Entra outra vez.'
          : 'Não foi possível concluir. Tenta outra vez daqui a pouco.';

type Open = { kind: 'form'; promo?: Promo } | { kind: 'delete'; promo: Promo };

/** The "Promoções" grid above the catalogue; nothing at all while there is no offer and the viewer cannot add one. */
export function PromotionsSection() {
  const [data, setData] = useState<Promos | null>();
  const [open, setOpen] = useState<Open>();

  const load = () => {
    api.list().then((o) => setData(o.kind === 'ok' ? o.data : null));
  };
  useEffect(load, []);

  if (!data || (data.promotions.length === 0 && !data.canManage)) return null;
  const close = () => setOpen(undefined);

  return (
    <section className="shop-promos" aria-labelledby="shop-promos-title">
      <div className="shop-promos__head">
        <h2 id="shop-promos-title" className="shop-promos__title">
          Promoções
        </h2>
        {data.canManage && (
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => setOpen({ kind: 'form' })}>
            <Icon name="plus" />
            Nova promoção
          </button>
        )}
      </div>

      {data.promotions.length === 0 ? (
        <p className="note">Ainda não há promoções. Junte produtos num pacote com preço especial.</p>
      ) : (
        <ul className="shop-grid">
          {data.promotions.map((p) => (
            <li key={p.id} className="shop-card shop-card--promo">
              <div className="shop-card__body">
                <span className="member-badges">
                  <MemberBadge kind="active">PROMOÇÃO</MemberBadge>
                  {!p.inStock && <MemberBadge kind="retired">Esgotado</MemberBadge>}
                </span>
                <strong>{p.name}</strong>
                <ul className="promo-items">
                  {p.items.map((i) => (
                    <li key={i.productId}>
                      {i.quantity} × {i.name}
                    </li>
                  ))}
                </ul>
                <span className="shop-card__price">
                  <span className="shop-card__was">{euros(p.normalPrice)}</span>
                  {euros(p.price)}
                </span>
                <span className="note">Poupa {euros(p.normalPrice - p.price)}</span>
                {data.canManage && (
                  <span className="shop-card__actions">
                    <button type="button" className="icon-btn" onClick={() => setOpen({ kind: 'form', promo: p })}>
                      <Icon name="pencil" />
                      <span className="sr-only">Editar {p.name}</span>
                    </button>
                    <button type="button" className="icon-btn icon-btn--danger" onClick={() => setOpen({ kind: 'delete', promo: p })}>
                      <Icon name="trash" />
                      <span className="sr-only">Eliminar {p.name}</span>
                    </button>
                  </span>
                )}
              </div>
            </li>
          ))}
        </ul>
      )}

      {open?.kind === 'form' && <PromotionForm promo={open.promo} onClose={close} onSaved={load} />}
      {open?.kind === 'delete' && <DeletePromotion promo={open.promo} onClose={close} onDone={load} />}
    </section>
  );
}

function DeletePromotion({ promo, onClose, onDone }: { promo: Promo; onClose: () => void; onDone: () => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const go = async () => {
    setBusy(true);
    const o = await api.remove(promo.id);
    setBusy(false);
    if (o.kind === 'ok' || o.kind === 'notfound') {
      onDone();
      onClose();
    } else setError(problem(o));
  };
  return (
    <Dialog
      title="Eliminar promoção"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="button" className="btn btn--danger" onClick={go} disabled={busy}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            Eliminar
          </button>
        </>
      }
    >
      <p>
        Eliminar a promoção <strong>{promo.name}</strong>? Os produtos continuam na loja.
      </p>
      {error && (
        <p className="form__banner" role="alert">
          {error}
        </p>
      )}
    </Dialog>
  );
}

function PromotionForm({ promo, onClose, onSaved }: { promo?: Promo; onClose: () => void; onSaved: () => void }) {
  const fid = useId();
  const [name, setName] = useState(promo?.name ?? '');
  const [priceText, setPriceText] = useState(promo ? String(promo.price).replace('.', ',') : '');
  const [rows, setRows] = useState<{ productId: number; quantity: number }[]>(
    promo ? promo.items.map((i) => ({ productId: i.productId, quantity: i.quantity })) : [{ productId: 0, quantity: 1 }],
  );
  const [products, setProducts] = useState<ProductChoice[] | 'failed'>();
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [banner, setBanner] = useState<string>();
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    api.products().then((o) => setProducts(o.kind === 'ok' ? o.data.products : 'failed'));
  }, []);

  const price = (id: number) => (Array.isArray(products) ? (products.find((p) => p.id === id)?.price ?? 0) : 0);
  const normal = rows.reduce((sum, r) => sum + price(r.productId) * r.quantity, 0);
  const setRow = (index: number, change: Partial<{ productId: number; quantity: number }>) =>
    setRows((all) => all.map((r, i) => (i === index ? { ...r, ...change } : r)));

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setBanner(undefined);
    setErrors({});
    const input: Input = {
      name,
      price: Number(priceText.replace(',', '.')) || 0,
      items: rows.filter((r) => r.productId > 0),
    };
    const saved = promo ? await api.update(promo.id, input) : await api.create(input);
    setBusy(false);
    if (saved.kind === 'ok') {
      onSaved();
      return onClose();
    }
    if (saved.kind === 'invalid') return setErrors(saved.errors);
    setBanner(problem(saved));
  };

  const field = (key: string) => (errors[key] ? 'form__field form__field--error' : 'form__field');
  const error = (key: string) =>
    errors[key] && (
      <p className="form__error" role="alert">
        {errors[key]}
      </p>
    );

  return (
    <Dialog
      title={promo ? 'Editar promoção' : 'Nova promoção'}
      size="lg"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="submit" form={`${fid}-form`} className="btn btn--primary" disabled={busy || !Array.isArray(products)}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            Guardar
          </button>
        </>
      }
    >
      <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
        {(banner || products === 'failed') && (
          <p className="form__banner" role="alert">
            {banner ?? 'Não foi possível carregar os produtos.'}
          </p>
        )}
        <div className={field('name')}>
          <label htmlFor={`${fid}-name`}>Nome</label>
          <input id={`${fid}-name`} type="text" maxLength={100} placeholder="3 CDs" value={name} onChange={(e) => setName(e.target.value)} />
          {error('name')}
        </div>

        <fieldset className={field('items')}>
          <legend>Produtos do pacote</legend>
          {rows.map((r, i) => (
            <div key={i} className="promo-row">
              <label className="sr-only" htmlFor={`${fid}-p${i}`}>
                Produto {i + 1}
              </label>
              <select id={`${fid}-p${i}`} value={r.productId} onChange={(e) => setRow(i, { productId: Number(e.target.value) })}>
                <option value={0}>Escolher produto…</option>
                {Array.isArray(products) &&
                  products.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.name} ({p.type}) - {euros(p.price)}
                    </option>
                  ))}
              </select>
              <label className="sr-only" htmlFor={`${fid}-q${i}`}>
                Quantidade do produto {i + 1}
              </label>
              <input
                id={`${fid}-q${i}`}
                type="number"
                min={1}
                max={99}
                step={1}
                value={r.quantity}
                onChange={(e) => setRow(i, { quantity: Math.trunc(Number(e.target.value) || 1) })}
              />
              <button
                type="button"
                className="icon-btn icon-btn--danger"
                disabled={rows.length === 1}
                onClick={() => setRows((all) => all.filter((_, j) => j !== i))}
              >
                <Icon name="trash" />
                <span className="sr-only">Remover produto {i + 1}</span>
              </button>
            </div>
          ))}
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => setRows((all) => [...all, { productId: 0, quantity: 1 }])}>
            <Icon name="plus" />
            Adicionar produto
          </button>
          {error('items')}
        </fieldset>

        <div className={field('price')}>
          <label htmlFor={`${fid}-price`}>Preço da promoção (€)</label>
          <input id={`${fid}-price`} type="text" inputMode="decimal" value={priceText} onChange={(e) => setPriceText(e.target.value)} />
          <p className="form__hint">Preço normal: {euros(normal)}. A promoção tem de ser mais barata.</p>
          {error('price')}
        </div>
      </form>
    </Dialog>
  );
}
