// Treasury report transaction and history dialogs.
import { useEffect, useId, useState, type FormEvent } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { isoDay, problem, treasuryApi, type Activity, type History, type Report, type Transaction } from '../data/treasuryApi';
import { today } from '../data/format';
import { PAGE } from '../data/treasuryReportView';
import { Loading } from './ui/Loading';
import { FormField } from './ui/FormField';
import { DialogFooter } from './ui/DialogFooter';
import { Banner } from './ui/Banner';

const MAX_RECEIPT = 10 * 1024 * 1024;

/** "Nova transação" / "Editar transação": date, description, category, type, amount, optional receipt (image or PDF ≤ 10 MB). */
export function TransactionDialog({
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
      footer={<DialogFooter form={`${fid}-form`} busy={busy} disabled={!description.trim() || !category.trim() || !amount || !date} onClose={onClose} />}
    >
      <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
        <Banner text={errors.form} />
        <div className="form__row">
          <FormField alert id={`${fid}-date`} label="Data" error={errors.date}>
            <input id={`${fid}-date`} type="date" value={date} onChange={(e) => setDate(e.target.value)} />
          </FormField>
          <FormField alert id={`${fid}-type`} label="Tipo" error={errors.type}>
            <span className="control control--select">
              <select id={`${fid}-type`} value={type} onChange={(e) => setType(e.target.value)}>
                <option value="Income">Receita</option>
                <option value="Expense">Despesa</option>
              </select>
            </span>
          </FormField>
        </div>
        <FormField alert id={`${fid}-desc`} label="Descrição" error={errors.description}>
          <input id={`${fid}-desc`} type="text" maxLength={500} value={description} onChange={(e) => setDescription(e.target.value)} />
        </FormField>
        <div className="form__row">
          <FormField alert id={`${fid}-cat`} label="Categoria" error={errors.category}>
            <input id={`${fid}-cat`} type="text" maxLength={100} value={category} onChange={(e) => setCategory(e.target.value)} />
          </FormField>
          <FormField alert id={`${fid}-amount`} label="Valor (€)" error={errors.amount}>
            <input id={`${fid}-amount`} type="number" inputMode="decimal" min="0.01" step="0.01" value={amount} onChange={(e) => setAmount(e.target.value)} />
          </FormField>
        </div>
        <FormField alert id={`${fid}-receipt`} label="Recibo (opcional)" error={errors.receipt}>
          <input id={`${fid}-receipt`} type="file" accept="image/*,.pdf,application/pdf" onChange={(e) => pick(e.target.files?.[0] ?? null)} />
          <p className="form__hint">Uma imagem (JPG, PNG, …) ou um PDF, até 10 MB.</p>
        </FormField>
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
export function HistoryDialog({ reportId, onClose }: { reportId: number; onClose: () => void }) {
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
        <Banner text="Não foi possível abrir o histórico." />
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
