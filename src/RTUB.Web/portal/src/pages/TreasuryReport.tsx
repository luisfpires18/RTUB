import { useEffect, useRef, useState } from 'react';
import { portal } from '../data/content';
import { Icon } from '../components/icons';
import { problem, treasuryApi, type Activity, type ReportEvent, type Report, type Transaction } from '../data/treasuryApi';
import { Refusal, TreasuryTabs } from '../components/TreasuryUi';
import { euros } from '../data/format';
import { ActivityPanel } from '../components/TreasuryActivityPanel';
import { BalanceDialog, ActivityDialog } from '../components/TreasuryReportDialogs';
import { TransactionDialog, HistoryDialog } from '../components/TreasuryTransactionDialogs';
import { Loading } from '../components/ui/Loading';
import { AsyncConfirmDialog } from '../components/ui/ConfirmDialog';
import { Banner } from '../components/ui/Banner';
import { BackLink } from '../components/ui/BackLink';
import { EmptyNote } from '../components/ui/EmptyNote';
import { PageHead } from '../components/ui/PageHead';

/**
 * /treasury/reports/{id} - one annual report. Treasury members:
 * money totals, activities with their transactions and receipts, PDF. Managers (Admin, Owner, treasury-team Mod) edit
 * bank / cash, activities and transactions of a draft, and lock / unlock activities; Admin and Owner see the history.
 */
export default function TreasuryReport({ reportId }: { reportId: number }) {
  const [report, setReport] = useState<Report | 'signin' | 'forbidden' | 'failed' | 'missing'>();
  const [message, setMessage] = useState<string>();
  const [balance, setBalance] = useState<'bank' | 'cash'>();
  const [activityForm, setActivityForm] = useState<{ activity?: Activity; event?: ReportEvent }>();
  // ?activityForEvent={id} (from "As minhas ações pendentes") opens "Nova atividade" for that event, once.
  const forEvent = useRef(Number(new URLSearchParams(location.search).get('activityForEvent')) || 0);
  const [transactionForm, setTransactionForm] = useState<{ activity: Activity; transaction?: Transaction }>();
  const [deleting, setDeleting] = useState<{ activity?: Activity; transaction?: Transaction }>();
  const [history, setHistory] = useState(false);

  const load = () =>
    treasuryApi.report(reportId).then((o) =>
      setReport(o.kind === 'ok' ? o.data : o.kind === 'signin' || o.kind === 'forbidden' ? o.kind : o.kind === 'notfound' ? 'missing' : 'failed'),
    );

  useEffect(() => {
    load();
  }, [reportId]);

  const r = report && typeof report === 'object' ? report : null;

  useEffect(() => {
    if (!r || !forEvent.current) return;
    const event = r.canManage && !r.isPublished ? r.events?.find((e) => e.id === forEvent.current) : undefined;
    forEvent.current = 0;
    const url = new URL(location.href);
    url.searchParams.delete('activityForEvent');
    window.history.replaceState(window.history.state, '', url);
    if (event) setActivityForm({ event });
  }, [r]);

  useEffect(() => {
    document.title = `${r?.title ?? 'Relatório'} · Tesouraria · RTUB`;
  }, [r?.title]);

  const editable = !!r && r.canManage && !r.isPublished;

  const lock = async (a: Activity) => {
    setMessage(undefined);
    const o = await treasuryApi.lockActivity(a.id, !a.isLocked);
    if (o.kind === 'ok') setReport(o.data);
    else setMessage(problem(o, a.isLocked ? 'desbloquear a atividade' : 'bloquear a atividade'));
  };

  if (typeof report === 'string') {
    return (
      <section className="page wrap">
        <BackLink href={portal.treasury}>Tesouraria</BackLink>
        <Refusal state={report} path={portal.treasuryReport(reportId)} onRetry={load} />
      </section>
    );
  }

  if (!r) {
    return (
      <section className="page wrap">
        <Loading label="A carregar o relatório…" />
      </section>
    );
  }

  const fy = `${r.year}-${r.year + 1}`;
  const tile = (label: string, value: number, tone: 'in' | 'out' | 'sign', edit?: 'bank' | 'cash') => (
    <div className="tr-tile">
      <dt>{label}</dt>
      <dd className={tone === 'in' || (tone === 'sign' && value >= 0) ? 'tr-in' : 'tr-out'}>{euros(value)}</dd>
      {edit && editable && (
        <button type="button" className="icon-btn icon-btn--sm tr-tile__edit" onClick={() => setBalance(edit)}>
          <Icon name="pencil" />
          <span className="sr-only">Editar {label}</span>
        </button>
      )}
    </div>
  );

  return (
    <section className="page wrap events-page tr-page" aria-labelledby="tr-title">
      <BackLink href={portal.treasury}>Tesouraria</BackLink>
      <PageHead
        eyebrow={`Ano letivo ${fy}`}
        title={r.title}
        titleId="tr-title"
        meta={
          <p className="tr-badges">
            <span className={r.isPublished ? 'pill pill--yes' : 'pill pill--wait'}>{r.isPublished ? 'Publicado' : 'Rascunho'}</span>
          </p>
        }
        lead={r.summary}
      >
        <div className="events-page__actions">
          <a className="btn btn--ghost btn--sm" href={`/api/treasury/reports/${r.id}/pdf`} download>
            <Icon name="download" />
            PDF
          </a>
          {r.canSeeHistory && (
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setHistory(true)}>
              <Icon name="clock" />
              Histórico
            </button>
          )}
        </div>
      </PageHead>

      <TreasuryTabs current="reports" fiscalYear={fy} />

      {r.isPublished && (
        <p className="notice" role="status">
          Relatório publicado: só de leitura.
        </p>
      )}
      <Banner text={message} />

      <dl className="tr-tiles">
        {tile('Dinheiro total', r.totals.totalMoney, 'sign')}
        {tile('Dinheiro no banco', r.totals.bank, 'sign', 'bank')}
        {tile('Dinheiro em caixa', r.totals.cash, 'sign', 'cash')}
        {tile('Calotes', r.totals.calotes, 'out')}
        {tile('Receitas', r.totals.income, 'in')}
        {tile('Despesas', r.totals.expenses, 'out')}
        {tile('Saldo', r.totals.balance, 'sign')}
      </dl>

      <section className="lx-section" aria-labelledby="tr-activities">
        <div className="tr-section-head">
          <h2 id="tr-activities" className="lx-section__title">
            Atividades <small>{r.activities.length}</small>
          </h2>
          {editable && (
            <button type="button" className="btn btn--primary btn--sm" onClick={() => setActivityForm({})}>
              <Icon name="plus" />
              Nova atividade
            </button>
          )}
        </div>
        {r.activities.length === 0 ? (
          <EmptyNote>Nenhuma atividade criada ainda.</EmptyNote>
        ) : (
          <div className="tr-activities">
            {r.activities.map((a) => (
              <ActivityPanel
                key={a.id}
                activity={a}
                report={r}
                onLock={() => lock(a)}
                onEdit={() => setActivityForm({ activity: a })}
                onDelete={() => setDeleting({ activity: a })}
                onAdd={() => setTransactionForm({ activity: a })}
                onEditTransaction={(t) => setTransactionForm({ activity: a, transaction: t })}
                onDeleteTransaction={(t) => setDeleting({ transaction: t })}
              />
            ))}
          </div>
        )}
      </section>

      {balance && <BalanceDialog report={r} kind={balance} onClose={() => setBalance(undefined)} onSaved={setReport} />}
      {activityForm && (
        <ActivityDialog
          reportId={r.id}
          activity={activityForm.activity}
          event={activityForm.event}
          events={r.events ?? []}
          onClose={() => setActivityForm(undefined)}
          onSaved={setReport}
        />
      )}
      {transactionForm && (
        <TransactionDialog
          activity={transactionForm.activity}
          transaction={transactionForm.transaction}
          onClose={() => setTransactionForm(undefined)}
          onSaved={setReport}
        />
      )}
      {deleting?.activity && (
        <AsyncConfirmDialog danger
          title="Eliminar atividade"
          confirmLabel="Eliminar"
          onClose={() => setDeleting(undefined)}
          onConfirm={async () => {
            const o = await treasuryApi.removeActivity(deleting.activity!.id);
            if (o.kind !== 'ok') return problem(o, 'eliminar a atividade');
            setReport(o.data);
          }}
        >
          <p>
            Eliminar a atividade <strong>{deleting.activity.name}</strong>?
          </p>
          <p className="warning">
            <Icon name="warning" />
            Apaga também as transações e os recibos dela. Não dá para desfazer.
          </p>
        </AsyncConfirmDialog>
      )}
      {deleting?.transaction && (
        <AsyncConfirmDialog danger
          title="Eliminar transação"
          confirmLabel="Eliminar"
          onClose={() => setDeleting(undefined)}
          onConfirm={async () => {
            const o = await treasuryApi.removeTransaction(deleting.transaction!.id);
            if (o.kind !== 'ok') return problem(o, 'eliminar a transação');
            setReport(o.data);
          }}
        >
          <p>
            Eliminar <strong>{deleting.transaction.description}</strong> ({euros(deleting.transaction.amount)})?
          </p>
          <p className="warning">
            <Icon name="warning" />
            Não dá para desfazer.
          </p>
        </AsyncConfirmDialog>
      )}
      {history && <HistoryDialog reportId={r.id} onClose={() => setHistory(false)} />}
    </section>
  );
}
