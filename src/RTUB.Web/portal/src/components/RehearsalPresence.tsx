// The member's own presença dialog (Vou / Não vou, instrument, note, removal).

import { rehearsalActivity, CalendarOffer } from './CalendarOffer';
import { useEffect, useId, useState, type FormEvent } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { rehearsalsApi, presenceLabel, type MyAttendance } from '../data/rehearsalsApi';
import { NOTES_MAX, problem, RehearsalLine } from './RehearsalShared';
import { Loading } from './ui/Loading';

// ---------- the member's presença ----------

/**
 * "Vais ao ensaio?" - a member's presença (Vou / Não vou, the instrument, a note) on an upcoming rehearsal.
 * A new "vou" waits for Admin/Owner to confirm it. Over or cancelled: only removing one's own presença.
 */
export function PresenceDialog({ rehearsalId, onClose, onSaved }: { rehearsalId: number; onClose: () => void; onSaved: () => void }) {
  const [data, setData] = useState<MyAttendance | null>();

  useEffect(() => {
    rehearsalsApi.myAttendance(rehearsalId).then((o) => setData(o.kind === 'ok' ? o.data : null));
  }, [rehearsalId]);

  return (
    <Dialog title={data && data.state !== 'open' ? 'A minha presença' : 'Vais ao ensaio?'} onClose={onClose}>
      {data === undefined ? (
        <Loading label="A carregar…" />
      ) : data === null ? (
        <p>Não foi possível abrir a tua presença agora. Tenta outra vez daqui a pouco.</p>
      ) : (
        <>
          <RehearsalLine rehearsal={data.rehearsal} />
          {data.state === 'open' ? (
            <PresenceForm data={data} onClose={onClose} onSaved={onSaved} />
          ) : data.state === 'cancelled' ? (
            <p className="warning">
              <Icon name="warning" />
              Este ensaio foi cancelado; não há presenças a marcar.
            </p>
          ) : (
            <RemovePresence data={data} onClose={onClose} onSaved={onSaved} />
          )}
        </>
      )}
    </Dialog>
  );
}

function PresenceForm({ data, onClose, onSaved }: { data: MyAttendance; onClose: () => void; onSaved: () => void }) {
  const going = data.status === 'pending' || data.status === 'approved';
  const [willAttend, setWillAttend] = useState<boolean | null>(data.status ? going : null);
  // Someone going plays their primary instrument unless they pick "não vou tocar"; a Leitão starts without one.
  const [instrument, setInstrument] = useState(going ? (data.instrument ?? '') : data.isLeitao ? '' : (data.defaultInstrument ?? ''));
  const [notes, setNotes] = useState(data.notes ?? '');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [offer, setOffer] = useState(false);
  const ids = { instrument: useId(), notes: useId() };

  // Changing the answer starts a fresh note: a reason for missing it is not a note for going.
  const choose = (value: boolean) => {
    if (value === willAttend) return;
    setWillAttend(value);
    setNotes(value === going && data.status ? (data.notes ?? '') : '');
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (willAttend === null) return;
    setBusy(true);
    setError(undefined);
    const o = await rehearsalsApi.saveAttendance(data.rehearsal.id, willAttend, willAttend && instrument ? instrument : null, notes.trim() || null);
    setBusy(false);
    if (o.kind !== 'ok') return setError(problem(o, 'guardar'));
    onSaved();
    // A new "vou" to an upcoming rehearsal offers the calendar (the presença stays pending until approved).
    if (willAttend && !going && data.state === 'open' && !data.rehearsal.past && !data.rehearsal.cancelled) setOffer(true);
    else onClose();
  };

  if (offer) {
    return <CalendarOffer activity={rehearsalActivity(data.rehearsal)} headline="Presença registada, pendente de aprovação." onClose={onClose} />;
  }

  return (
    <form className="answer" onSubmit={submit}>
      <fieldset className="choice">
        <legend className="sr-only">A tua presença</legend>
        <div className="choice__options">
          <label className={`choice__option choice__option--yes${willAttend === true ? ' is-on' : ''}`}>
            <input type="radio" name="willAttend" checked={willAttend === true} onChange={() => choose(true)} />
            <Icon name="check" />
            Vou
          </label>
          <label className={`choice__option choice__option--no${willAttend === false ? ' is-on' : ''}`}>
            <input type="radio" name="willAttend" checked={willAttend === false} onChange={() => choose(false)} />
            <Icon name="close" />
            Não vou
          </label>
        </div>
      </fieldset>

      {willAttend === true && data.instruments.length > 0 && (
        <div className="form__field">
          <label htmlFor={ids.instrument}>Instrumento</label>
          <span className="control control--select">
            <select id={ids.instrument} value={instrument} onChange={(e) => setInstrument(e.target.value)}>
              <option value="">{data.isLeitao ? 'Sem instrumento' : 'Não vou tocar'}</option>
              {data.instruments.map((i) => (
                <option key={i.value} value={i.value}>
                  {i.label}
                </option>
              ))}
            </select>
          </span>
        </div>
      )}

      {willAttend !== null && (
        <details className="answer__more" open={Boolean(notes)}>
          <summary>{willAttend ? 'Acrescentar uma nota' : 'Dizer porquê'}</summary>
          <label className="sr-only" htmlFor={ids.notes}>
            {willAttend ? 'Nota' : 'Motivo'}
          </label>
          <textarea
            id={ids.notes}
            rows={3}
            maxLength={NOTES_MAX}
            placeholder={willAttend ? 'Chego mais tarde…' : 'Opcional'}
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
          />
        </details>
      )}

      {willAttend === true && data.status !== 'approved' && <p className="note">A presença fica pendente até ser confirmada no fim do ensaio.</p>}
      {error && (
        <p className="form__error" role="alert">
          {error}
        </p>
      )}
      <div className="answer__actions">
        <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
          Cancelar
        </button>
        <button type="submit" className="btn btn--primary" disabled={busy || willAttend === null}>
          {busy && <span className="spinner spinner--small" aria-hidden="true" />}
          Guardar
        </button>
      </div>
    </form>
  );
}

function RemovePresence({ data, onClose, onSaved }: { data: MyAttendance; onClose: () => void; onSaved: () => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();

  if (!data.status) return <p>Este ensaio já passou e não tens presença registada.</p>;

  const remove = async () => {
    setBusy(true);
    const o = await rehearsalsApi.removeAttendance(data.rehearsal.id);
    setBusy(false);
    if (o.kind !== 'ok') return setError(problem(o, 'remover'));
    onSaved();
    onClose();
  };

  return (
    <div className="answer">
      <p>
        A tua presença: <strong>{presenceLabel(data.status, true)}</strong>. O ensaio já passou; se a presença está errada, podes
        removê-la.
      </p>
      {error && <p className="form__error">{error}</p>}
      <div className="answer__actions">
        <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
          Manter
        </button>
        <button type="button" className="btn btn--danger" onClick={remove} disabled={busy}>
          Remover a minha presença
        </button>
      </div>
    </div>
  );
}
