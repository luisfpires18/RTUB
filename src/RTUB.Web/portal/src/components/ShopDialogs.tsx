// Shop detail and reservation dialogs.
import { useEffect, useId, useState, type FormEvent, type ReactNode } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { euros } from '../data/format';
import { type Reservation, type Product, type Detail, SIZE_LABELS, api, when, problem } from '../data/shopApi';
import { Confirm } from './ShopProductForm';
import { Loading } from './ui/Loading';
import { DialogFooter } from './ui/DialogFooter';
import { Banner } from './ui/Banner';
import { FormField } from './ui/FormField';
import { EmptyNote } from './ui/EmptyNote';
import { CheckField } from './ui/CheckField';
import { FactRow } from './ui/FactRow';
import { IconAction } from './ui/IconAction';
import { SelectField } from './ui/SearchTools';

export function DetailDialog({ id, onClose }: { id: number; onClose: () => void }) {
  const [detail, setDetail] = useState<Detail | 'missing' | null>();
  useEffect(() => {
    api.product(id).then((o) => setDetail(o.kind === 'ok' ? o.data : o.kind === 'notfound' ? 'missing' : null));
  }, [id]);

  const field = (label: string, value: ReactNode) => (
    <FactRow label={label}>{value}</FactRow>
  );

  return (
    <Dialog title="Detalhes do produto" onClose={onClose}>
      {detail === undefined ? (
        <Loading label="A carregar…" />
      ) : detail === 'missing' ? (
        <Banner role="none" text="Este produto já não existe." />
      ) : detail === null ? (
        <Banner role="none" text="Não foi possível carregar o produto." />
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
            {field('Ano letivo', detail.availableAllYears ? 'Todos os anos letivos' : detail.fiscalYear)}
          </dl>
          {detail.description && <p className="shop-description">{detail.description}</p>}
        </div>
      )}
    </Dialog>
  );
}

export function ReserveDialog({ product, sizes, onClose, onDone }: { product: Product; sizes: string[]; onClose: () => void; onDone: () => void }) {
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
      footer={<DialogFooter form={`${id}-form`} busy={busy} label="Reservar" onClose={onClose} />}
    >
      <form id={`${id}-form`} className="form" onSubmit={submit} noValidate>
        <p className="answer-event">
          <strong>{product.name}</strong>
          <span>
            {product.type} · {euros(product.price)} · {product.stock} disponíveis
          </span>
        </p>
        <Banner text={errors.product || errors.hasSizes} />
        <FormField id={`${id}-name`} label="Nome para apresentação (opcional)" error={errors.displayName} hint="Em branco, fica o teu nome de membro.">
          <input id={`${id}-name`} type="text" maxLength={200} value={displayName} onChange={(e) => setDisplayName(e.target.value)} />
        </FormField>
        <CheckField id={`${id}-sizes`} checked={hasSizes} onChange={(on) => setHasSizes(on)}>
          Este produto tem tamanhos
        </CheckField>
        {hasSizes && (
          <FormField id={`${id}-size`} label="Tamanho" error={errors.size}>
            <SelectField id={`${id}-size`} value={size} onChange={setSize}>
              <option value="">Escolher o tamanho</option>
              {sizes.map((s) => (
                <option key={s} value={s}>
                  {SIZE_LABELS[s] ?? s}
                </option>
              ))}
            </SelectField>
          </FormField>
        )}
      </form>
    </Dialog>
  );
}

function ReservationLines({ r }: { r: Reservation }) {
  return (
    <dl className="member-fields">
      <FactRow label="Reservado por">{r.username}</FactRow>
      {r.displayName && (
        <FactRow label="Nome para apresentação">{r.displayName}</FactRow>
      )}
      {r.size && (
        <FactRow label="Tamanho">{r.size}</FactRow>
      )}
      <FactRow label="Data da reserva">{when(r.createdAt)}</FactRow>
    </dl>
  );
}

/** "Minha Reserva", with "Anular" for the member. */
export function MineDialog({ product, reservation, onClose, onDone }: { product: Product; reservation: Reservation; onClose: () => void; onDone: () => void }) {
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

/** "Reservas do Produto" (Mod, Admin and Owner): every reservation, newest first, each deletable. */
export function ReservationsDialog({ product, onClose, onChanged }: { product: Product; onClose: () => void; onChanged: () => void }) {
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
          <Banner role="none" text="Não foi possível carregar as reservas." />
        ) : list.length === 0 ? (
          <EmptyNote>Não existem reservas para este produto.</EmptyNote>
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
                <IconAction icon="trash" label={`Eliminar a reserva de ${r.username}`} onClick={() => setDeleting(r)} large danger />
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
