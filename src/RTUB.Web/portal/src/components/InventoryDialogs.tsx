// Inventory thumbnail, detail and delete dialogs.
import { useEffect, useState } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { MemberBadge } from './MemberBadge';
import { type Card, type Detail, api, problem, conditionClass } from '../data/inventoryApi';
import { Loading } from './ui/Loading';
import { AsyncConfirmDialog } from './ui/ConfirmDialog';

export function Thumb({ url, size }: { url: string | null; size: number }) {
  return url ? (
    <img className="inventory-thumb" src={url} alt="" width={size} height={size} loading="lazy" />
  ) : (
    <span className="inventory-thumb inventory-thumb--empty" style={{ width: size, height: size }} aria-hidden="true">
      <Icon name="music" />
    </span>
  );
}

/** "Detalhes do Instrumento": every field any member may see. */
export function DetailDialog({ id, onClose }: { id: number; onClose: () => void }) {
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
              <MemberBadge kind="position">{detail.categoryLabel}</MemberBadge>
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

export function DeleteDialog({ instrument, onClose, onDone }: { instrument: Card; onClose: () => void; onDone: () => void }) {

  const remove = async () => {
    const o = await api.remove(instrument.id);
    if (o.kind !== 'ok' && o.kind !== 'notfound') return problem(o, 'eliminar');
    onDone();
  };

  return (
    <AsyncConfirmDialog title="Eliminar instrumento" confirmLabel="Eliminar" danger onConfirm={remove} onClose={onClose}>
      <p>
        Eliminar <strong>{instrument.name}</strong> do inventário?
      </p>
      <p className="warning">
        <Icon name="warning" />
        Apaga também a imagem. Não dá para desfazer.
      </p>
    </AsyncConfirmDialog>
  );
}
