import { useEffect, useId, useState, type FormEvent } from 'react';
import { Loading } from './App';
import { Dialog } from './Dialog';
import { PrizeManager } from './EventManage';
import { portal } from './content';
import { dateLabel, eventsApi, timeLabel, yearOf, type EventEnrollment, type EventSummary } from './eventsApi';
import { Icon } from './icons';

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
      onClose();
    } else if (outcome.kind === 'invalid') setError(Object.values(outcome.errors)[0]);
    else if (outcome.kind === 'closed') setError('Esta atuação já não aceita respostas.');
    else if (outcome.kind === 'signin') setError('A sessão terminou. Entra de novo para responder.');
    else setError('Não foi possível guardar. Tenta outra vez daqui a pouco.');
  };

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
        <div className="form__field">
          <label htmlFor={ids.instrument}>Instrumento</label>
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
        </div>
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

      {error && (
        <p className="form__error" role="alert">
          {error}
        </p>
      )}

      <div className="answer__actions">
        <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
          Cancelar
        </button>
        <button type="submit" className="btn btn--primary" disabled={busy || choice.willAttend === null}>
          {busy && <span className="spinner spinner--small" aria-hidden="true" />}
          Guardar
        </button>
      </div>
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
      {failed && (
        <p className="form__error" role="alert">
          Não foi possível retirar. Tenta outra vez daqui a pouco.
        </p>
      )}
      <div className="answer__actions">
        <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
          Manter
        </button>
        <button type="button" className="btn btn--danger" onClick={remove} disabled={busy}>
          {busy && <span className="spinner spinner--small" aria-hidden="true" />}
          Retirar inscrição
        </button>
      </div>
    </div>
  );
}

// ---------- prizes ----------

/**
 * The RTUB's prize history, newest festival first (festivals without prizes left out). With
 * `current`, that event's prizes lead. The history comes from the agenda when not given.
 */
export function PrizesDialog({
  history,
  current,
  manage,
  onClose,
}: {
  history?: EventSummary[];
  current?: EventSummary;
  /** Admin/Owner on the event page (012B): the current event's prizes become editable. */
  manage?: { canAdd: boolean; onChanged: () => void };
  onClose: () => void;
}) {
  const [past, setPast] = useState<EventSummary[] | null | undefined>(history);

  useEffect(() => {
    if (history) return;
    eventsApi.agenda().then((o) => setPast(o.kind === 'ok' ? o.data.past : null));
  }, [history]);

  const won = past?.filter((e) => e.trophies.length > 0 && e.id !== current?.id) ?? [];
  const total = (past ?? []).reduce((n, e) => n + e.trophies.length, 0);

  return (
    <Dialog title="Prémios" onClose={onClose} size="lg">
      {current && manage && <PrizeManager eventId={current.id} canAdd={manage.canAdd} onChanged={manage.onChanged} />}
      {current && !manage && current.trophies.length > 0 && (
        <section className="prizes-current" aria-label="Prémios desta atuação">
          <p className="eyebrow">Nesta atuação</p>
          <ul className="trophies">
            {current.trophies.map((t, i) => (
              <li key={i}>
                <Icon name="trophy" />
                {t}
              </li>
            ))}
          </ul>
        </section>
      )}
      {past === undefined ? (
        <Loading label="A carregar o palmarés…" />
      ) : past === null ? (
        <p>Não foi possível carregar o palmarés agora.</p>
      ) : won.length === 0 ? (
        !current && <p>Ainda não há prémios registados.</p>
      ) : (
        <section aria-label="Palmarés">
          <p className="eyebrow prizes__summary">
            {current ? 'Palmarés' : `${total === 1 ? '1 prémio' : `${total} prémios`} em ${won.length === 1 ? '1 festival' : `${won.length} festivais`}`}
          </p>
          <ul className="prizes__list">
            {won.map((e) => (
              <li key={e.id} className="prizes__festival">
                <a href={portal.event(e.id)} className="prizes__event">
                  {e.name}
                  <span>{yearOf(e.date)}</span>
                </a>
                <ul className="prizes__items">
                  {e.trophies.map((t, i) => (
                    <li key={i}>{t}</li>
                  ))}
                </ul>
              </li>
            ))}
          </ul>
        </section>
      )}
    </Dialog>
  );
}
