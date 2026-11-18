// A member's answer (inscrição) to one event: the Vou / Não vou dialog, its form and withdrawal.

import { eventActivity, CalendarOffer } from './CalendarOffer';
import { useEffect, useId, useState, type FormEvent } from 'react';
import { Dialog } from './Dialog';
import { portal } from '../data/content';
import { eventsApi, dateLabel, timeLabel, type EventSummary, type EventEnrollment } from '../data/eventsApi';
import { Icon } from './icons';
import { Loading } from './ui/Loading';
import { FieldError } from './ui/FieldError';
import { AnswerActions } from './ui/AnswerActions';
import { FormField } from './ui/FormField';

const NOTES_MAX = 1000;

// ---------- answer (inscrição) ----------

type Choice = { willAttend: boolean | null; instrument: string; notes: string };

function choiceFrom(e: EventEnrollment): Choice {
  const going = e.status === 'going';
  return {
    willAttend: e.status ? going : null,
    // Someone answering "vou" plays their primary instrument unless they pick "não vou tocar";
    // a Leitão starts without one (it is optional for them).
    instrument: going ? (e.instrument ?? '') : e.isLeitao ? '' : (e.defaultInstrument ?? ''),
    notes: e.notes ?? '',
  };
}

/**
 * A member's answer for one event (Vou / Não vou), in a modal over the agenda or the event page.
 * Open events take the answer, an instrument and a note; past events only let someone who went
 * withdraw; cancelled events take nothing. The server enforces all of it; `onSaved` gets its answer.
 */
export function EnrollmentDialog({
  eventId,
  onClose,
  onSaved,
}: {
  eventId: number;
  onClose: () => void;
  onSaved: (enrollment: EventEnrollment) => void;
}) {
  const [data, setData] = useState<EventEnrollment | 'signin' | 'missing' | 'failed'>();

  useEffect(() => {
    eventsApi.getEnrollment(eventId).then((o) =>
      setData(o.kind === 'ok' ? o.data : o.kind === 'signin' ? 'signin' : o.kind === 'notfound' ? 'missing' : 'failed'),
    );
  }, [eventId]);

  const title = typeof data === 'object' && data.state !== 'open' ? 'A minha inscrição' : 'Vais a esta atuação?';

  return (
    <Dialog title={title} onClose={onClose}>
      {data === undefined ? (
        <Loading label="A carregar…" />
      ) : data === 'signin' ? (
        <div className="answer-note">
          <p>Responder é para membros da RTUB.</p>
          <a className="btn btn--primary btn--sm" href={`${portal.login}?returnUrl=${encodeURIComponent(`${portal.event(eventId)}?respond=1`)}`}>
            Entrar
          </a>
        </div>
      ) : data === 'missing' ? (
        <p>Esta atuação já não existe.</p>
      ) : data === 'failed' ? (
        <p>Não foi possível abrir a tua inscrição agora. Tenta outra vez daqui a pouco.</p>
      ) : (
        <>
          <EventLine event={data.event} />
          {data.state === 'open' ? (
            <AnswerForm enrollment={data} onClose={onClose} onSaved={onSaved} />
          ) : data.state === 'cancelled' ? (
            <p className="warning">
              <Icon name="warning" />
              Esta atuação foi cancelada; não há nada a responder.
            </p>
          ) : (
            <Withdraw enrollment={data} onClose={onClose} onSaved={onSaved} />
          )}
        </>
      )}
    </Dialog>
  );
}

function EventLine({ event }: { event: EventSummary }) {
  const time = timeLabel(event.time);
  return (
    <p className="answer-event">
      <strong>{event.name}</strong>
      <span>
        {dateLabel(event)}
        {time && ` · ${time}`}
      </span>
    </p>
  );
}

function AnswerForm({
  enrollment,
  onClose,
  onSaved,
}: {
  enrollment: EventEnrollment;
  onClose: () => void;
  onSaved: (e: EventEnrollment) => void;
}) {
  const [choice, setChoice] = useState<Choice>(() => choiceFrom(enrollment));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [offer, setOffer] = useState<EventEnrollment>();
  const ids = { instrument: useId(), notes: useId() };
  const options = enrollment.instruments;

  // Changing the answer starts a fresh note: a reason for missing it is not a note for going.
  const choose = (willAttend: boolean) => {
    setError(undefined);
    setChoice((c) =>
      willAttend === c.willAttend
        ? c
        : { ...c, willAttend, notes: willAttend === (enrollment.status === 'going') ? (enrollment.notes ?? '') : '' },
    );
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (choice.willAttend === null) return;
    setBusy(true);
    setError(undefined);
    const outcome = await eventsApi.saveEnrollment(enrollment.event.id, {
      willAttend: choice.willAttend,
      instrument: choice.willAttend && choice.instrument ? choice.instrument : null,
      notes: choice.notes.trim() || null,
    });
    setBusy(false);
    if (outcome.kind === 'ok') {
      onSaved(outcome.data);
      // A new "vou" to an upcoming event offers the calendar; a no, an unchanged yes or a past event closes.
      const saved = outcome.data;
      if (choice.willAttend && enrollment.status !== 'going' && saved.state === 'open' && !saved.event.past && !saved.event.cancelled) setOffer(saved);
      else onClose();
    } else if (outcome.kind === 'invalid') setError(Object.values(outcome.errors)[0]);
    else if (outcome.kind === 'closed') setError('Esta atuação já não aceita respostas.');
    else if (outcome.kind === 'signin') setError('A sessão terminou. Entra de novo para responder.');
    else setError('Não foi possível guardar. Tenta outra vez daqui a pouco.');
  };

  if (offer) return <CalendarOffer activity={eventActivity(offer.event)} headline="Inscrição guardada." onClose={onClose} />;

  return (
    <form className="answer" onSubmit={submit}>
      <fieldset className="choice">
        <legend className="sr-only">A tua resposta</legend>
        <div className="choice__options">
          <label className={choice.willAttend === true ? 'choice__option choice__option--yes is-on' : 'choice__option choice__option--yes'}>
            <input type="radio" name="willAttend" checked={choice.willAttend === true} onChange={() => choose(true)} />
            <Icon name="check" />
            Vou
          </label>
          <label className={choice.willAttend === false ? 'choice__option choice__option--no is-on' : 'choice__option choice__option--no'}>
            <input type="radio" name="willAttend" checked={choice.willAttend === false} onChange={() => choose(false)} />
            <Icon name="close" />
            Não vou
          </label>
        </div>
      </fieldset>

      {choice.willAttend === true && options.length > 0 && (
        <FormField id={ids.instrument} label="Instrumento">
          <span className="control control--select">
            <select id={ids.instrument} value={choice.instrument} onChange={(e) => setChoice((c) => ({ ...c, instrument: e.target.value }))}>
              <option value="">{enrollment.isLeitao ? 'Sem instrumento' : 'Não vou tocar'}</option>
              {options.map((i) => (
                <option key={i.value} value={i.value}>
                  {i.label}
                </option>
              ))}
            </select>
          </span>
        </FormField>
      )}

      {choice.willAttend !== null && (
        <details className="answer__more" open={Boolean(choice.notes)}>
          <summary>{choice.willAttend ? 'Acrescentar uma nota' : 'Dizer porquê'}</summary>
          <label className="sr-only" htmlFor={ids.notes}>
            {choice.willAttend ? 'Nota' : 'Motivo'}
          </label>
          <textarea
            id={ids.notes}
            rows={3}
            maxLength={NOTES_MAX}
            placeholder={choice.willAttend ? 'Chego mais tarde, levo boleia…' : 'Opcional'}
            value={choice.notes}
            onChange={(e) => setChoice((c) => ({ ...c, notes: e.target.value }))}
          />
        </details>
      )}

      <FieldError text={error} />

      <AnswerActions cancelLabel="Cancelar" onCancel={onClose} label="Guardar" busy={busy} disabled={choice.willAttend === null} />
    </form>
  );
}

function Withdraw({
  enrollment,
  onClose,
  onSaved,
}: {
  enrollment: EventEnrollment;
  onClose: () => void;
  onSaved: (e: EventEnrollment) => void;
}) {
  const [busy, setBusy] = useState(false);
  const [failed, setFailed] = useState(false);

  if (!enrollment.canRemove) {
    return <p>Esta atuação já passou e não aceita respostas.</p>;
  }

  const remove = async () => {
    setBusy(true);
    const outcome = await eventsApi.removeEnrollment(enrollment.event.id);
    setBusy(false);
    if (outcome.kind === 'ok') {
      onSaved(outcome.data);
      onClose();
    } else setFailed(true);
  };

  return (
    <div className="answer">
      <p>A tua inscrição diz que foste. Se afinal não foste, podes retirá-la.</p>
      {failed && <FieldError text="Não foi possível retirar. Tenta outra vez daqui a pouco." />}
      <AnswerActions cancelLabel="Manter" onCancel={onClose} label="Retirar inscrição" onConfirm={remove} busy={busy} danger />
    </div>
  );
}
