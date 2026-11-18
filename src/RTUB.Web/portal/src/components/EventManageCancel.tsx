// Delete, cancel and reactivate event dialogs.

import { useId, useState } from 'react';
import { Dialog } from './Dialog';
import { dateLabel, eventsApi, timeLabel, type EventSummary } from '../data/eventsApi';
import { Icon } from './icons';
import { failure, EventLine, members, useAudience } from './EventManageShared';
import { FieldError } from './ui/FieldError';
import { AnswerActions } from './ui/AnswerActions';

/** Confirms a delete, naming the event; the database also drops its answers, prizes, videos and repertoire. */
export function DeleteEventDialog({ event, onClose, onDeleted }: { event: EventSummary; onClose: () => void; onDeleted: () => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const time = timeLabel(event.time);

  const remove = async () => {
    setBusy(true);
    const outcome = await eventsApi.deleteEvent(event.id);
    setBusy(false);
    if (outcome.kind === 'ok' || outcome.kind === 'notfound') {
      onDeleted();
      onClose();
    } else if (outcome.kind === 'inuse') setError('Esta atuação tem encomendas NERBA associadas e não pode ser apagada.');
    else if (outcome.kind === 'forbidden' || outcome.kind === 'signin') setError('Não tem permissão para apagar atuações.');
    else setError('Não foi possível apagar. Tente outra vez daqui a pouco.');
  };

  return (
    <Dialog title="Apagar atuação" onClose={onClose}>
      <div className="answer">
        <p className="answer-event">
          <strong>{event.name}</strong>
          <span>
            {dateLabel(event)}
            {time && ` · ${time}`} · {event.location}
          </span>
        </p>
        <p className="warning">
          <Icon name="warning" />
          Apaga também a imagem, as inscrições, os prémios, os vídeos, o repertório e a discussão desta atuação. Não dá para desfazer.
        </p>
        <FieldError text={error} />
        <AnswerActions cancelLabel="Cancelar" onCancel={onClose} label="Apagar" onConfirm={remove} busy={busy} danger />
      </div>
    </Dialog>
  );
}
/**
 * Cancels an upcoming event: a reason is required, every inscrição is deleted,
 * and an email to the members with email notifications on is opt-in. `onDone` refreshes the page.
 */
export function CancelEventDialog({ event, onClose, onDone }: { event: EventSummary; onClose: () => void; onDone: () => void }) {
  const [reason, setReason] = useState('');
  const [notify, setNotify] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [warning, setWarning] = useState<string>();
  const audience = useAudience(event.id);
  const id = useId();
  const going = event.member?.goingCount ?? 0;

  const confirm = async () => {
    if (!reason.trim()) return setError('Indique o motivo do cancelamento.');
    setBusy(true);
    setError(undefined);
    const o = await eventsApi.cancelEvent(event.id, reason.trim(), notify);
    setBusy(false);
    if (o.kind === 'ok') {
      onDone();
      if (o.data.warning) setWarning(o.data.warning);
      else onClose();
      return;
    }
    setError(o.kind === 'invalid' ? Object.values(o.errors)[0] : failure(o, 'cancelar'));
  };

  return (
    <Dialog title="Cancelar atuação" onClose={onClose}>
      <div className="answer">
        <EventLine event={event} />
        {warning ? (
          <>
            <p className="warning" role="alert">
              <Icon name="warning" />
              {warning}
            </p>
            <div className="answer__actions">
              <button type="button" className="btn btn--primary" onClick={onClose}>
                Fechar
              </button>
            </div>
          </>
        ) : (
          <>
            <p className="warning">
              <Icon name="warning" />
              Cancelar apaga todas as inscrições desta atuação{going > 0 && ` (${going === 1 ? '1 confirmado' : `${going} confirmados`})`}.
              Reativar depois não as recupera.
            </p>
            <div className={error ? 'form__field form__field--error' : 'form__field'}>
              <label htmlFor={id}>Motivo do cancelamento</label>
              <textarea id={id} rows={3} maxLength={1000} value={reason} onChange={(e) => setReason(e.target.value)} disabled={busy} />
              <p className="form__hint">Os membros veem o motivo na página da atuação.</p>
            </div>
            <label className="form__check">
              <input type="checkbox" checked={notify} onChange={(e) => setNotify(e.target.checked)} disabled={busy} />
              Avisar por email
              {audience && ` (${members(audience.emailSubscribed)} com notificações por email ativas)`}
            </label>
            <FieldError text={error} />
            <AnswerActions cancelLabel="Voltar" onCancel={onClose} label="Cancelar atuação" onConfirm={confirm} busy={busy} danger />
          </>
        )}
      </div>
    </Dialog>
  );
}

/** Reactivates a cancelled upcoming event. No one is notified and the deleted inscrições stay deleted. */
export function ReactivateEventDialog({ event, onClose, onDone }: { event: EventSummary; onClose: () => void; onDone: () => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();

  const confirm = async () => {
    setBusy(true);
    const o = await eventsApi.reactivateEvent(event.id);
    setBusy(false);
    if (o.kind === 'ok') {
      onDone();
      onClose();
    } else setError(failure(o, 'reativar'));
  };

  return (
    <Dialog title="Reativar atuação" onClose={onClose}>
      <div className="answer">
        <EventLine event={event} />
        <p>
          A atuação volta à agenda como confirmada e aceita inscrições outra vez. Ninguém é notificado e as inscrições
          apagadas ao cancelar não voltam.
        </p>
        <FieldError text={error} />
        <AnswerActions cancelLabel="Voltar" onCancel={onClose} label="Reativar" onConfirm={confirm} busy={busy} />
      </div>
    </Dialog>
  );
}
