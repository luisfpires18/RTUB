// Pieces shared by the rehearsal dialogs: the notes limit, error text, the rehearsal line and the presença pill.

import { dateLabel, type Outcome } from '../data/eventsApi';
import { Icon } from './icons';
import { outcomeText } from '../data/api';
import { presenceLabel, type RehearsalSummary } from '../data/rehearsalsApi';

export const NOTES_MAX = 500;

export const problem = (o: Outcome<unknown>, what: string) =>
  outcomeText(o, what, { forbidden: 'Não tens permissão para isto.', notfound: 'Este ensaio já não existe.', closed: 'Este ensaio já não permite isto. Recarrega a página.' });

export function RehearsalLine({ rehearsal }: { rehearsal: RehearsalSummary }) {
  return (
    <p className="answer-event">
      <strong>{rehearsal.theme ?? 'Ensaio'}</strong>
      <span>
        {dateLabel({ date: rehearsal.date, endDate: null })} · {rehearsal.start}–{rehearsal.end} · {rehearsal.location}
      </span>
    </p>
  );
}

/** The caller's own presença as a pill; `fallback` shows "Não foste" on a past rehearsal with none. */
export function PresencePill({ rehearsal, fallback = false }: { rehearsal: RehearsalSummary; fallback?: boolean }) {
  const status = rehearsal.mine?.status ?? (fallback && rehearsal.past ? 'notGoing' : null);
  if (!status || rehearsal.cancelled) return null;
  const kind = status === 'notGoing' ? 'no' : status === 'approved' ? 'yes' : 'wait';
  return (
    <span className={`pill pill--${kind}`}>
      <Icon name={status === 'notGoing' ? 'close' : status === 'approved' ? 'check' : 'clock'} />
      {presenceLabel(status, rehearsal.past)}
    </span>
  );
}
