import { useEffect, useId, useState, type FormEvent } from 'react';
import { Loading } from './App';
import { portal } from './content';
import { dateLabel, eventsApi, timeLabel, type EventEnrollment as Enrollment } from './eventsApi';
import { Icon } from './icons';

const NOTES_MAX = 1000;

type Load = Enrollment | 'signin' | 'missing' | 'failed';
type Draft = { willAttend: boolean | null; play: boolean; instrument: string; notes: string };

function draftFrom(a: Enrollment): Draft {
  const going = a.status === 'going';
  return {
    willAttend: a.status ? going : null,
    play: going ? a.instrument !== null : a.instruments.length > 0 && !a.isLeitao,
    instrument: (going ? a.instrument : null) ?? a.defaultInstrument ?? '',
    notes: a.notes ?? '',
  };
}

/**
 * /events/{id}/enrollment - a member's own answer for one event, on its own page (never a modal).
 * Open events take "vou" / "não vou", an instrument and a note; past events only let someone who
 * went withdraw; cancelled events take nothing. The server enforces all of it.
 */
export default function EventEnrollmentPage({ eventId }: { eventId: number }) {
  const [data, setData] = useState<Load>();

  const load = () => {
    setData(undefined);
    eventsApi.getEnrollment(eventId).then((o) =>
      setData(o.kind === 'ok' ? o.data : o.kind === 'signin' ? 'signin' : o.kind === 'notfound' ? 'missing' : 'failed'),
    );
  };
  useEffect(load, [eventId]);

  useEffect(() => {
    document.title = typeof data === 'object' ? `A minha resposta · ${data.event.name} · RTUB` : 'A minha resposta · RTUB';
  }, [data]);

  const back = typeof data === 'object' ? portal.event(eventId) : portal.events;

  return (
    <section className="page wrap enrollment-page" aria-labelledby="enrollment-title">
      <a className="back-link" href={back}>
        <Icon name="arrow" />
        {typeof data === 'object' ? 'Voltar à atuação' : 'Agenda'}
      </a>
      {data === undefined ? (
        <Loading label="A carregar a tua resposta…" />
      ) : data === 'signin' ? (
        <div className="notice" role="status">
          <p id="enrollment-title">Responder a uma atuação é para membros da RTUB.</p>
          <a
            className="btn btn--primary btn--sm"
            href={`${portal.login}?returnUrl=${encodeURIComponent(portal.eventEnrollment(eventId))}`}
          >
            Entrar
          </a>
        </div>
      ) : data === 'missing' ? (
        <div className="notice" role="status">
          <p id="enrollment-title">Esta atuação não existe ou foi removida.</p>
          <a className="btn btn--ghost btn--sm" href={portal.events}>
            Ver a agenda
          </a>
        </div>
      ) : data === 'failed' ? (
        <div className="notice" role="status">
          <p id="enrollment-title">Não conseguimos abrir esta página agora.</p>
          <button type="button" className="btn btn--ghost btn--sm" onClick={load}>
            Tentar novamente
          </button>
        </div>
      ) : (
        <Answer enrollment={data} onSaved={setData} />
      )}
    </section>
  );
}

function Summary({ enrollment }: { enrollment: Enrollment }) {
  const e = enrollment.event;
  const time = timeLabel(e.time);
  return (
    <header className="enrollment-event">
      <p className="eyebrow">A minha resposta</p>
      <h1 id="enrollment-title" className="enrollment-event__name">
        {e.name}
      </h1>
      <p className="agenda-card__meta">
        <Icon name="calendar" />
        <span>
          {dateLabel(e)}
          {time && ` · ${time}`}
        </span>
      </p>
      <p className="agenda-card__meta">
        <Icon name="geo" />
        <span>{e.location}</span>
      </p>
    </header>
  );
}

function CurrentAnswer({ enrollment }: { enrollment: Enrollment }) {
  const label =
    enrollment.status === 'going'
      ? enrollment.state === 'past'
        ? 'Foste a esta atuação.'
        : 'Vais a esta atuação.'
      : enrollment.status === 'notGoing'
        ? enrollment.state === 'past'
          ? 'Não foste a esta atuação.'
          : 'Não vais a esta atuação.'
        : 'Ainda não respondeste.';
  const instrument = enrollment.instruments.find((i) => i.value === enrollment.instrument)?.label;
  return (
    <div className={`answer answer--${enrollment.status ?? 'none'}`} role="status">
      <Icon name={enrollment.status === 'going' ? 'check' : enrollment.status === 'notGoing' ? 'close' : 'clock'} />
      <div>
        <p className="answer__label">{label}</p>
        {instrument && <p className="answer__detail">A tocar: {instrument}</p>}
        {enrollment.notes && <p className="answer__detail">“{enrollment.notes}”</p>}
      </div>
    </div>
  );
}

function Answer({ enrollment, onSaved }: { enrollment: Enrollment; onSaved: (a: Enrollment) => void }) {
  return (
    <div className="enrollment">
      <Summary enrollment={enrollment} />
      <CurrentAnswer enrollment={enrollment} />
      {enrollment.state === 'open' ? (
        <AnswerForm enrollment={enrollment} onSaved={onSaved} />
      ) : enrollment.state === 'cancelled' ? (
        <p className="warning">
          <Icon name="warning" />
          Esta atuação foi cancelada; já não há nada a responder.
        </p>
      ) : (
        <PastAnswer enrollment={enrollment} onSaved={onSaved} />
      )}
    </div>
  );
}

function AnswerForm({ enrollment, onSaved }: { enrollment: Enrollment; onSaved: (a: Enrollment) => void }) {
  const [draft, setDraft] = useState<Draft>(() => draftFrom(enrollment));
  const [busy, setBusy] = useState(false);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [banner, setBanner] = useState<'saved' | 'closed' | 'failed' | 'signin'>();
  const ids = { play: useId(), instrument: useId(), notes: useId() };

  const set = (next: Partial<Draft>) => {
    setDraft((d) => ({ ...d, ...next }));
    setBanner(undefined);
  };

  // Changing the answer starts a fresh note: a reason for missing it is not a note for going.
  const choose = (willAttend: boolean) =>
    set(willAttend === draft.willAttend ? {} : { willAttend, notes: willAttend === (enrollment.status === 'going') ? enrollment.notes ?? '' : '' });

  const hasOptions = enrollment.instruments.length > 0;
  const plays = draft.willAttend === true && (enrollment.isLeitao ? draft.instrument !== '' : draft.play && hasOptions);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (draft.willAttend === null) {
      setErrors({ willAttend: 'Escolhe se vais ou não.' });
      return;
    }
    if (draft.willAttend && !enrollment.isLeitao && draft.play && hasOptions && !draft.instrument) {
      setErrors({ instrument: 'Escolhe o instrumento.' });
      return;
    }
    setErrors({});
    setBusy(true);
    const outcome = await eventsApi.saveEnrollment(enrollment.event.id, {
      willAttend: draft.willAttend,
      instrument: plays ? draft.instrument : null,
      notes: draft.notes.trim() || null,
    });
    setBusy(false);
    if (outcome.kind === 'ok') {
      onSaved(outcome.data);
      setBanner('saved');
    } else if (outcome.kind === 'invalid') setErrors(outcome.errors);
    else if (outcome.kind === 'closed' || outcome.kind === 'signin') setBanner(outcome.kind);
    else setBanner('failed');
  };

  return (
    <form className="form enrollment-form" onSubmit={submit} noValidate>
      <fieldset className="choice" aria-describedby={errors.willAttend ? 'enrollment-will-error' : undefined}>
        <legend className="choice__legend">Vais?</legend>
        <div className="choice__options">
          <label className={draft.willAttend === true ? 'choice__option choice__option--yes is-on' : 'choice__option choice__option--yes'}>
            <input type="radio" name="willAttend" checked={draft.willAttend === true} onChange={() => choose(true)} />
            <Icon name="check" />
            Vou
          </label>
          <label className={draft.willAttend === false ? 'choice__option choice__option--no is-on' : 'choice__option choice__option--no'}>
            <input type="radio" name="willAttend" checked={draft.willAttend === false} onChange={() => choose(false)} />
            <Icon name="close" />
            Não vou
          </label>
        </div>
        {errors.willAttend && (
          <p id="enrollment-will-error" className="form__error">
            {errors.willAttend}
          </p>
        )}
      </fieldset>

      {draft.willAttend === true &&
        (enrollment.isLeitao ? (
          hasOptions && (
            <div className="form__field">
              <label htmlFor={ids.instrument}>Instrumento (se quiseres)</label>
              <span className="control control--select">
                <select id={ids.instrument} value={draft.instrument} onChange={(e) => set({ instrument: e.target.value })}>
                  <option value="">Sem instrumento</option>
                  {enrollment.instruments.map((i) => (
                    <option key={i.value} value={i.value}>
                      {i.label}
                    </option>
                  ))}
                </select>
              </span>
            </div>
          )
        ) : (
          <>
            <label className="form__check" htmlFor={ids.play}>
              <input
                id={ids.play}
                type="checkbox"
                checked={draft.play && hasOptions}
                disabled={!hasOptions}
                onChange={(e) => set({ play: e.target.checked })}
              />
              Vou tocar
            </label>
            {!hasOptions && (
              <p className="form__hint">
                Não tens instrumentos no perfil, por isso a inscrição fica registada sem instrumento.
              </p>
            )}
            {draft.play && hasOptions && (
              <div className={errors.instrument ? 'form__field form__field--error' : 'form__field'}>
                <label htmlFor={ids.instrument}>Instrumento</label>
                <span className="control control--select">
                  <select id={ids.instrument} value={draft.instrument} onChange={(e) => set({ instrument: e.target.value })}>
                    <option value="">Escolher…</option>
                    {enrollment.instruments.map((i) => (
                      <option key={i.value} value={i.value}>
                        {i.label}
                      </option>
                    ))}
                  </select>
                </span>
                {errors.instrument && <p className="form__error">{errors.instrument}</p>}
              </div>
            )}
          </>
        ))}

      {draft.willAttend !== null && (
        <div className={errors.notes ? 'form__field form__field--error' : 'form__field'}>
          <label htmlFor={ids.notes}>{draft.willAttend ? 'Nota (opcional)' : 'Motivo (opcional)'}</label>
          <textarea
            id={ids.notes}
            rows={4}
            maxLength={NOTES_MAX}
            placeholder={draft.willAttend ? 'Chego mais tarde, levo boleia…' : 'Porque não podes ir'}
            value={draft.notes}
            onChange={(e) => set({ notes: e.target.value })}
          />
          {errors.notes && <p className="form__error">{errors.notes}</p>}
        </div>
      )}

      {banner === 'saved' && (
        <p className="form__banner form__banner--ok" role="status">
          Resposta guardada.{' '}
          <a href={portal.event(enrollment.event.id)}>Voltar à atuação</a>
        </p>
      )}
      {banner === 'closed' && (
        <p className="form__banner" role="alert">
          Esta atuação já não aceita respostas. Recarrega a página.
        </p>
      )}
      {banner === 'signin' && (
        <p className="form__banner" role="alert">
          A sessão terminou. Entra de novo para guardar.
        </p>
      )}
      {banner === 'failed' && (
        <p className="form__banner" role="alert">
          Não foi possível guardar. Tenta outra vez daqui a pouco.
        </p>
      )}

      <button type="submit" className="btn btn--primary form__submit" disabled={busy}>
        {busy && <span className="spinner spinner--small" aria-hidden="true" />}
        {enrollment.status ? 'Guardar alteração' : 'Guardar resposta'}
      </button>
    </form>
  );
}

function PastAnswer({ enrollment, onSaved }: { enrollment: Enrollment; onSaved: (a: Enrollment) => void }) {
  const [confirming, setConfirming] = useState(false);
  const [busy, setBusy] = useState(false);
  const [failed, setFailed] = useState(false);

  if (!enrollment.canRemove) {
    return <p className="note">Esta atuação já passou e já não aceita respostas.</p>;
  }

  const remove = async () => {
    setBusy(true);
    const outcome = await eventsApi.removeEnrollment(enrollment.event.id);
    setBusy(false);
    if (outcome.kind === 'ok') onSaved(outcome.data);
    else setFailed(true);
  };

  return (
    <div className="enrollment-remove">
      <p>Afinal não foste? Podes retirar a tua inscrição desta atuação.</p>
      {confirming ? (
        <div className="enrollment-remove__confirm">
          <button type="button" className="btn btn--danger btn--sm" onClick={remove} disabled={busy}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            Sim, retirar
          </button>
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => setConfirming(false)} disabled={busy}>
            Manter
          </button>
        </div>
      ) : (
        <button type="button" className="btn btn--ghost btn--sm" onClick={() => setConfirming(true)}>
          Retirar a minha inscrição
        </button>
      )}
      {failed && (
        <p className="form__banner" role="alert">
          Não foi possível retirar. Tenta outra vez daqui a pouco.
        </p>
      )}
    </div>
  );
}
