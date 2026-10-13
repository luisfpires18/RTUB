import { useEffect, useId, useState, type FormEvent } from 'react';
import { Loading } from './App';
import { portal } from './content';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import {
  day,
  euros,
  isoDay,
  matches,
  problem,
  today,
  treasuryApi,
  type Activity,
  type History,
  type Report,
  type Transaction,
} from './treasuryApi';
import { Confirm, Field, Refusal, SaveFooter, TreasuryTabs } from './TreasuryUi';

const PAGE = 10;

/** "04/10/2026" or "04/10/2026 - 06/10/2026", as the old activity badge. */
const when = (a: Activity) =>
  a.endDate && isoDay(a.endDate) !== isoDay(a.startDate) ? `${day(a.startDate)} - ${day(a.endDate)}` : day(a.startDate);

/**
 * /treasury/reports/{id} - one annual report (React track 024; was the Blazor /finance/report/{id}). Treasury members:
 * money totals, activities with their transactions and receipts, PDF. Managers (Admin, Owner, treasury-team Mod) edit
 * bank / cash, activities and transactions of a draft, and lock / unlock activities; Admin and Owner see the history.
 */
export default function TreasuryReport({ reportId }: { reportId: number }) {
  const [report, setReport] = useState<Report | 'signin' | 'forbidden' | 'failed' | 'missing'>();
  const [message, setMessage] = useState<string>();
  const [balance, setBalance] = useState<'bank' | 'cash'>();
  const [activityForm, setActivityForm] = useState<{ activity?: Activity }>();
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
        <a className="back-link" href={portal.treasury}>
          <Icon name="arrow" />
          Tesouraria
        </a>
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
      <a className="back-link" href={portal.treasury}>
        <Icon name="arrow" />
        Tesouraria
      </a>
      <header className="page__head events-page__head">
        <div>
          <p className="eyebrow">Ano letivo {fy}</p>
          <h1 id="tr-title" className="page__title">
            {r.title}
          </h1>
          <p className="tr-badges">
            <span className={r.isPublished ? 'pill pill--yes' : 'pill pill--wait'}>{r.isPublished ? 'Publicado' : 'Rascunho'}</span>
          </p>
          {r.summary && <p className="page__lead">{r.summary}</p>}
        </div>
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
      </header>

      <TreasuryTabs current="reports" fiscalYear={fy} />

      {r.isPublished && (
        <p className="notice" role="status">
          Relatório publicado: só de leitura.
        </p>
      )}
      {message && (
        <p className="form__banner" role="alert">
          {message}
        </p>
      )}

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
          <p className="note">Nenhuma atividade criada ainda.</p>
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
      {activityForm && <ActivityDialog reportId={r.id} activity={activityForm.activity} onClose={() => setActivityForm(undefined)} onSaved={setReport} />}
      {transactionForm && (
        <TransactionDialog
          activity={transactionForm.activity}
          transaction={transactionForm.transaction}
          onClose={() => setTransactionForm(undefined)}
          onSaved={setReport}
        />
      )}
      {deleting?.activity && (
        <Confirm
          title="Eliminar atividade"
          action="Eliminar"
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
        </Confirm>
      )}
      {deleting?.transaction && (
        <Confirm
          title="Eliminar transação"
          action="Eliminar"
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
        </Confirm>
      )}
      {history && <HistoryDialog reportId={r.id} onClose={() => setHistory(false)} />}
    </section>
  );
}

function ActivityPanel({
  activity: a,
  report,
  onLock,
  onEdit,
  onDelete,
  onAdd,
  onEditTransaction,
  onDeleteTransaction,
}: {
  activity: Activity;
  report: Report;
  onLock: () => void;
  onEdit: () => void;
  onDelete: () => void;
  onAdd: () => void;
  onEditTransaction: (t: Transaction) => void;
  onDeleteTransaction: (t: Transaction) => void;
}) {
  const [search, setSearch] = useState('');
  const [limit, setLimit] = useState(PAGE);
  const id = useId();
  const editable = report.canManage && !report.isPublished;
  const shown = a.transactions.filter((t) => matches(search, t.description, t.category));

  return (
    <details className="tr-activity">
      <summary className="tr-activity__head">
        <span className="tr-activity__name">
          {a.isLocked && <Icon name="lock" />}
          <strong>{a.name}</strong>
        </span>
        <span className="tr-activity__meta">
          <span className="tag">{when(a)}</span>
          <span className={a.balance >= 0 ? 'pill pill--yes' : 'pill pill--no'}>Saldo {euros(a.balance)}</span>
        </span>
        <Icon name="chevronDown" className="lx-completed__chevron" />
      </summary>
      <div className="tr-activity__body">
        {report.canManage && (
          <div className="tr-activity__tools">
            <button type="button" className="btn btn--ghost btn--sm" onClick={onLock}>
              <Icon name={a.isLocked ? 'unlock' : 'lock'} />
              {a.isLocked ? 'Desbloquear' : 'Bloquear'}
            </button>
            {editable && (
              <>
                <button type="button" className="btn btn--ghost btn--sm" onClick={onEdit}>
                  <Icon name="pencil" />
                  Editar
                </button>
                <button type="button" className="btn btn--ghost btn--sm" onClick={onDelete}>
                  <Icon name="trash" />
                  Eliminar
                </button>
              </>
            )}
          </div>
        )}
        {a.description && <p className="tr-muted tr-pre">{a.description}</p>}
        <dl className="tr-stats tr-stats--compact">
          <div>
            <dt>Receitas</dt>
            <dd className="tr-in">{euros(a.income)}</dd>
          </div>
          <div>
            <dt>Despesas</dt>
            <dd className="tr-out">{euros(a.expenses)}</dd>
          </div>
        </dl>

        <div className="tr-section-head">
          <h3 className="tr-subtitle">Transações</h3>
          {editable && !a.isLocked && (
            <button type="button" className="btn btn--primary btn--sm" onClick={onAdd}>
              <Icon name="plus" />
              Nova transação
            </button>
          )}
        </div>
        {a.isLocked && (
          <p className="note">Atividade bloqueada: não é possível adicionar transações.</p>
        )}

        {a.transactions.length === 0 ? (
          <p className="note">Sem transações.</p>
        ) : (
          <>
            <label className="control control--sm" htmlFor={id}>
              <span className="sr-only">Procurar transação em {a.name}</span>
              <Icon name="search" />
              <input
                id={id}
                type="search"
                placeholder="Procurar por descrição ou categoria"
                value={search}
                onChange={(e) => {
                  setSearch(e.target.value);
                  setLimit(PAGE);
                }}
              />
            </label>
            {search && (
              <p className="note">
                A mostrar {shown.length} de {a.transactions.length} transações.
              </p>
            )}
            <ul className="tr-transactions">
              {shown.slice(0, limit).map((t) => (
                <li key={t.id} className="tr-transaction">
                  <span className="tr-transaction__main">
                    <strong>{t.description}</strong>
                    <span className="tr-muted">
                      {day(t.date)} · {t.category}
                    </span>
                  </span>
                  <span className={t.type === 'Income' ? 'tr-in tr-transaction__amount' : 'tr-out tr-transaction__amount'}>
                    {t.type === 'Income' ? '+' : '−'}
                    {euros(t.amount)}
                  </span>
                  <span className="tr-transaction__tools">
                    {t.receiptUrl && (
                      <a className="icon-btn icon-btn--sm" href={t.receiptUrl} target="_blank" rel="noopener noreferrer">
                        <Icon name="file" />
                        <span className="sr-only">Ver o recibo de {t.description}</span>
                      </a>
                    )}
                    {editable && (
                      <>
                        <button type="button" className="icon-btn icon-btn--sm" onClick={() => onEditTransaction(t)}>
                          <Icon name="pencil" />
                          <span className="sr-only">Editar {t.description}</span>
                        </button>
                        <button type="button" className="icon-btn icon-btn--sm icon-btn--danger" onClick={() => onDeleteTransaction(t)}>
                          <Icon name="trash" />
                          <span className="sr-only">Eliminar {t.description}</span>
                        </button>
                      </>
                    )}
                  </span>
                </li>
              ))}
            </ul>
            {shown.length === 0 && <p className="note">Nenhuma transação encontrada.</p>}
            {shown.length > limit && (
              <button type="button" className="btn btn--ghost btn--sm tr-more" onClick={() => setLimit(limit + PAGE)}>
                Mostrar mais ({shown.length - limit})
              </button>
            )}
          </>
        )}
      </div>
    </details>
  );
}

/** "Editar Dinheiro no Banco / em Caixa": one value, any sign; 0 clears it (as before). */
function BalanceDialog({ report, kind, onClose, onSaved }: { report: Report; kind: 'bank' | 'cash'; onClose: () => void; onSaved: (r: Report) => void }) {
  const [value, setValue] = useState(String(kind === 'bank' ? report.totals.bank : report.totals.cash));
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const fid = useId();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const n = Number(value.replace(',', '.'));
    if (value.trim() === '' || Number.isNaN(n)) return setError('Indique um valor em euros.');
    setBusy(true);
    const o = await treasuryApi.setBalance(report.id, kind, n);
    setBusy(false);
    if (o.kind !== 'ok') return setError(problem(o, 'guardar o valor'));
    onSaved(o.data);
    onClose();
  };

  return (
    <Dialog
      title={kind === 'bank' ? 'Editar dinheiro no banco' : 'Editar dinheiro em caixa'}
      onClose={onClose}
      footer={<SaveFooter formId={`${fid}-form`} busy={busy} onClose={onClose} />}
    >
      <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
        <Field id={`${fid}-value`} label="Valor (€)" error={error}>
          <input id={`${fid}-value`} type="number" inputMode="decimal" step="0.01" value={value} onChange={(e) => setValue(e.target.value)} />
        </Field>
      </form>
    </Dialog>
  );
}

/** "Nova atividade" / "Editar atividade": name, one date or a range, description. */
function ActivityDialog({ reportId, activity, onClose, onSaved }: { reportId: number; activity?: Activity; onClose: () => void; onSaved: (r: Report) => void }) {
  const [name, setName] = useState(activity?.name ?? '');
  const [range, setRange] = useState(!!activity?.endDate);
  const [start, setStart] = useState(isoDay(activity?.startDate ?? null) || today());
  const [end, setEnd] = useState(isoDay(activity?.endDate ?? null));
  const [description, setDescription] = useState(activity?.description ?? '');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const fid = useId();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    const input = { name, startDate: start, endDate: range && end ? end : null, description };
    const o = activity ? await treasuryApi.updateActivity(activity.id, input) : await treasuryApi.createActivity(reportId, input);
    setBusy(false);
    if (o.kind === 'invalid') return setErrors(o.errors);
    if (o.kind !== 'ok') return setErrors({ form: problem(o, 'guardar a atividade') });
    onSaved(o.data);
    onClose();
  };

  return (
    <Dialog
      title={activity ? 'Editar atividade' : 'Nova atividade'}
      onClose={onClose}
      footer={<SaveFooter formId={`${fid}-form`} busy={busy} disabled={!name.trim() || !start} onClose={onClose} />}
    >
      <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
        {errors.form && (
          <p className="form__banner" role="alert">
            {errors.form}
          </p>
        )}
        <Field id={`${fid}-name`} label="Nome" error={errors.name}>
          <input id={`${fid}-name`} type="text" maxLength={200} value={name} onChange={(e) => setName(e.target.value)} />
        </Field>
        <label className="check">
          <input type="checkbox" checked={range} onChange={(e) => setRange(e.target.checked)} />
          Intervalo de datas (em vez de data única)
        </label>
        <div className="form__row">
          <Field id={`${fid}-start`} label={range ? 'Data de início' : 'Data'} error={errors.startDate}>
            <input id={`${fid}-start`} type="date" value={start} onChange={(e) => setStart(e.target.value)} />
          </Field>
          {range && (
            <Field id={`${fid}-end`} label="Data de fim (opcional)" error={errors.endDate}>
              <input id={`${fid}-end`} type="date" min={start} value={end} onChange={(e) => setEnd(e.target.value)} />
            </Field>
          )}
        </div>
        <Field id={`${fid}-desc`} label="Descrição (opcional)" error={errors.description}>
          <textarea id={`${fid}-desc`} rows={3} maxLength={1000} value={description} onChange={(e) => setDescription(e.target.value)} />
        </Field>
      </form>
    </Dialog>
  );
}

const MAX_RECEIPT = 10 * 1024 * 1024;

/** "Nova transação" / "Editar transação": date, description, category, type, amount, optional receipt (image or PDF ≤ 10 MB). */
function TransactionDialog({
  activity,
  transaction,
  onClose,
  onSaved,
}: {
  activity: Activity;
  transaction?: Transaction;
  onClose: () => void;
  onSaved: (r: Report) => void;
}) {
  const [date, setDate] = useState(isoDay(transaction?.date ?? null) || today());
  const [description, setDescription] = useState(transaction?.description ?? '');
  const [category, setCategory] = useState(transaction?.category ?? '');
  const [type, setType] = useState<string>(transaction?.type ?? 'Expense');
  const [amount, setAmount] = useState(transaction ? String(transaction.amount) : '');
  const [receipt, setReceipt] = useState<File | null>(null);
  const [removeReceipt, setRemoveReceipt] = useState(false);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const fid = useId();

  const pick = (file: File | null) => {
    if (file && file.size > MAX_RECEIPT) {
      setReceipt(null);
      return setErrors({ ...errors, receipt: 'O ficheiro é demasiado grande. O tamanho máximo é 10MB.' });
    }
    setErrors({ ...errors, receipt: '' });
    setReceipt(file);
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    const input = { date, description, category, amount, type, removeReceipt, receipt };
    const o = transaction ? await treasuryApi.updateTransaction(transaction.id, input) : await treasuryApi.createTransaction(activity.id, input);
    setBusy(false);
    if (o.kind === 'invalid') return setErrors(o.errors);
    if (o.kind !== 'ok') return setErrors({ form: problem(o, 'guardar a transação') });
    onSaved(o.data);
    onClose();
  };

  return (
    <Dialog
      title={transaction ? 'Editar transação' : `Nova transação · ${activity.name}`}
      onClose={onClose}
      footer={<SaveFooter formId={`${fid}-form`} busy={busy} disabled={!description.trim() || !category.trim() || !amount || !date} onClose={onClose} />}
    >
      <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
        {errors.form && (
          <p className="form__banner" role="alert">
            {errors.form}
          </p>
        )}
        <div className="form__row">
          <Field id={`${fid}-date`} label="Data" error={errors.date}>
            <input id={`${fid}-date`} type="date" value={date} onChange={(e) => setDate(e.target.value)} />
          </Field>
          <Field id={`${fid}-type`} label="Tipo" error={errors.type}>
            <span className="control control--select">
              <select id={`${fid}-type`} value={type} onChange={(e) => setType(e.target.value)}>
                <option value="Income">Receita</option>
                <option value="Expense">Despesa</option>
              </select>
            </span>
          </Field>
        </div>
        <Field id={`${fid}-desc`} label="Descrição" error={errors.description}>
          <input id={`${fid}-desc`} type="text" maxLength={500} value={description} onChange={(e) => setDescription(e.target.value)} />
        </Field>
        <div className="form__row">
          <Field id={`${fid}-cat`} label="Categoria" error={errors.category}>
            <input id={`${fid}-cat`} type="text" maxLength={100} value={category} onChange={(e) => setCategory(e.target.value)} />
          </Field>
          <Field id={`${fid}-amount`} label="Valor (€)" error={errors.amount}>
            <input id={`${fid}-amount`} type="number" inputMode="decimal" min="0.01" step="0.01" value={amount} onChange={(e) => setAmount(e.target.value)} />
          </Field>
        </div>
        <Field id={`${fid}-receipt`} label="Recibo (opcional)" error={errors.receipt}>
          <input id={`${fid}-receipt`} type="file" accept="image/*,.pdf,application/pdf" onChange={(e) => pick(e.target.files?.[0] ?? null)} />
          <p className="form__hint">Uma imagem (JPG, PNG, …) ou um PDF, até 10 MB.</p>
        </Field>
        {transaction?.receiptUrl && (
          <div className="tr-receipt">
            <a href={transaction.receiptUrl} target="_blank" rel="noopener noreferrer">
              <Icon name="file" /> Recibo atual
            </a>
            {!receipt && (
              <label className="check">
                <input type="checkbox" checked={removeReceipt} onChange={(e) => setRemoveReceipt(e.target.checked)} />
                Remover o recibo
              </label>
            )}
          </div>
        )}
      </form>
    </Dialog>
  );
}

const ACTIONS: Record<string, { label: string; tone: string }> = {
  Created: { label: 'Criado', tone: 'pill pill--yes' },
  Modified: { label: 'Modificado', tone: 'pill pill--wait' },
  Deleted: { label: 'Eliminado', tone: 'pill pill--no' },
};

/** "Histórico de transações" (Admin, Owner): who created, changed or deleted which transaction, newest first. */
function HistoryDialog({ reportId, onClose }: { reportId: number; onClose: () => void }) {
  const [page, setPage] = useState(1);
  const [data, setData] = useState<History | 'failed'>();

  useEffect(() => {
    setData(undefined);
    treasuryApi.history(reportId, page, PAGE).then((o) => setData(o.kind === 'ok' ? o.data : 'failed'));
  }, [reportId, page]);

  const pages = data && data !== 'failed' ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;
  return (
    <Dialog title="Histórico de transações" size="lg" onClose={onClose}>
      {data === 'failed' ? (
        <p className="form__banner" role="alert">
          Não foi possível abrir o histórico.
        </p>
      ) : !data ? (
        <Loading label="A carregar o histórico…" />
      ) : data.entries.length === 0 ? (
        <p className="note">Não há registos de transações neste relatório.</p>
      ) : (
        <>
          <ul className="tr-history">
            {data.entries.map((e, i) => (
              <li key={`${e.timestamp}-${i}`} className="tr-history__row">
                <span className="tr-transaction__main">
                  <strong>{e.description}</strong>
                  <span className="tr-muted">
                    {new Date(e.timestamp).toLocaleString('pt-PT', { dateStyle: 'short', timeStyle: 'short' })} · {e.activityName}
                  </span>
                </span>
                <span className="tr-history__who">
                  <span className={ACTIONS[e.action]?.tone ?? 'pill'}>{ACTIONS[e.action]?.label ?? e.action}</span>
                  <small>{e.userName}</small>
                </span>
              </li>
            ))}
          </ul>
          {pages > 1 && (
            <div className="pager">
              <button type="button" className="btn btn--ghost btn--sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>
                Anterior
              </button>
              <span>
                Página {page} de {pages}
              </span>
              <button type="button" className="btn btn--ghost btn--sm" disabled={page >= pages} onClick={() => setPage(page + 1)}>
                Seguinte
              </button>
            </div>
          )}
        </>
      )}
    </Dialog>
  );
}
