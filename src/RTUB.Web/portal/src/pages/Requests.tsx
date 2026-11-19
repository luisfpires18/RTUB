import { useEffect, useId, useState } from 'react';
import { useLatest } from '../components/AppShared';
import { portal } from '../data/content';
import { RetryNotice, SignInNotice, LinkNotice } from '../components/Notice';
import { ConfirmAction } from '../components/RequestConfirm';
import { CreateEventOffer, RequestDialog } from '../components/RequestDialogs';
import { RequestGroup } from '../components/RequestGroup';
import { type RequestItem, type RequestList, api } from '../data/requestsApi';
import { Loading } from '../components/ui/Loading';
import { PageHead } from '../components/ui/PageHead';
import { SearchField, SelectField } from '../components/ui/SearchTools';

/**
 * /requests - Gestão de Pedidos. The performance requests sent through the public
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

  const latest = useLatest();
  const load = () =>
    latest(api.list(fiscalYear, q, status), (o) =>
      setData(o.kind === 'ok' ? o.data : o.kind === 'signin' ? 'signin' : o.kind === 'forbidden' ? 'forbidden' : null),
    );

  useEffect(() => {
    load();
  }, [fiscalYear, q, status]);

  const list = data && typeof data === 'object' ? data : null;

  return (
    <section className="page wrap requests-page" aria-labelledby="requests-title">
      <PageHead plain eyebrow="Gestão" title="Pedidos de atuação" titleId="requests-title" lead="Os pedidos feitos no portal, do primeiro contacto à resposta." />

      {data === 'signin' ? (
        <SignInNotice path={'/requests'}>Os pedidos são da área de membros.</SignInNotice>
      ) : data === 'forbidden' ? (
        <LinkNotice href={portal.events} label="Ver a agenda">Os pedidos de atuação não estão disponíveis para a tua categoria.</LinkNotice>
      ) : data === null ? (
        <RetryNotice onRetry={load}>Não conseguimos abrir os pedidos agora.</RetryNotice>
      ) : !list ? (
        <Loading label="A carregar os pedidos…" />
      ) : (
        <>
          <div className="events-tools" role="search">
            <SearchField id={ids.q} label="Procurar pedidos" placeholder="Nome, email, tipo de evento, local…" value={text} onChange={setText} />
            <SelectField id={ids.year} label="Ano letivo" value={fiscalYear ?? list.fiscalYear} onChange={setFiscalYear}>
              <option value="">Todos os anos</option>
              {list.fiscalYears.map((y) => (
                <option key={y.value} value={y.value}>
                  {y.label}
                </option>
              ))}
            </SelectField>
            <SelectField id={ids.status} label="Estado" value={status} onChange={setStatus}>
              <option value="">Todos os estados</option>
              <option value="pending">Pendentes</option>
              <option value="confirmed">Aprovados</option>
              <option value="rejected">Rejeitados</option>
            </SelectField>
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
