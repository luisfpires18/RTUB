// The create / edit board dialog.
import { useEffect, useId, useState, type FormEvent } from 'react';
import { Loading } from './AppShared';
import { Dialog } from './Dialog';
import { logisticsApi, problem, type BoardInput, type BoardSummary, type LogisticsEvent, type Visibility } from '../data/logisticsApi';
import { AccessFields, person, type Person } from './LogisticsAccessFields';
import { EventField } from './LogisticsEventField';

/** "Criar quadro" / "Editar quadro": name, description, event and (task 067) responsible member, visibility and access list. */
export function BoardDialog({ board, onClose, onSaved }: { board?: BoardSummary; onClose: () => void; onSaved: () => void }) {
  const [name, setName] = useState(board?.name ?? '');
  const [description, setDescription] = useState(board?.description ?? '');
  const [event, setEvent] = useState<LogisticsEvent | null>(board?.event ?? null);
  const [owner, setOwner] = useState<Person | null>(board?.owner ? person(board.owner) : null);
  const [visibility, setVisibility] = useState<Visibility>(board?.visibility ?? 'Members');
  const [members, setMembers] = useState<Person[]>([]);
  const [ready, setReady] = useState(!board);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const fid = useId();

  // Editing: the access list comes from the board itself (managers get the ids).
  useEffect(() => {
    if (!board) return;
    logisticsApi.board(board.id).then((o) => {
      if (o.kind === 'ok') setMembers(o.data.members.map(person));
      else setErrors({ form: problem(o, 'abrir o quadro') });
      setReady(true);
    });
  }, [board?.id]);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    const input: BoardInput = {
      name,
      description,
      eventId: event?.id ?? null,
      ownerUserId: owner?.id || null,
      visibility,
      memberIds: visibility === 'Private' ? members.map((m) => m.id) : [],
    };
    const o = board ? await logisticsApi.updateBoard(board.id, input) : await logisticsApi.createBoard(input);
    setBusy(false);
    if (o.kind === 'invalid') return setErrors(o.errors);
    if (o.kind !== 'ok') return setErrors({ form: problem(o, 'guardar o quadro') });
    onSaved();
    onClose();
  };

  const err = (key: string) =>
    errors[key] && (
      <p className="form__error" role="alert">
        {errors[key]}
      </p>
    );

  return (
    <Dialog
      title={board ? 'Editar quadro' : 'Criar quadro'}
      className="lx-card-modal"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="submit" form={`${fid}-form`} className="btn btn--primary" disabled={busy || !ready || !name.trim()}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            Guardar
          </button>
        </>
      }
    >
      {!ready ? (
        <Loading label="A carregar…" />
      ) : (
        <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
          {err('form')}
          {!board && <p className="note">O quadro começa com as colunas Por fazer, Em curso, Bloqueado e Feito.</p>}
          <div className={errors.name ? 'form__field form__field--error' : 'form__field'}>
            <label htmlFor={`${fid}-name`}>Nome do quadro</label>
            <input id={`${fid}-name`} type="text" maxLength={200} value={name} onChange={(e) => setName(e.target.value)} />
            {err('name')}
          </div>
          <div className={errors.description ? 'form__field form__field--error' : 'form__field'}>
            <label htmlFor={`${fid}-desc`}>Descrição (opcional)</label>
            <textarea id={`${fid}-desc`} rows={3} maxLength={2000} value={description} onChange={(e) => setDescription(e.target.value)} />
            {err('description')}
          </div>
          <EventField event={event} setEvent={setEvent} error={errors.eventId} />
          <AccessFields
            owner={owner}
            setOwner={setOwner}
            visibility={visibility}
            setVisibility={setVisibility}
            members={members}
            setMembers={setMembers}
            errors={errors}
          />
        </form>
      )}
    </Dialog>
  );
}
