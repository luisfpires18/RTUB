// The "Pedidos de Reuniões" section of /meetings.
import { useEffect, useId, useState } from 'react';
import { Loading } from './AppShared';
import { Icon, type IconName } from './icons';
import { ConfirmDialog, RequestStatusPill, problem, typeClass } from './MeetingUi';
import { RequestDetailsDialog } from './MeetingProposalDialogs';
import { meetingsApi } from '../data/meetingsApi';
import type { MeetingDraft, MeetingRequest, MeetingRequestPage } from '../data/meetingsTypes';
import { numericDate } from '../data/meetingsDates';
import { RetryNotice } from './Notice';
import { Pager } from './MeetingPager';

const REQUEST_PAGE_SIZES = [4, 8, 12, 16, 20];

const requestIcon = (type: string): IconName => (type === 'ConselhoVeteranos' ? 'megaphone' : type === 'ReuniaoDirecao' ? 'briefcase' : 'people');

type RequestAction = { kind: 'details' | 'delete' | 'remind'; request: MeetingRequest };

/** "Pedidos de Reuniões": every type, the page's year, newest proposed date first, paged by the server. */
export function RequestsSection({
  fy,
  version,
  onAccepted,
  onChanged,
}: {
  fy: string | null;
  version: number;
  onAccepted: (draft: MeetingDraft) => void;
  onChanged: () => void;
}) {
  const [status, setStatus] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(REQUEST_PAGE_SIZES[0]);
  const [data, setData] = useState<MeetingRequestPage | null>();
  const [mine, setMine] = useState(0);
  const [busy, setBusy] = useState<number>();
  const [action, setAction] = useState<RequestAction>();
  const [success, setSuccess] = useState<string>();
  const [error, setError] = useState<string>();
  const ids = { title: useId(), status: useId() };

  useEffect(() => setPage(1), [fy, status]);

  useEffect(() => {
    let live = true;
    meetingsApi.requests(status, fy, page, pageSize).then((o) => {
      if (!live) return;
      if (o.kind === 'ok' && o.data.items.length === 0 && o.data.total > 0 && page > 1) return setPage(Math.ceil(o.data.total / pageSize));
      setData(o.kind === 'ok' ? o.data : null);
    });
    return () => {
      live = false;
    };
  }, [status, fy, page, pageSize, version, mine]);

  useEffect(() => {
    if (!success) return;
    const timer = setTimeout(() => setSuccess(undefined), 3000);
    return () => clearTimeout(timer);
  }, [success]);

  const reload = () => setMine((v) => v + 1);

  const decide = async (request: MeetingRequest, accept: boolean) => {
    setBusy(request.id);
    setError(undefined);
    if (accept) {
      const o = await meetingsApi.accept(request.id);
      setBusy(undefined);
      reload();
      if (o.kind !== 'ok') return setError(problem(o, 'aceitar o pedido'));
      onAccepted(o.data); // the create form opens prefilled with the request, as before
    } else {
      const o = await meetingsApi.reject(request.id);
      setBusy(undefined);
      reload();
      if (o.kind !== 'ok') return setError(problem(o, 'rejeitar o pedido'));
    }
    onChanged();
  };

  const pages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;

  return (
    <section className="mtg-section mtg-requests" aria-labelledby={ids.title}>
      <h2 id={ids.title} className="mtg-section__title">
        <Icon name="inbox" />
        Pedidos de Reuniões
      </h2>
      <div className="mtg-requests__tools">
        <label className="control control--select" htmlFor={ids.status}>
          <span className="sr-only">Estado</span>
          <select id={ids.status} value={status} onChange={(e) => setStatus(e.target.value)}>
            <option value="">Todos os Estados</option>
            <option value="Pending">Pendente</option>
            <option value="Confirmed">Aceite</option>
            <option value="Rejected">Rejeitado</option>
          </select>
        </label>
      </div>

      {success && (
        <div className="mtg-flash mtg-flash--ok" role="status">
          <Icon name="checkCircle" />
          <p>{success}</p>
          <button type="button" className="icon-btn icon-btn--sm" onClick={() => setSuccess(undefined)}>
            <Icon name="close" />
            <span className="sr-only">Fechar a mensagem</span>
          </button>
        </div>
      )}
      {error && (
        <p className="form__banner" role="alert">
          {error}
        </p>
      )}

      {data === undefined ? (
        <Loading label="A carregar os pedidos…" />
      ) : data === null ? (
        <RetryNotice onRetry={reload}>Não conseguimos abrir os pedidos agora.</RetryNotice>
      ) : data.items.length === 0 ? (
        <div className="mtg-empty">
          <Icon name="inbox" className="mtg-empty__icon" />
          <p className="mtg-empty__title">Nenhum pedido de reunião encontrado.</p>
        </div>
      ) : (
        <>
          <ul className="mtg-grid mtg-grid--requests">
            {data.items.map((r) => (
              <li key={r.id}>
                <article className="mtg-request">
                  <div className="mtg-request__top">
                    <span className="mtg-request__icon" aria-hidden="true">
                      <Icon name={requestIcon(r.type)} />
                    </span>
                    <span className={typeClass(r.type)}>{r.typeLabel}</span>
                    {(r.canDelete || r.canRemind) && (
                      <span className="mtg-card__tools">
                        {r.canRemind && (
                          <button type="button" className="icon-btn icon-btn--sm" onClick={() => setAction({ kind: 'remind', request: r })} title="Enviar Lembrete">
                            <Icon name="bell" />
                            <span className="sr-only">Enviar lembrete sobre {r.title}</span>
                          </button>
                        )}
                        {r.canDelete && (
                          <button
                            type="button"
                            className="icon-btn icon-btn--sm icon-btn--danger"
                            onClick={() => setAction({ kind: 'delete', request: r })}
                            title="Eliminar"
                          >
                            <Icon name="trash" />
                            <span className="sr-only">Eliminar pedido {r.title}</span>
                          </button>
                        )}
                      </span>
                    )}
                  </div>
                  <h3 className="mtg-request__title">{r.title}</h3>
                  <dl className="mtg-facts">
                    <div>
                      <dt>
                        <Icon name="person" />
                        <span className="sr-only">Autor</span>
                      </dt>
                      <dd>
                        <strong>{r.authorName}</strong>
                      </dd>
                    </div>
                    <div>
                      <dt>
                        <Icon name="calendar" />
                        <span className="sr-only">Data proposta</span>
                      </dt>
                      <dd>{numericDate(r.proposedDate)}</dd>
                    </div>
                    {r.location && (
                      <div>
                        <dt>
                          <Icon name="geo" />
                          <span className="sr-only">Local</span>
                        </dt>
                        <dd className="mtg-break">{r.location}</dd>
                      </div>
                    )}
                  </dl>
                  <RequestStatusPill status={r.status} />
                  <div className="mtg-card__actions">
                    <button type="button" className="btn btn--ghost btn--sm" onClick={() => setAction({ kind: 'details', request: r })}>
                      <Icon name="eye" />
                      Ver Detalhes
                    </button>
                    {r.canDecide && (
                      <>
                        <button type="button" className="btn btn--primary btn--sm" onClick={() => decide(r, true)} disabled={busy === r.id} title="Aceitar">
                          <Icon name="checkCircle" />
                          Aceitar
                        </button>
                        <button type="button" className="btn btn--danger btn--sm" onClick={() => decide(r, false)} disabled={busy === r.id} title="Rejeitar">
                          <Icon name="xCircle" />
                          Rejeitar
                        </button>
                      </>
                    )}
                  </div>
                </article>
              </li>
            ))}
          </ul>
          <Pager
            total={data.total}
            itemLabel="pedidos"
            page={data.page}
            pages={pages}
            pageSize={data.pageSize}
            sizes={REQUEST_PAGE_SIZES}
            onPage={setPage}
            onPageSize={(s) => (setPageSize(s), setPage(1))}
          />
        </>
      )}

      {action?.kind === 'details' && <RequestDetailsDialog request={action.request} onClose={() => setAction(undefined)} />}
      {action?.kind === 'delete' && (
        <ConfirmDialog
          title="Confirmar Eliminação"
          confirmLabel="Eliminar"
          confirmIcon="trash"
          danger
          onClose={() => setAction(undefined)}
          onConfirm={async () => {
            const o = await meetingsApi.removeRequest(action.request.id);
            if (o.kind !== 'ok') return problem(o, 'eliminar o pedido');
            reload();
          }}
        >
          <p>
            Tem a certeza que quer eliminar o pedido <strong>{action.request.title}</strong>?
          </p>
          <p className="note">Não é possível desfazer.</p>
        </ConfirmDialog>
      )}
      {action?.kind === 'remind' && (
        <ConfirmDialog
          title="Enviar Lembrete"
          confirmLabel="Enviar"
          confirmIcon="bell"
          onClose={() => setAction(undefined)}
          onConfirm={async () => {
            const o = await meetingsApi.remind(action.request.id);
            if (o.kind !== 'ok') return problem(o, 'enviar o lembrete');
            setSuccess('Notificação Enviada!');
          }}
        >
          <p>
            Tem a certeza que quer enviar um lembrete sobre o pedido <strong>{action.request.title}</strong>?
          </p>
          <p className="note">Será enviada uma notificação push para os responsáveis.</p>
        </ConfirmDialog>
      )}
    </section>
  );
}
