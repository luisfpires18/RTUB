import { useEffect, useId, useState, type FormEvent } from 'react';
import { Loading } from './App';
import { portal } from './content';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { day, euros, isoDay, matches, problem, treasuryApi, type NerbaOrder, type NerbaOrders } from './treasuryApi';
import { Confirm, Field, Refusal, SaveFooter } from './TreasuryUi';

type Sort = 'item' | 'type' | 'stock' | 'pricePerUnit' | 'total';
const COLUMNS: { key: Sort; label: string; numeric?: boolean }[] = [
  { key: 'item', label: 'Item' },
  { key: 'type', label: 'Tipo' },
  { key: 'stock', label: 'Qtd.', numeric: true },
  { key: 'pricePerUnit', label: 'Preço/Un.', numeric: true },
  { key: 'total', label: 'Total', numeric: true },
];
const weekday = (iso: string) => new Date(`${isoDay(iso)}T12:00:00`).toLocaleDateString('pt-PT', { weekday: 'long' });

/**
 * /treasury/nerba/{eventId} - one Nerba event's orders (React track 024; was the Blazor /nerba/event/{id}). Treasury
 * members: items with quantity, unit price and total, per day of a multi-day event, sortable, searchable. Mod, Admin and
 * Owner add, edit and delete items.
 */
export default function TreasuryNerbaEvent({ eventId }: { eventId: number }) {
  const [data, setData] = useState<NerbaOrders | 'signin' | 'forbidden' | 'failed' | 'missing'>();
  const [dayFilter, setDayFilter] = useState('');
  const [search, setSearch] = useState('');
  const [sort, setSort] = useState<{ key: Sort; asc: boolean }>({ key: 'item', asc: true });
  const [form, setForm] = useState<{ order?: NerbaOrder }>();
  const [deleting, setDeleting] = useState<NerbaOrder>();
  const id = useId();

  const load = () =>
    treasuryApi.nerbaOrders(eventId).then((o) =>
      setData(o.kind === 'ok' ? o.data : o.kind === 'signin' || o.kind === 'forbidden' ? o.kind : o.kind === 'notfound' ? 'missing' : 'failed'),
    );

  useEffect(() => {
    load();
  }, [eventId]);

  const n = data && typeof data === 'object' ? data : null;

  useEffect(() => {
    document.title = `${n?.event.name ?? 'Encomendas'} · Nerba · RTUB`;
  }, [n?.event.name]);

  if (typeof data === 'string' || !n) {
    return (
      <section className="page wrap">
        <a className="back-link" href={portal.treasuryNerba}>
          <Icon name="arrow" />
          Encomendas Nerba
        </a>
        {typeof data === 'string' ? <Refusal state={data} path={portal.treasuryNerbaEvent(eventId)} onRetry={load} /> : <Loading label="A carregar as encomendas…" />}
      </section>
    );
  }

  const ofDay = (d: string) => n.orders.filter((o) => isoDay(o.orderDate) === isoDay(d));
  const inDay = dayFilter ? ofDay(dayFilter) : n.orders;
  const rows = inDay
    .filter((o) => matches(search, o.item, o.type))
    .sort((a, b) => {
      const x = a[sort.key] ?? '';
      const y = b[sort.key] ?? '';
      const c = typeof x === 'number' && typeof y === 'number' ? x - y : String(x).localeCompare(String(y), 'pt-PT', { sensitivity: 'base' });
      return sort.asc ? c : -c;
    });

  return (
    <section className="page wrap events-page tr-page" aria-labelledby="tr-title">
      <a className="back-link" href={portal.treasuryNerba}>
        <Icon name="arrow" />
        Encomendas Nerba
      </a>
      <header className="page__head events-page__head">
        <div>
          <p className="eyebrow">Encomendas Nerba</p>
          <h1 id="tr-title" className="page__title">
            {n.event.name}
          </h1>
          <p className="page__lead">
            {day(n.event.date)}
            {n.event.endDate && ` — ${day(n.event.endDate)}`}
            {n.event.location && ` · ${n.event.location}`}
          </p>
        </div>
        {n.canManage && (
          <div className="events-page__actions">
            <button type="button" className="btn btn--primary btn--sm" onClick={() => setForm({})}>
              <Icon name="plus" />
              Adicionar item
            </button>
          </div>
        )}
      </header>

      {n.days.length > 0 && (
        <div className="segmented segmented--wrap" role="radiogroup" aria-label="Dia">
          {['', ...n.days].map((d) => {
            const list = d ? ofDay(d) : n.orders;
            return (
              <label key={d || 'all'} className={dayFilter === d ? 'segmented__option is-on' : 'segmented__option'}>
                <input type="radio" name={`${id}-day`} checked={dayFilter === d} onChange={() => setDayFilter(d)} />
                {d ? day(d).slice(0, 5) : 'Todos os dias'}
                <span className="segmented__count">
                  {list.length} · {euros(list.reduce((s, o) => s + o.total, 0))}
                </span>
              </label>
            );
          })}
        </div>
      )}

      <dl className="tr-tiles tr-tiles--one">
        <div className="tr-tile">
          <dt>{dayFilter ? `Total — ${day(dayFilter)}` : 'Total da encomenda'}</dt>
          <dd className="tr-in">{euros(inDay.reduce((s, o) => s + o.total, 0))}</dd>
        </div>
      </dl>

      {inDay.length === 0 ? (
        <p className="note">{dayFilter ? 'Nenhum item neste dia.' : 'Este evento ainda não tem encomendas.'}</p>
      ) : (
        <>
          <div className="events-tools" role="search">
            <label className="control" htmlFor={`${id}-search`}>
              <span className="sr-only">Procurar item</span>
              <Icon name="search" />
              <input id={`${id}-search`} type="search" placeholder="Procurar por item ou tipo" value={search} onChange={(e) => setSearch(e.target.value)} />
            </label>
          </div>
          <div className="tr-table-wrap">
            <table className="tr-table">
              <thead>
                <tr>
                  {COLUMNS.map((c) => (
                    <th key={c.key} className={c.numeric ? 'tr-num' : undefined} aria-sort={sort.key === c.key ? (sort.asc ? 'ascending' : 'descending') : 'none'}>
                      <button type="button" onClick={() => setSort({ key: c.key, asc: sort.key === c.key ? !sort.asc : true })}>
                        {c.label}
                        {sort.key === c.key && <Icon name={sort.asc ? 'up' : 'down'} />}
                      </button>
                    </th>
                  ))}
                  {n.canManage && (
                    <th>
                      <span className="sr-only">Ações</span>
                    </th>
                  )}
                </tr>
              </thead>
              <tbody>
                {rows.map((o) => (
                  <tr key={o.id}>
                    <td>
                      {o.item}
                      {!dayFilter && o.orderDate && n.days.length > 0 && <small className="tr-muted"> · {day(o.orderDate).slice(0, 5)}</small>}
                    </td>
                    <td className="tr-muted">{o.type ?? '—'}</td>
                    <td className="tr-num">{o.stock}</td>
                    <td className="tr-num">{euros(o.pricePerUnit)}</td>
                    <td className="tr-num">
                      <strong>{euros(o.total)}</strong>
                    </td>
                    {n.canManage && (
                      <td className="tr-row-tools">
                        <button type="button" className="icon-btn icon-btn--sm" onClick={() => setForm({ order: o })}>
                          <Icon name="pencil" />
                          <span className="sr-only">Editar {o.item}</span>
                        </button>
                        <button type="button" className="icon-btn icon-btn--sm icon-btn--danger" onClick={() => setDeleting(o)}>
                          <Icon name="trash" />
                          <span className="sr-only">Eliminar {o.item}</span>
                        </button>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {rows.length === 0 && <p className="note">Nenhum item corresponde à pesquisa.</p>}
        </>
      )}

      {form && <OrderDialog data={n} order={form.order} defaultDay={dayFilter} onClose={() => setForm(undefined)} onSaved={setData} />}
      {deleting && (
        <Confirm
          title="Eliminar item"
          action="Eliminar"
          onClose={() => setDeleting(undefined)}
          onConfirm={async () => {
            const o = await treasuryApi.removeNerbaOrder(deleting.id);
            if (o.kind !== 'ok') return problem(o, 'eliminar o item');
            setData(o.data);
          }}
        >
          <p>
            Eliminar <strong>{deleting.item}</strong>?
          </p>
        </Confirm>
      )}
    </section>
  );
}

/** "Adicionar item" / "Editar item": day (multi-day events), item, type, quantity, unit price. */
function OrderDialog({
  data,
  order,
  defaultDay,
  onClose,
  onSaved,
}: {
  data: NerbaOrders;
  order?: NerbaOrder;
  defaultDay: string;
  onClose: () => void;
  onSaved: (n: NerbaOrders) => void;
}) {
  const [orderDate, setOrderDate] = useState(isoDay(order?.orderDate ?? null) || isoDay(defaultDay || null) || isoDay(data.days[0] ?? null));
  const [item, setItem] = useState(order?.item ?? '');
  const [type, setType] = useState(order?.type ?? '');
  const [stock, setStock] = useState(order ? String(order.stock) : '1');
  const [price, setPrice] = useState(order ? String(order.pricePerUnit) : '');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const fid = useId();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    const input = {
      item,
      type,
      stock: /^\d+$/.test(stock.trim()) ? Number(stock) : null,
      pricePerUnit: price.trim() === '' ? null : Number(price.replace(',', '.')),
      orderDate: data.days.length > 0 ? orderDate : null,
    };
    const o = order ? await treasuryApi.updateNerbaOrder(order.id, input) : await treasuryApi.addNerbaOrder(data.event.id, input);
    setBusy(false);
    if (o.kind === 'invalid') return setErrors(o.errors);
    if (o.kind !== 'ok') return setErrors({ form: problem(o, 'guardar o item') });
    onSaved(o.data);
    onClose();
  };

  return (
    <Dialog
      title={order ? 'Editar item' : 'Adicionar item'}
      onClose={onClose}
      footer={<SaveFooter formId={`${fid}-form`} busy={busy} disabled={!item.trim() || !stock || !price} onClose={onClose} />}
    >
      <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
        {errors.form && (
          <p className="form__banner" role="alert">
            {errors.form}
          </p>
        )}
        {data.days.length > 0 && (
          <Field id={`${fid}-day`} label="Dia" error={errors.orderDate}>
            <span className="control control--select">
              <select id={`${fid}-day`} value={orderDate} onChange={(e) => setOrderDate(e.target.value)}>
                {data.days.map((d) => (
                  <option key={d} value={isoDay(d)}>
                    {day(d)} ({weekday(d)})
                  </option>
                ))}
              </select>
            </span>
          </Field>
        )}
        <Field id={`${fid}-item`} label="Item" error={errors.item}>
          <input id={`${fid}-item`} type="text" maxLength={200} value={item} onChange={(e) => setItem(e.target.value)} />
        </Field>
        <Field id={`${fid}-type`} label="Tipo (opcional)" error={errors.type}>
          <input id={`${fid}-type`} type="text" maxLength={100} value={type} onChange={(e) => setType(e.target.value)} />
        </Field>
        <div className="form__row">
          <Field id={`${fid}-stock`} label="Quantidade" error={errors.stock}>
            <input id={`${fid}-stock`} type="number" inputMode="numeric" min="1" step="1" value={stock} onChange={(e) => setStock(e.target.value)} />
          </Field>
          <Field id={`${fid}-price`} label="Preço por unidade (€)" error={errors.pricePerUnit}>
            <input id={`${fid}-price`} type="number" inputMode="decimal" min="0" step="0.01" value={price} onChange={(e) => setPrice(e.target.value)} />
          </Field>
        </div>
      </form>
    </Dialog>
  );
}
