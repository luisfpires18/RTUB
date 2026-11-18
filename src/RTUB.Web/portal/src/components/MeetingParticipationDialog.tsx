// The "Vou / Não vou" dialog of /meetings.
import { useId, useState, type FormEvent } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { meetingsApi } from '../data/meetingsApi';
import type { MeetingCard } from '../data/meetingsTypes';
import { MAX_NOTES, problem, type Errors, fieldClass } from './MeetingUi';
import { Banner } from './ui/Banner';
import { FieldError } from './ui/FieldError';
import { Spinner } from './ui/Spinner';

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
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="submit" form={`${id}-form`} className="btn btn--primary" disabled={busy}>
            {busy && <Spinner small />}
            {existing ? 'Guardar' : 'Confirmar'}
          </button>
        </>
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
        <div className={fieldClass(errors, 'notes')}>
          <label htmlFor={`${id}-notes`}>{willAttend ? 'Notas (opcional)' : 'Motivo (opcional)'}</label>
          <textarea
            id={`${id}-notes`}
            rows={4}
            maxLength={MAX_NOTES}
            placeholder={willAttend ? 'Informações adicionais...' : 'Motivo da impossibilidade...'}
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
          />
          <FieldError text={errors.notes} />
        </div>
      </form>
    </Dialog>
  );
}
