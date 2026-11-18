// Local-date and time labels for events (local text in, Portuguese text out), split out of eventsApi.ts.
import type { EventSummary } from './eventsTypes';

// ---------- dates (local text in, Portuguese text out) ----------

const parts = (date: string) => date.split('-').map(Number) as [number, number, number];
const local = (date: string) => {
  const [y, m, d] = parts(date);
  return new Date(y, m - 1, d);
};
const format = (date: string, options: Intl.DateTimeFormatOptions) =>
  new Intl.DateTimeFormat('pt-PT', options).format(local(date)).replace('.', '');

export const dayOf = (date: string) => parts(date)[2];
export const monthShort = (date: string) => format(date, { month: 'short' });
export const yearOf = (date: string) => parts(date)[0];
export const weekday = (date: string) => format(date, { weekday: 'long' });

/** "sábado, 16 de outubro de 2026" or "10 a 12 de outubro de 2026" for a multi-day event. */
export function dateLabel(event: Pick<EventSummary, 'date' | 'endDate'>) {
  if (!event.endDate) return format(event.date, { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' });
  const [y1, m1] = parts(event.date);
  const [y2, m2] = parts(event.endDate);
  const end = format(event.endDate, { day: 'numeric', month: 'long', year: 'numeric' });
  if (y1 === y2 && m1 === m2) return `${dayOf(event.date)} a ${end}`;
  if (y1 === y2) return `${format(event.date, { day: 'numeric', month: 'long' })} a ${end}`;
  return `${format(event.date, { day: 'numeric', month: 'long', year: 'numeric' })} a ${end}`;
}

/** "21h30" for a set time; nothing for a whole-day event. */
export const timeLabel = (time: string | null) => (time ? time.replace(':', 'h') : null);
