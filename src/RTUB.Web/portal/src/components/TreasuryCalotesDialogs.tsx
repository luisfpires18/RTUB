// Treasury calotes debts and debt dialogs.
import { useId, useState, type FormEvent } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { isoDay, problem, treasuryApi, type Calotes, type DebtGroup, type Person } from '../data/treasuryApi';
import { MemberPicker } from './TreasuryUi';
import { day, euros } from '../data/format';
import { type Debt } from '../data/treasuryCalotesView';
import { PersonChip } from './ui/PersonChip';
import { FormField } from './ui/FormField';
import { DialogFooter } from './ui/DialogFooter';
import { Spinner } from './ui/Spinner';
import { Banner } from './ui/Banner';
import { FieldError } from './ui/FieldError';

/** One member's debts of the year, with the commitment date (managers change it) and, for managers, edit / remove. */
export function DebtsDialog({
  group,
  calotes,
  onClose,
  onSaved,
  onEdit,
  onDelete,
  onAdd,
}: {
  group: DebtGroup;
  calotes: Calotes;
  onClose: () => void;
  onSaved: (c: Calotes) => void;
  onEdit: (d: Debt) => void;
  onDelete: (d: Debt) => void;
  onAdd: () => void;
}) {
  const [until, setUntil] = useState(isoDay(group.compromisedUntil));
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string>();
  const fid = useId();

  const saveCommitment = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    const o = await treasuryApi.setCommitment(calotes.fiscalYear!, group.member.id!, until || null);
    setBusy(false);
    if (o.kind !== 'ok') return setMessage(problem(o, 'guardar o compromisso'));
    setMessage('Compromisso guardado.');
    onSaved(o.data);
  };

  return (
    <Dialog title={`Calotes · ${group.member.displayName}`} size="lg" onClose={onClose}>
      <div className="tr-debts">
        <p className="tr-debts__total">
          Total em dívida <span className="pill pill--no">{euros(group.total)}</span>
        </p>

        <section aria-labelledby={`${fid}-commit`}>
          <h3 id={`${fid}-commit`} className="tr-subtitle">
            Compromisso de pagamento
          </h3>
          {calotes.canManage ? (
            <form className="tr-inline-form" onSubmit={saveCommitment} noValidate>
              <FormField alert id={`${fid}-until`} label="Até (opcional)">
                <input id={`${fid}-until`} type="date" value={until} onChange={(e) => setUntil(e.target.value)} />
              </FormField>
              <button type="submit" className="btn btn--primary btn--sm" disabled={busy}>
                {busy && <Spinner small />}
                Guardar
              </button>
              <p className="form__hint">Enquanto esta data estiver no futuro, não são enviados lembretes de calote a este membro neste ano letivo.</p>
            </form>
          ) : (
            <p className="tr-muted">{group.compromisedUntil ? `Até ${day(group.compromisedUntil)}.` : 'Nenhum compromisso de pagamento definido.'}</p>
          )}
          {message && <p className="note" role="status">{message}</p>}
        </section>

        <section aria-labelledby={`${fid}-list`}>
          <div className="tr-section-head">
            <h3 id={`${fid}-list`} className="tr-subtitle">
              Dívidas ({group.debts.length})
            </h3>
            {calotes.canManage && (
              <button type="button" className="btn btn--ghost btn--sm" onClick={onAdd}>
                <Icon name="plus" />
                Adicionar
              </button>
            )}
          </div>
          <ul className="tr-transactions">
            {group.debts.map((d) => (
              <li key={d.id} className="tr-transaction">
                <span className="tr-transaction__main">{d.description ? <strong>{d.description}</strong> : <span className="note">Sem descrição</span>}</span>
                <span className="tr-out tr-transaction__amount">{euros(d.amount)}</span>
                {calotes.canManage && (
                  <span className="tr-transaction__tools">
                    <button type="button" className="icon-btn icon-btn--sm" onClick={() => onEdit(d)}>
                      <Icon name="pencil" />
                      <span className="sr-only">Editar calote</span>
                    </button>
                    <button type="button" className="icon-btn icon-btn--sm icon-btn--danger" onClick={() => onDelete(d)}>
                      <Icon name="trash" />
                      <span className="sr-only">Remover calote</span>
                    </button>
                  </span>
                )}
              </li>
            ))}
          </ul>
        </section>
      </div>
    </Dialog>
  );
}

/** "Adicionar calote" / "Editar calote": member (not a Leitão), amount, description. */
export function DebtDialog({
  fiscalYear,
  member: initial,
  debt,
  onClose,
  onSaved,
}: {
  fiscalYear: string;
  member?: Person;
  debt?: Debt;
  onClose: () => void;
  onSaved: (c: Calotes) => void;
}) {
  const [member, setMember] = useState<Person | undefined>(initial);
  const [amount, setAmount] = useState(debt ? String(debt.amount) : '');
  const [description, setDescription] = useState(debt?.description ?? '');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const fid = useId();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const value = amount.trim() === '' ? null : Number(amount.replace(',', '.'));
    setBusy(true);
    const o = debt
      ? await treasuryApi.updateDebt(debt.id, { amount: value, description })
      : await treasuryApi.addDebt({ fiscalYear, userId: member?.id ?? '', amount: value, description });
    setBusy(false);
    if (o.kind === 'invalid') return setErrors(o.errors);
    if (o.kind !== 'ok') return setErrors({ form: problem(o, 'guardar o calote') });
    onSaved(o.data);
    onClose();
  };

  return (
    <Dialog
      title={debt ? 'Editar calote' : `Adicionar calote · ${fiscalYear}`}
      onClose={onClose}
      footer={<DialogFooter form={`${fid}-form`} busy={busy} disabled={!member || !amount} onClose={onClose} label={debt ? 'Guardar' : 'Adicionar'} />}
    >
      <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
        <Banner text={errors.form} />
        <div className={errors.userId ? 'form__field form__field--error' : 'form__field'}>
          <span className="form__label">Membro</span>
          {member ? (
            <PersonChip name={member.displayName} avatar={member.avatarUrl} onRemove={debt || initial ? undefined : () => setMember(undefined)} />
          ) : (
            <MemberPicker forDebts label="Procurar membro" onPick={setMember} />
          )}
          <FieldError text={errors.userId} />
        </div>
        <FormField alert id={`${fid}-amount`} label="Montante (€)" error={errors.amount}>
          <input id={`${fid}-amount`} type="number" inputMode="decimal" min="0.01" step="0.01" value={amount} onChange={(e) => setAmount(e.target.value)} />
        </FormField>
        <FormField alert id={`${fid}-desc`} label="Descrição (opcional)" error={errors.description}>
          <input id={`${fid}-desc`} type="text" maxLength={500} value={description} onChange={(e) => setDescription(e.target.value)} />
        </FormField>
      </form>
    </Dialog>
  );
}
