// Logistics dialogs: confirm, create / edit column, create card.
import { useId, useState, type FormEvent, type ReactNode } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import type { Outcome } from '../data/eventsApi';
import { logisticsApi, problem, STATUS, STATUSES, type List, type MemberOption, type Status } from '../data/logisticsApi';
import { FieldError, Footer, MemberPicker, PersonChip } from './LogisticsFields';

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
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const run = async () => {
    setBusy(true);
    const o = await action();
    setBusy(false);
    if (o.kind === 'ok' || o.kind === 'notfound') {
      onDone();
      onClose();
    } else setError(problem(o, 'eliminar'));
  };
  return (
    <Dialog
      title={title}
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="button" className="btn btn--danger" onClick={run} disabled={busy}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            Eliminar
          </button>
        </>
      }
    >
      <p>Eliminar {what}?</p>
      <p className="warning">
        <Icon name="warning" />
        {warning}
      </p>
      {error && (
        <p className="form__banner" role="alert">
          {error}
        </p>
      )}
    </Dialog>
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
    <Dialog title={list ? 'Editar coluna' : 'Nova coluna'} onClose={onClose} footer={<Footer onClose={onClose} busy={busy} form={`${fid}-form`} label="Guardar" disabled={!name.trim()} />}>
      <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
        <div className={error ? 'form__field form__field--error' : 'form__field'}>
          <label htmlFor={`${fid}-name`}>Nome da coluna</label>
          <input id={`${fid}-name`} type="text" maxLength={100} value={name} onChange={(e) => setName(e.target.value)} autoFocus />
          <FieldError text={error} />
        </div>
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
    <Dialog title={`Novo cartão · ${list.name}`} onClose={onClose} footer={<Footer onClose={onClose} busy={busy} form={`${fid}-form`} label="Criar" disabled={!title.trim()} />}>
      <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
        <FieldError text={errors.form} />
        <div className={errors.title ? 'form__field form__field--error' : 'form__field'}>
          <label htmlFor={`${fid}-title`}>Título</label>
          <input id={`${fid}-title`} type="text" maxLength={200} value={title} onChange={(e) => setTitle(e.target.value)} autoFocus />
          <FieldError text={errors.title} />
        </div>
        <div className={errors.description ? 'form__field form__field--error' : 'form__field'}>
          <label htmlFor={`${fid}-desc`}>Descrição (opcional)</label>
          <textarea id={`${fid}-desc`} rows={3} maxLength={2000} value={description} onChange={(e) => setDescription(e.target.value)} />
          <FieldError text={errors.description} />
        </div>
        <div className="form__field">
          <span className="form__label">Atribuir a (opcional)</span>
          {assignee ? (
            <PersonChip name={assignee.displayName} avatar={assignee.avatarUrl} onRemove={() => setAssignee(null)} removeLabel="Remover" />
          ) : (
            <MemberPicker label="Atribuir a" boardId={boardId} onPick={setAssignee} />
          )}
          <FieldError text={errors.assignedToUserId} />
        </div>
      </form>
    </Dialog>
  );
}
