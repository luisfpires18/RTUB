// The create / edit board dialog.
import { useEffect, useId, useState, type FormEvent } from 'react';
import { Dialog } from './Dialog';
import { logisticsApi, problem, type BoardInput, type BoardSummary, type LogisticsEvent, type Visibility } from '../data/logisticsApi';
import { AccessFields, person, type Person } from './LogisticsAccessFields';
import { EventField } from './LogisticsEventField';
import { Loading } from './ui/Loading';
import { DialogFooter } from './ui/DialogFooter';
import { FieldError } from './ui/FieldError';
import { FormField } from './ui/FormField';

/** "Criar quadro" / "Editar quadro": name, description, event and responsible member, visibility and access list. */
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

  return (
    <Dialog
      title={board ? 'Editar quadro' : 'Criar quadro'}
      className="lx-card-modal"
      onClose={onClose}
      footer={<DialogFooter form={`${fid}-form`} busy={busy} disabled={!ready || !name.trim()} onClose={onClose} />}
    >
      {!ready ? (
        <Loading label="A carregar…" />
      ) : (
        <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
          <FieldError text={errors.form} />
          {!board && <p className="note">O quadro começa com as colunas Por fazer, Em curso, Bloqueado e Feito.</p>}
          <FormField id={`${fid}-name`} label="Nome do quadro" error={errors.name} alert>
            <input id={`${fid}-name`} type="text" maxLength={200} value={name} onChange={(e) => setName(e.target.value)} />
          </FormField>
          <FormField id={`${fid}-desc`} label="Descrição (opcional)" error={errors.description} alert>
            <textarea id={`${fid}-desc`} rows={3} maxLength={2000} value={description} onChange={(e) => setDescription(e.target.value)} />
          </FormField>
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
