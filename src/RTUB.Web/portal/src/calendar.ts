// "Adicionar ao Google Calendar" (task 079, issue #260; docs/react-events.md → Calendário). One deterministic link
// builder for events and rehearsals: Google's documented prefilled template
// (https://calendar.google.com/calendar/render?action=TEMPLATE), with RTUB's own wall-clock times and ctz=Europe/Lisbon,
// so Google applies Portugal's offset (summer and winter) whatever the viewer's device zone is. Nothing here reads the
// browser's zone. The member reviews and saves in Google; RTUB never writes a calendar and never learns the result.

export const GOOGLE_TEMPLATE = 'https://calendar.google.com/calendar/render';
const ZONE = 'Europe/Lisbon';

/** What may leave RTUB: title, place, times and the item's own RTUB page. No notes, participants or descriptions. */
export type CalendarActivity = {
  title: string;
  location: string | null;
  /** yyyy-MM-dd, Portugal's calendar day. */
  date: string;
  /** HH:mm Portugal time, or null for a day-long activity. */
  start: string | null;
  /** HH:mm the source really holds (rehearsals), or null when it has no end time. */
  end: string | null;
  /** yyyy-MM-dd, the last day of a multi-day activity, or null. */
  endDate: string | null;
  /** The item's RTUB path, e.g. /events/12 (made absolute with the app's own origin). */
  path: string;
};

const pad = (n: number) => String(n).padStart(2, '0');

/** yyyy-MM-dd + days, as a calendar date (UTC arithmetic on the date only: no zone involved). */
export function addDays(date: string, days: number): string {
  const [y, m, d] = date.split('-').map(Number);
  const t = new Date(Date.UTC(y, m - 1, d + days));
  return `${t.getUTCFullYear()}-${pad(t.getUTCMonth() + 1)}-${pad(t.getUTCDate())}`;
}

const minutes = (hhmm: string) => {
  const [h, m] = hhmm.split(':').map(Number);
  return h * 60 + m;
};

const compactDate = (date: string) => date.replaceAll('-', '');
const stamp = (date: string, total: number) =>
  `${compactDate(addDays(date, Math.floor(total / 1440)))}T${pad(Math.floor((total % 1440) / 60))}${pad(total % 60)}00`;

/**
 * The window as Google wants it. Timed: local start/end (end on the next day when it is not after the start, e.g. a
 * rehearsal ending at 00:00). No real end: `suggestedMinutes` after the start (shown to the member as a suggestion).
 * Day-long or multi-day without a time: all-day dates, end exclusive.
 */
export function calendarDates(a: CalendarActivity, suggestedMinutes: number): { dates: string; allDay: boolean; suggested: boolean } {
  if (!a.start || (a.endDate && a.endDate !== a.date)) {
    const last = a.endDate && a.endDate > a.date ? a.endDate : a.date;
    return { dates: `${compactDate(a.date)}/${compactDate(addDays(last, 1))}`, allDay: true, suggested: false };
  }
  const from = minutes(a.start);
  let to = a.end ? minutes(a.end) : from + suggestedMinutes;
  if (to <= from) to += 1440;
  return { dates: `${stamp(a.date, from)}/${stamp(a.date, to)}`, allDay: false, suggested: !a.end };
}

/** The Google link. URLSearchParams encodes Portuguese characters, &, # and newlines; the destination is fixed. */
export function googleCalendarUrl(a: CalendarActivity, suggestedMinutes: number, origin: string): string {
  const { dates } = calendarDates(a, suggestedMinutes);
  const params = new URLSearchParams({ action: 'TEMPLATE', text: a.title, dates, ctz: ZONE });
  if (a.location?.trim()) params.set('location', a.location.trim());
  params.set('details', `RTUB: ${origin}${a.path}\nCópia independente: alterações na RTUB não a atualizam.`);
  return `${GOOGLE_TEMPLATE}?${params.toString()}`;
}
