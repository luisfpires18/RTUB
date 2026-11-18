// Dialogs that move cards: delete a column (cards move out) and move a card to a list.
import { useId, useState, type FormEvent } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { logisticsApi, problem, STATUS, type Board, type Card, type List, type MemberOption } from '../data/logisticsApi';
import { MemberPicker } from './LogisticsFields';
import { FieldError } from './ui/FieldError';
import { PersonChip } from './ui/PersonChip';
import { DialogFooter } from './ui/DialogFooter';

/** Delete a column: its cards never go with it - they move to the chosen column and take its state. */
export function DeleteListDialog({ board, list, onClose, onDone }: { board: Board; list: List; onClose: () => void; onDone: () => void }) {
  const others = board.lists.filter((l) => l.id !== list.id);
  const [target, setTarget] = useState<number | undefined>(others[0]?.id);
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const fid = useId();
  const count = list.cards.length;
  const remove = async () => {
    setBusy(true);
    const o = await logisticsApi.removeList(list.id, count > 0 ? target : undefined);
    setBusy(false);
    if (o.kind === 'ok' || o.kind === 'notfound') return (onDone(), onClose());
    setError(problem(o, 'eliminar a coluna'));
  };
  return (
    <Dialog
      title="Eliminar coluna"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="button" className="btn btn--danger" onClick={remove} disabled={busy || (count > 0 && !target)}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            Eliminar coluna
          </button>
        </>
      }
    >
      <p>
        Eliminar a coluna <strong>{list.name}</strong>?
      </p>
      {count > 0 &&
        (others.length === 0 ? (
          <p className="warning">
            <Icon name="warning" />
            Tem {count} {count === 1 ? 'cartão' : 'cartões'} e é a única coluna: crie outra primeiro.
          </p>
        ) : (
          <div className="form__field">
            <label htmlFor={`${fid}-to`}>
              {count === 1 ? 'O cartão passa para' : `Os ${count} cartões passam para`}
            </label>
            <span className="control control--select control--sm">
              <select id={`${fid}-to`} value={target} onChange={(e) => setTarget(Number(e.target.value))}>
                {others.map((l) => (
                  <option key={l.id} value={l.id}>
                    {l.name} ({STATUS[l.state].label})
                  </option>
                ))}
              </select>
            </span>
            <small className="note">E ficam com o estado dessa coluna.</small>
          </div>
        ))}
      <FieldError text={error} />
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

/** "Mover cartão": any list of the board, at any position; the touch-friendly twin of drag and drop. */
export function MoveCardDialog({
  board,
  card,
  listId,
  onClose,
  onMove,
}: {
  board: Board;
  card: Card;
  listId: number;
  onClose: () => void;
  onMove: (listId: number, position: number) => void;
}) {
  const [target, setTarget] = useState(listId);
  const others = board.lists.find((l) => l.id === target)!.cards.filter((c) => c.id !== card.id);
  const current = board.lists.find((l) => l.id === listId)!.cards.findIndex((c) => c.id === card.id);
  const [position, setPosition] = useState(target === listId ? current : others.length);
  const fid = useId();
  return (
    <Dialog
      title="Mover cartão"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose}>
            Cancelar
          </button>
          <button type="button" className="btn btn--primary" onClick={() => onMove(target, position)}>
            Mover
          </button>
        </>
      }
    >
      <p className="note">{card.title}</p>
      <div className="form">
        <div className="form__row">
          <div className="form__field">
            <label htmlFor={`${fid}-list`}>Lista</label>
            <span className="control control--select">
              <select
                id={`${fid}-list`}
                value={target}
                onChange={(e) => {
                  const next = Number(e.target.value);
                  setTarget(next);
                  setPosition(next === listId ? current : board.lists.find((l) => l.id === next)!.cards.length);
                }}
              >
                {board.lists.map((l) => (
                  <option key={l.id} value={l.id}>
                    {l.name}
                  </option>
                ))}
              </select>
            </span>
          </div>
          <div className="form__field">
            <label htmlFor={`${fid}-pos`}>Posição</label>
            <span className="control control--select">
              <select id={`${fid}-pos`} value={position} onChange={(e) => setPosition(Number(e.target.value))}>
                {Array.from({ length: others.length + 1 }, (_, i) => (
                  <option key={i} value={i}>
                    {i === 0 ? '1 (no topo)' : i === others.length ? `${i + 1} (no fim)` : i + 1}
                  </option>
                ))}
              </select>
            </span>
          </div>
        </div>
      </div>
    </Dialog>
  );
}
