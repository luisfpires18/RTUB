import { useId, useState } from 'react';
import { calendarDates, googleCalendarUrl, type CalendarActivity } from './calendar';
import { Dialog } from './Dialog';
import type { EventSummary } from './eventsApi';
import { Icon } from './icons';
import type { RehearsalSummary } from './rehearsalsApi';

// Task 079 (#260): the optional "Adicionar ao Google Calendar" offer after a saved "vou", and the same action later on an
// upcoming detail page. Opening Google happens only on the member's click (no popup after an async save). Nothing here
// changes the RTUB answer: closing, skipping or a failed open leaves it exactly as saved.

export const eventActivity = (e: EventSummary): CalendarActivity => ({
  title: `RTUB · ${e.name}`, location: e.location || null, date: e.date, start: e.time, end: null, endDate: e.endDate, path: `/events/${e.id}`,
});

export const rehearsalActivity = (r: RehearsalSummary): CalendarActivity => ({
  title: r.theme?.trim() ? `RTUB · Ensaio · ${r.theme.trim()}` : 'RTUB · Ensaio',
  location: r.location || null, date: r.date, start: r.start || null, end: r.end || null, endDate: null, path: `/rehearsals/${r.id}`,
});

const DURATIONS = [60, 90, 120, 180, 240];
const hours = (m: number) => (m % 60 === 0 ? `${m / 60} h` : `${Math.floor(m / 60)} h ${m % 60}`);
const day = (date: string) => new Date(`${date}T12:00:00Z`).toLocaleDateString('pt-PT', { weekday: 'long', day: 'numeric', month: 'long', timeZone: 'UTC' });

/**
 * The offer itself (title + body + buttons), placed inside an existing dialog after a save, or in its own dialog.
 * `headline`: what was saved ("Inscrição guardada", "Presença registada, pendente de aprovação"); null for the manual action.
 */
export function CalendarOffer({ activity, headline, onClose }: { activity: CalendarActivity; headline: string | null; onClose: () => void }) {
  const [duration, setDuration] = useState(120);
  const [copied, setCopied] = useState<'yes' | 'no'>();
  const id = useId();
  const { allDay, suggested } = calendarDates(activity, duration);
  const url = googleCalendarUrl(activity, duration, location.origin);
  const end = (() => {
    if (!activity.start) return null;
    const [h, m] = activity.start.split(':').map(Number);
    const t = (h * 60 + m + duration) % 1440;
    return `${String(Math.floor(t / 60)).padStart(2, '0')}:${String(t % 60).padStart(2, '0')}`;
  })();

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(url);
      setCopied('yes');
    } catch {
      setCopied('no');
    }
  };

  return (
    <div className="calendar-offer">
      {headline && (
        <p className="calendar-offer__saved" role="status">
          <Icon name="check" /> {headline}
        </p>
      )}
      <dl className="calendar-offer__what">
        <dt className="sr-only">Atividade</dt>
        <dd className="calendar-offer__title">{activity.title}</dd>
        <dt className="sr-only">Quando</dt>
        <dd>
          {day(activity.date)}
          {activity.endDate && activity.endDate !== activity.date && ` a ${day(activity.endDate)}`}
          {allDay ? ' · dia inteiro' : ` · ${activity.start}${activity.end ? `–${activity.end}` : ''}`}
        </dd>
        {activity.location && (
          <>
            <dt className="sr-only">Onde</dt>
            <dd>{activity.location}</dd>
          </>
        )}
      </dl>
      {suggested && (
        <div className="form__field">
          <label htmlFor={`${id}-duration`}>Duração sugerida (a RTUB não guarda a hora de fim)</label>
          <select id={`${id}-duration`} value={duration} onChange={(e) => setDuration(Number(e.target.value))}>
            {DURATIONS.map((d) => (
              <option key={d} value={d}>
                {hours(d)}
              </option>
            ))}
          </select>
          <p className="form__hint">Fim sugerido às {end}; podes mudá-lo no Google Calendar.</p>
        </div>
      )}
      <p className="form__hint">Confirma em Guardar no Google Calendar. Alterações ou cancelamentos na RTUB não atualizam esta cópia automaticamente.</p>
      <div className="calendar-offer__actions">
        <a className="btn btn--primary" href={url} target="_blank" rel="noopener noreferrer" aria-label="Adicionar ao Google Calendar (abre noutra janela)">
          <Icon name="calendar" />
          Adicionar ao Google Calendar
        </a>
        <button type="button" className="btn btn--ghost" onClick={onClose}>
          {headline ? 'Agora não' : 'Fechar'}
        </button>
      </div>
      <p className="calendar-offer__fallback">
        Não abriu?{' '}
        <button type="button" className="btn btn--ghost btn--sm" onClick={copy}>
          Copiar o link
        </button>
        {copied === 'yes' && <span role="status"> Link copiado.</span>}
        {copied === 'no' && (
          <span role="status">
            {' '}Não deu para copiar:{' '}
            <a href={url} target="_blank" rel="noopener noreferrer">
              abrir o link
            </a>
            .
          </span>
        )}
      </p>
    </div>
  );
}

/** The offer in its own dialog: /pending-actions after a "Vou", and the manual action on detail pages. */
export function CalendarOfferDialog({ activity, headline, onClose }: { activity: CalendarActivity; headline: string | null; onClose: () => void }) {
  return (
    <Dialog title={headline ? 'Resposta guardada' : 'Adicionar ao calendário'} onClose={onClose}>
      <CalendarOffer activity={activity} headline={headline} onClose={onClose} />
    </Dialog>
  );
}
