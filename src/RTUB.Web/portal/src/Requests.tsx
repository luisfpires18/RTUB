import { useEffect, useId, useState } from 'react';
import { Loading } from './App';
import { portal } from './content';
import { Dialog } from './Dialog';
import { call, type Outcome } from './eventsApi';
import { Icon } from './icons';
import { loginTo } from './musicApi';

// /api/requests (task 031, Endpoints/RequestAdminEndpoints.cs). Dates are yyyy-MM-dd; createdAt is UTC.
type Status = 'pending' | 'analysing' | 'confirmed' | 'rejected';
type Option = { value: string; label: string };
type RequestItem = {
  id: number;
  name: string;
  email: string;
  phone: string;
  eventType: string;
  preferredDate: string;
  preferredEndDate: string | null;
  location: string;
  message: string;
  status: Status;
  createdAt: string;
};
type RequestList = { fiscalYears: Option[]; fiscalYear: string; pending: RequestItem[]; answered: RequestItem[]; canManage: boolean };

const api = {
  // fiscalYear: null = the server's default (the current year), "" = every year.
  list: (fiscalYear: string | null, q: string, status: string) => {
    const p = new URLSearchParams();
    if (fiscalYear !== null) p.set('fiscalYear', fiscalYear);
    if (q) p.set('q', q);
    if (status) p.set('status', status);
    const s = p.toString();
    return call<RequestList>('GET', `/api/requests${s ? `?${s}` : ''}`);
  },
  approve: (id: number) => call<{ request: RequestItem; createEventUrl: string }>('POST', `/api/requests/${id}/approve`),
  reject: (id: number) => call<RequestItem>('POST', `/api/requests/${id}/reject`),
  remove: (id: number) => call<void>('DELETE', `/api/requests/${id}`),
};

const PAGE = 8;

const statusLabel: Record<Status, string> = {
  pending: 'Pendente',
  analysing: 'Em análise',
  confirmed: 'Aprovado',
  rejected: 'Rejeitado',
};
const statusPill: Record<Status, string> = { pending: 'pill pill--wait', analysing: 'pill pill--wait', confirmed: 'pill pill--yes', rejected: 'pill pill--no' };

const day = (iso: string) => new Date(`${iso}T00:00:00`).toLocaleDateString('pt-PT', { day: '2-digit', month: 'short', year: 'numeric' });
const when = (r: RequestItem) => (r.preferredEndDate ? `${day(r.preferredDate)} – ${day(r.preferredEndDate)}` : day(r.preferredDate));
const submitted = (iso: string) =>
  new Date(/[zZ]|[+-]\d\d:?\d\d$/.test(iso) ? iso : `${iso}Z`).toLocaleString('pt-PT', { dateStyle: 'medium', timeStyle: 'short' });

const problem = (o: Outcome<unknown>, what: string) =>
  o.kind === 'invalid'
    ? Object.values(o.errors)[0]
    : o.kind === 'forbidden'
      ? 'Não tem permissão para esta ação.'
      : o.kind === 'notfound'
        ? 'Este pedido já não existe.'
        : o.kind === 'signin'
          ? 'A sessão terminou. Entra outra vez.'
          : `Não foi possível ${what}. Tenta outra vez daqui a pouco.`;

/**
 * /requests - Gestão de Pedidos (task 031; was the Blazor page). The performance requests sent through the public
 * /request form: every signed-in member but Leitões reads them; Admin and Owner approve, reject and delete. Approving
 * offers to open the agenda's create form filled in from the request.
 */
export default function Requests() {
  const [data, setData] = useState<RequestList | 'signin' | 'forbidden' | null>();
  const [fiscalYear, setFiscalYear] = useState<string | null>(() => new URLSearchParams(location.search).get('fiscalYear'));
  const [status, setStatus] = useState('');
  const [text, setText] = useState('');
  const [q, setQ] = useState('');
  const [open, setOpen] = useState<RequestItem>();
  const [action, setAction] = useState<{ kind: 'approve' | 'reject' | 'delete'; request: RequestItem }>();
  const [approved, setApproved] = useState<{ request: RequestItem; createEventUrl: string }>();
  const ids = { q: useId(), year: useId(), status: useId() };

  useEffect(() => {
    document.title = 'Pedidos · RTUB';
  }, []);

  useEffect(() => {
    const timer = setTimeout(() => setQ(text.trim()), 250);
    return () => clearTimeout(timer);
  }, [text]);

  const load = () =>
    api.list(fiscalYear, q, status).then((o) =>
      setData(o.kind === 'ok' ? o.data : o.kind === 'signin' ? 'signin' : o.kind === 'forbidden' ? 'forbidden' : null),
    );

  useEffect(() => {
    load();
  }, [fiscalYear, q, status]);

  const list = data && typeof data === 'object' ? data : null;

  return (
    <section className="page wrap requests-page" aria-labelledby="requests-title">
      <header className="page__head">
        <p className="eyebrow">Gestão</p>
        <h1 id="requests-title" className="page__title">
          Pedidos de atuação
        </h1>
        <p className="page__lead">Os pedidos feitos no portal, do primeiro contacto à resposta.</p>
      </header>

      {data === 'signin' ? (
        <div className="notice" role="status">
          <p>Os pedidos são da área de membros.</p>
          <a className="btn btn--primary btn--sm" href={loginTo('/requests')}>
            <Icon name="login" />
            Entrar
          </a>
        </div>
      ) : data === 'forbidden' ? (
        <div className="notice" role="status">
          <p>Os pedidos de atuação não estão disponíveis para a tua categoria.</p>
          <a className="btn btn--ghost btn--sm" href={portal.events}>
            Ver a agenda
          </a>
        </div>
      ) : data === null ? (
        <div className="notice" role="status">
          <p>Não conseguimos abrir os pedidos agora.</p>
          <button type="button" className="btn btn--ghost btn--sm" onClick={load}>
            Tentar novamente
          </button>
        </div>
      ) : !list ? (
        <Loading label="A carregar os pedidos…" />
      ) : (
        <>
          <div className="events-tools" role="search">
            <label className="control" htmlFor={ids.q}>
              <span className="sr-only">Procurar pedidos</span>
              <Icon name="search" />
              <input
                id={ids.q}
                type="search"
                placeholder="Nome, email, tipo de evento, local…"
                value={text}
                onChange={(e) => setText(e.target.value)}
              />
            </label>
            <label className="control control--select" htmlFor={ids.year}>
              <span className="sr-only">Ano letivo</span>
              <select id={ids.year} value={fiscalYear ?? list.fiscalYear} onChange={(e) => setFiscalYear(e.target.value)}>
                <option value="">Todos os anos</option>
                {list.fiscalYears.map((y) => (
                  <option key={y.value} value={y.value}>
                    {y.label}
                  </option>
                ))}
              </select>
            </label>
            <label className="control control--select" htmlFor={ids.status}>
              <span className="sr-only">Estado</span>
              <select id={ids.status} value={status} onChange={(e) => setStatus(e.target.value)}>
                <option value="">Todos os estados</option>
                <option value="pending">Pendentes</option>
                <option value="confirmed">Aprovados</option>
                <option value="rejected">Rejeitados</option>
              </select>
            </label>
          </div>

          <RequestGroup title="Por responder" empty="Nenhum pedido por responder." items={list.pending} canManage={list.canManage} onOpen={setOpen} onAction={setAction} />
          <RequestGroup title="Respondidos" empty="Nenhum pedido respondido." items={list.answered} canManage={list.canManage} onOpen={setOpen} onAction={setAction} />

          {open && (
            <RequestDialog
              request={open}
              canManage={list.canManage}
              onClose={() => setOpen(undefined)}
              onAction={(kind) => {
                setOpen(undefined);
                setAction({ kind, request: open });
              }}
            />
          )}
          {action && (
            <ConfirmAction
              action={action.kind}
              request={action.request}
              onClose={() => setAction(undefined)}
              onDone={(result) => {
                setAction(undefined);
                if (result) setApproved(result);
                load();
              }}
            />
          )}
          {approved && <CreateEventOffer approved={approved} onClose={() => setApproved(undefined)} />}
        </>
      )}
    </section>
  );
}

function RequestGroup({
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

function RequestDialog({
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

const confirmCopy = {
  approve: { title: 'Aprovar pedido', button: 'Aprovar', what: 'aprovar o pedido', text: (n: string) => `Aprovar o pedido de ${n}?` },
  reject: {
    title: 'Rejeitar pedido',
    button: 'Rejeitar',
    what: 'rejeitar o pedido',
    text: (n: string) => `Rejeitar o pedido de ${n}? Depois de respondido, o pedido não volta a ficar por responder.`,
  },
  delete: {
    title: 'Eliminar pedido',
    button: 'Eliminar',
    what: 'eliminar o pedido',
    text: (n: string) => `Eliminar o pedido de ${n}? Não é possível recuperá-lo.`,
  },
};

function ConfirmAction({
  action,
  request,
  onClose,
  onDone,
}: {
  action: 'approve' | 'reject' | 'delete';
  request: RequestItem;
  onClose: () => void;
  onDone: (approved?: { request: RequestItem; createEventUrl: string }) => void;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const copy = confirmCopy[action];

  const confirm = async () => {
    setBusy(true);
    if (action === 'approve') {
      const o = await api.approve(request.id);
      setBusy(false);
      return o.kind === 'ok' ? onDone(o.data) : setError(problem(o, copy.what));
    }
    const o = action === 'reject' ? await api.reject(request.id) : await api.remove(request.id);
    setBusy(false);
    if (o.kind === 'ok' || (action === 'delete' && o.kind === 'notfound')) onDone();
    else setError(problem(o, copy.what));
  };

  return (
    <Dialog
      title={copy.title}
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="button" className={action === 'approve' ? 'btn btn--primary' : 'btn btn--danger'} onClick={confirm} disabled={busy}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            {copy.button}
          </button>
        </>
      }
    >
      <p>{copy.text(request.name)}</p>
      {error && (
        <p className="form__banner" role="alert">
          {error}
        </p>
      )}
    </Dialog>
  );
}

/** The old "Criar Evento" step: the agenda's create form, filled in from the request. */
function CreateEventOffer({ approved, onClose }: { approved: { request: RequestItem; createEventUrl: string }; onClose: () => void }) {
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
