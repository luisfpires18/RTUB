import { useEffect, useId, useState, type FormEvent } from 'react';
import { Dialog } from './Dialog';
import { call, type Outcome, outcomeText } from '../data/api';
import { Icon } from './icons';
import { MemberBadge } from './MemberBadge';
import { euros } from '../data/format';
import { Banner } from './ui/Banner';
import { DialogFooter } from './ui/DialogFooter';
import { FormField } from './ui/FormField';
import { FieldError } from './ui/FieldError';
import { AsyncConfirmDialog } from './ui/ConfirmDialog';
import { EmptyNote } from './ui/EmptyNote';
import { IconAction } from './ui/IconAction';
import { SelectField } from './ui/SearchTools';

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

const problem = (o: Outcome<unknown>) =>
  outcomeText(o, 'concluir', { forbidden: 'Não tem permissão para esta ação.', notfound: 'Esta promoção já não existe.' });

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
        <EmptyNote>Ainda não há promoções. Junte produtos num pacote com preço especial.</EmptyNote>
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
                    <IconAction icon="pencil" label={`Editar ${p.name}`} onClick={() => setOpen({ kind: 'form', promo: p })} large />
                    <IconAction icon="trash" label={`Eliminar ${p.name}`} onClick={() => setOpen({ kind: 'delete', promo: p })} large danger />
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
  return (
    <AsyncConfirmDialog
      danger
      title="Eliminar promoção"
      confirmLabel="Eliminar"
      onClose={onClose}
      onConfirm={async () => {
        const o = await api.remove(promo.id);
        if (o.kind !== 'ok' && o.kind !== 'notfound') return problem(o);
        onDone();
      }}
    >
      <p>
        Eliminar a promoção <strong>{promo.name}</strong>? Os produtos continuam na loja.
      </p>
    </AsyncConfirmDialog>
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
  const typed = Number(priceText.replace(',', '.')) || 0;
  const saving = typed > 0 && typed < normal ? normal - typed : 0;
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

  return (
    <Dialog
      title={promo ? 'Editar promoção' : 'Nova promoção'}
      size="lg"
      onClose={onClose}
      footer={<DialogFooter form={`${fid}-form`} busy={busy} disabled={!Array.isArray(products)} onClose={onClose} />}
    >
      <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
        <Banner text={banner || (products === 'failed' ? 'Não foi possível carregar os produtos.' : undefined)} />
        <FormField alert id={`${fid}-name`} label="Nome" error={errors.name}>
          <input id={`${fid}-name`} type="text" maxLength={100} placeholder="3 CDs" value={name} onChange={(e) => setName(e.target.value)} />
        </FormField>

        <div className={errors.items ? 'form__field form__field--error' : 'form__field'} role="group" aria-labelledby={`${fid}-items`}>
          <span id={`${fid}-items`} className="promo-label">
            Produtos do pacote
          </span>
          {rows.map((r, i) => (
            <div key={i} className="promo-row">
              <div className="promo-row__pick">
                <label className="sr-only" htmlFor={`${fid}-p${i}`}>
                  Produto {i + 1}
                </label>
                <SelectField id={`${fid}-p${i}`} value={r.productId} onChange={(v) => setRow(i, { productId: Number(v) })}>
                  <option value={0}>Escolher produto…</option>
                  {Array.isArray(products) &&
                    products.map((p) => (
                      <option key={p.id} value={p.id} disabled={rows.some((o, j) => j !== i && o.productId === p.id)}>
                        {p.name} ({p.type}) - {euros(p.price)}
                      </option>
                    ))}
                </SelectField>
                <IconAction icon="trash" label={`Remover produto ${i + 1}`} onClick={() => setRows((all) => all.filter((_, j) => j !== i))} large danger disabled={rows.length === 1} />
              </div>
              <div className="promo-row__qty">
                <span className="promo-stepper" role="group" aria-label={`Quantidade do produto ${i + 1}`}>
                  <IconAction icon="minus" label="Menos um" onClick={() => setRow(i, { quantity: r.quantity - 1 })} large disabled={r.quantity <= 1} />
                  <output className="promo-stepper__value" aria-live="polite">
                    {r.quantity}
                  </output>
                  <IconAction icon="plus" label="Mais um" onClick={() => setRow(i, { quantity: r.quantity + 1 })} large disabled={r.quantity >= 99} />
                </span>
                {r.productId > 0 && (
                  <span className="promo-row__sum">
                    {r.quantity} × {euros(price(r.productId))} = <strong>{euros(price(r.productId) * r.quantity)}</strong>
                  </span>
                )}
              </div>
            </div>
          ))}
          <button type="button" className="btn btn--ghost btn--sm promo-add" disabled={rows.length >= 10} onClick={() => setRows((all) => [...all, { productId: 0, quantity: 1 }])}>
            <Icon name="plus" />
            Adicionar produto
          </button>
          <FieldError text={errors.items} />
        </div>

        <FormField
          alert
          id={`${fid}-price`}
          label="Preço da promoção (€)"
          error={errors.price}
          hint={`Preço normal: ${euros(normal)}. ${saving > 0 ? `Poupa ${euros(saving)} (${Math.round((saving / normal) * 100)} %).` : 'A promoção tem de ser mais barata.'}`}
        >
          <input id={`${fid}-price`} type="text" inputMode="decimal" value={priceText} onChange={(e) => setPriceText(e.target.value)} />
        </FormField>
      </form>
    </Dialog>
  );
}
