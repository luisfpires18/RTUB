// The "Vou / Não vou" dialog of /meetings.
import { useId, useState, type FormEvent } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { meetingsApi } from '../data/meetingsApi';
import type { MeetingCard } from '../data/meetingsTypes';
import { MAX_NOTES, problem, type Errors } from './MeetingUi';
import { Banner } from './ui/Banner';
import { FormField } from './ui/FormField';
import { DialogFooter } from './ui/DialogFooter';

export function ParticipationDialog({
  card,
  willAttend,
  onClose,
  onSaved,
}: {
  card: MeetingCard;
  willAttend: boolean;
  onClose: () => void;
  onSaved: () => void;
}) {
  const existing = card.myParticipation;
  const [notes, setNotes] = useState(existing?.notes ?? '');
  const [errors, setErrors] = useState<Errors>({});
  const [banner, setBanner] = useState<string>();
  const [busy, setBusy] = useState(false);
  const id = useId();
  const title = existing
    ? willAttend
      ? 'Editar Confirmação'
      : 'Editar Impossibilidade'
    : willAttend
      ? 'Confirmar Participação'
      : 'Registar Impossibilidade';

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setBanner(undefined);
    const o = await meetingsApi.respond(card.id, willAttend, notes.trim() ? notes : null);
    setBusy(false);
    if (o.kind === 'ok') onSaved();
    else if (o.kind === 'invalid') setErrors(o.errors);
    else setBanner(o.kind === 'closed' ? 'Já não é possível responder a esta reunião.' : problem(o, 'guardar a resposta'));
  };

  return (
    <Dialog
      title={title}
      onClose={onClose}
      footer={
        <DialogFooter form={`${id}-form`} busy={busy} label={existing ? 'Guardar' : 'Confirmar'} onClose={onClose} />
      }
    >
      <form id={`${id}-form`} className="form mtg-form" onSubmit={submit} noValidate>
        <p className="mtg-answer-for">
          <span className={willAttend ? 'pill pill--yes' : 'pill pill--no'}>
            <Icon name={willAttend ? 'checkCircle' : 'xCircle'} />
            {willAttend ? 'Vou' : 'Não vou'}
          </span>
          <strong>{card.title}</strong>
        </p>
        <Banner text={banner} />
        <FormField id={`${id}-notes`} label={willAttend ? 'Notas (opcional)' : 'Motivo (opcional)'} error={errors.notes} alert>
          <textarea
            id={`${id}-notes`}
            rows={4}
            maxLength={MAX_NOTES}
            placeholder={willAttend ? 'Informações adicionais...' : 'Motivo da impossibilidade...'}
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
          />
        </FormField>
      </form>
    </Dialog>
  );
}
