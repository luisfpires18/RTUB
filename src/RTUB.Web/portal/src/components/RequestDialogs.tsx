// Request dialog and the create-event offer after an approval.
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { type RequestItem, statusLabel, statusPill, submitted, when } from '../data/requestsApi';

export function RequestDialog({
  request: r,
  canManage,
  onClose,
  onAction,
}: {
  request: RequestItem;
  canManage: boolean;
  onClose: () => void;
  onAction: (kind: 'approve' | 'reject' | 'delete') => void;
}) {
  const answerable = canManage && (r.status === 'pending' || r.status === 'analysing');

  return (
    <Dialog
      title="Detalhes do pedido"
      size="lg"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose}>
            Voltar
          </button>
          {canManage && (
            <button type="button" className="btn btn--danger" onClick={() => onAction('delete')}>
              Eliminar
            </button>
          )}
          {answerable && (
            <>
              <button type="button" className="btn btn--ghost" onClick={() => onAction('reject')}>
                Rejeitar
              </button>
              <button type="button" className="btn btn--primary" onClick={() => onAction('approve')}>
                Aprovar
              </button>
            </>
          )}
        </>
      }
    >
      <div className="rq-detail">
        <p className="rq-detail__who">
          <strong>{r.name}</strong> <span className={statusPill[r.status]}>{statusLabel[r.status]}</span>
        </p>
        <h3 className="member-section__title">Contacto</h3>
        <dl className="member-fields">
          <div className="member-field">
            <dt>Email</dt>
            <dd className="rq-break">
              <a href={`mailto:${r.email}`}>{r.email}</a>
            </dd>
          </div>
          <div className="member-field">
            <dt>Telefone</dt>
            <dd>
              <a href={`tel:${r.phone.replace(/\s+/g, '')}`}>{r.phone}</a>
            </dd>
          </div>
        </dl>
        <h3 className="member-section__title">Evento</h3>
        <dl className="member-fields">
          <div className="member-field">
            <dt>Tipo</dt>
            <dd>{r.eventType}</dd>
          </div>
          <div className="member-field">
            <dt>{r.preferredEndDate ? 'Datas' : 'Data preferida'}</dt>
            <dd>{when(r)}</dd>
          </div>
          <div className="member-field">
            <dt>Local</dt>
            <dd>{r.location}</dd>
          </div>
          <div className="member-field">
            <dt>Recebido</dt>
            <dd>{submitted(r.createdAt)}</dd>
          </div>
        </dl>
        {r.message && (
          <>
            <h3 className="member-section__title">Mensagem</h3>
            <p className="rq-message">{r.message}</p>
          </>
        )}
      </div>
    </Dialog>
  );
}

/** The "Criar Evento" step: the agenda's create form, filled in from the request. */
export function CreateEventOffer({ approved, onClose }: { approved: { request: RequestItem; createEventUrl: string }; onClose: () => void }) {
  return (
    <Dialog
      title="Pedido aprovado"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose}>
            Agora não
          </button>
          <a className="btn btn--primary" href={approved.createEventUrl}>
            <Icon name="calendar" />
            Criar atuação
          </a>
        </>
      }
    >
      <p>
        O pedido de {approved.request.name} ficou aprovado. Queres criar já a atuação na agenda, com o tipo, o local e a data
        do pedido?
      </p>
    </Dialog>
  );
}
