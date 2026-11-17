// One group of requests (pending or answered) on /requests.
import { useId, useState } from 'react';
import { Icon } from './icons';
import { PAGE, type RequestItem, statusLabel, statusPill, when } from '../data/requestsApi';

export function RequestGroup({
  title,
  empty,
  items,
  canManage,
  onOpen,
  onAction,
}: {
  title: string;
  empty: string;
  items: RequestItem[];
  canManage: boolean;
  onOpen: (r: RequestItem) => void;
  onAction: (a: { kind: 'approve' | 'reject' | 'delete'; request: RequestItem }) => void;
}) {
  const id = useId();
  const [shown, setShown] = useState(PAGE);

  return (
    <section className="rq-group" aria-labelledby={id}>
      <h2 id={id} className="rq-group__title">
        {title} <span className="note">({items.length})</span>
      </h2>
      {items.length === 0 ? (
        <p className="note">{empty}</p>
      ) : (
        <>
          <ul className="rq-grid">
            {items.slice(0, shown).map((r) => (
              <li key={r.id} className="rq-card">
                <div className="rq-card__head">
                  <strong className="rq-card__title">{r.name}</strong>
                  <span className={statusPill[r.status]}>{statusLabel[r.status]}</span>
                </div>
                <dl className="rq-facts">
                  <div>
                    <dt>
                      <Icon name="calendar" />
                      <span className="sr-only">Evento</span>
                    </dt>
                    <dd>
                      {r.eventType} · {when(r)}
                    </dd>
                  </div>
                  <div>
                    <dt>
                      <Icon name="pin" />
                      <span className="sr-only">Local</span>
                    </dt>
                    <dd>{r.location}</dd>
                  </div>
                  <div>
                    <dt>
                      <Icon name="envelope" />
                      <span className="sr-only">Email</span>
                    </dt>
                    <dd className="rq-break">{r.email}</dd>
                  </div>
                </dl>
                <div className="rq-card__actions">
                  <button type="button" className="btn btn--ghost btn--sm" onClick={() => onOpen(r)}>
                    Ver detalhes
                  </button>
                  {canManage && (r.status === 'pending' || r.status === 'analysing') && (
                    <>
                      <button type="button" className="btn btn--primary btn--sm" onClick={() => onAction({ kind: 'approve', request: r })}>
                        <Icon name="check" />
                        Aprovar
                      </button>
                      <button type="button" className="btn btn--ghost btn--sm" onClick={() => onAction({ kind: 'reject', request: r })}>
                        <Icon name="ban" />
                        Rejeitar
                      </button>
                    </>
                  )}
                  {canManage && (
                    <button type="button" className="icon-btn icon-btn--sm icon-btn--danger" onClick={() => onAction({ kind: 'delete', request: r })}>
                      <Icon name="trash" />
                      <span className="sr-only">Eliminar o pedido de {r.name}</span>
                    </button>
                  )}
                </div>
              </li>
            ))}
          </ul>
          {items.length > shown && (
            <button type="button" className="btn btn--ghost btn--sm members-more" onClick={() => setShown(shown + PAGE)}>
              Mostrar mais ({items.length - shown})
            </button>
          )}
        </>
      )}
    </section>
  );
}
