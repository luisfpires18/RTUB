// Logistics dialogs: confirm, create / edit column, create card.
import { useId, useState, type FormEvent, type ReactNode } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import type { Outcome } from '../data/api';
import { logisticsApi, problem, STATUS, STATUSES, type List, type MemberOption, type Status } from '../data/logisticsApi';
import { MemberPicker } from './LogisticsFields';
import { FieldError } from './ui/FieldError';
import { PersonChip } from './ui/PersonChip';
import { FormField } from './ui/FormField';
import { DialogFooter } from './ui/DialogFooter';
import { AsyncConfirmDialog } from './ui/ConfirmDialog';

export function ConfirmDialog({
  title,
  what,
  warning,
  action,
  onClose,
  onDone,
}: {
  title: string;
  what: ReactNode;
  warning: string;
  action: () => Promise<Outcome<void>>;
  onClose: () => void;
  onDone: () => void;
}) {
  const run = async () => {
    const o = await action();
    if (o.kind !== 'ok' && o.kind !== 'notfound') return problem(o, 'eliminar');
    onDone();
  };
  return (
    <AsyncConfirmDialog title={title} confirmLabel="Eliminar" danger onConfirm={run} onClose={onClose}>
      <p>Eliminar {what}?</p>
      <p className="warning">
        <Icon name="warning" />
        {warning}
      </p>
    </AsyncConfirmDialog>
  );
}

/**
 * A column: name and state - every card in the column has it. Changing a column's state changes its cards'
 * status at once (the dialog says how many).
 */
export function ListDialog({ boardId, list, onClose, onSaved }: { boardId: number; list?: List; onClose: () => void; onSaved: () => void }) {
  const [name, setName] = useState(list?.name ?? '');
  const [state, setState] = useState<Status>(list?.state ?? 'Todo');
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const fid = useId();
  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    const o = list ? await logisticsApi.renameList(list.id, name, state) : await logisticsApi.createList(boardId, name, state);
    setBusy(false);
    if (o.kind !== 'ok') return setError(problem(o, 'guardar a coluna'));
    onSaved();
    onClose();
  };
  return (
    <Dialog title={list ? 'Editar coluna' : 'Nova coluna'} onClose={onClose} footer={<DialogFooter onClose={onClose} busy={busy} form={`${fid}-form`} label="Guardar" disabled={!name.trim()} />}>
      <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
        <FormField id={`${fid}-name`} label="Nome da coluna" error={error} alert>
          <input id={`${fid}-name`} type="text" maxLength={100} value={name} onChange={(e) => setName(e.target.value)} autoFocus />
        </FormField>
        <fieldset className="form__field">
          <legend>Estado dos cartões nesta coluna</legend>
          <div className="lx-segment lx-segment--four" role="radiogroup">
            {STATUSES.map((s) => (
              <button key={s} type="button" role="radio" aria-checked={state === s} aria-pressed={state === s} className={`lx-segment--${s.toLowerCase()}`} onClick={() => setState(s)}>
                {STATUS[s].label}
                <small>{STATUS[s].name}</small>
              </button>
            ))}
          </div>
          {list && state !== list.state && list.cards.length > 0 && (
            <p className="note">
              Os {list.cards.length} cartões desta coluna passam a {STATUS[state].name}.
            </p>
          )}
        </fieldset>
      </form>
    </Dialog>
  );
}

/** "Criar cartão": title, description and the optional "Atribuir a" member. Opens the details after. */
export function CreateCardDialog({ boardId, list, onClose, onCreated }: { boardId: number; list: List; onClose: () => void; onCreated: (id: number) => void }) {
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [assignee, setAssignee] = useState<MemberOption | null>(null);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const fid = useId();
  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    const o = await logisticsApi.createCard(list.id, { title, description, assignedToUserId: assignee?.id ?? null });
    setBusy(false);
    if (o.kind === 'invalid') return setErrors(o.errors);
    if (o.kind !== 'ok') return setErrors({ form: problem(o, 'criar o cartão') });
    onCreated(o.data.card.id);
  };
  return (
    <Dialog title={`Novo cartão · ${list.name}`} onClose={onClose} footer={<DialogFooter onClose={onClose} busy={busy} form={`${fid}-form`} label="Criar" disabled={!title.trim()} />}>
      <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
        <FieldError text={errors.form} />
        <FormField id={`${fid}-title`} label="Título" error={errors.title} alert>
          <input id={`${fid}-title`} type="text" maxLength={200} value={title} onChange={(e) => setTitle(e.target.value)} autoFocus />
        </FormField>
        <FormField id={`${fid}-desc`} label="Descrição (opcional)" error={errors.description} alert>
          <textarea id={`${fid}-desc`} rows={3} maxLength={2000} value={description} onChange={(e) => setDescription(e.target.value)} />
        </FormField>
        <FormField id={`${fid}-assignee`} label="Atribuir a (opcional)" heading>
          {assignee ? (
            <PersonChip name={assignee.displayName} avatar={assignee.avatarUrl} onRemove={() => setAssignee(null)} removeLabel="Remover" />
          ) : (
            <MemberPicker label="Atribuir a" boardId={boardId} onPick={setAssignee} />
          )}
          <FieldError text={errors.assignedToUserId} />
        </FormField>
      </form>
    </Dialog>
  );
}
