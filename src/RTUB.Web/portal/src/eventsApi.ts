// Typed client for /api/events (React track 011, Endpoints/EventEndpoints.cs). The server decides
// what the caller may see and do; `isMember` and `member` only spare people buttons that would be
// refused. Dates are local "yyyy-MM-dd" / "HH:mm" text, never parsed as UTC.

export type EventMemberSummary = {
  description: string;
  myStatus: 'going' | 'notGoing' | null;
  goingCount: number;
  repertoireCount: number;
  discussionCount: number;
};

export type EventSummary = {
  id: number;
  name: string;
  date: string;
  time: string | null;
  endDate: string | null;
  location: string;
  type: string;
  cancelled: boolean;
  past: boolean;
  season: string;
  imageUrl: string | null;
  videoCount: number;
  trophies: string[];
  member: EventMemberSummary | null;
};

export type EventTypeOption = { value: string; label: string };

/** `types` only for Admin/Owner (the create and edit form); null for everyone else. */
export type EventAgenda = {
  isMember: boolean;
  canManage: boolean;
  upcoming: EventSummary[];
  past: EventSummary[];
  types: EventTypeOption[] | null;
};

/** GET /api/events/{id}/edit (Admin/Owner). `time` is null for whole-day and multi-day events. */
export type EventEdit = {
  id: number;
  name: string;
  date: string;
  time: string | null;
  endDate: string | null;
  location: string;
  type: string;
  description: string;
  hasImage: boolean;
};

/** POST /api/events, PUT /api/events/{id}. A multi-day event (endDate) has no time. */
export type EventInput = {
  name: string;
  date: string;
  time: string | null;
  endDate: string | null;
  location: string;
  type: string;
  description: string;
};

export type EventVideo = { id: number; title: string; url: string; mimeType: string };

export type EventDetail = {
  isMember: boolean;
  canManage: boolean;
  event: EventSummary;
  videos: EventVideo[];
  member: {
    cancellationReason: string | null;
    notGoingCount: number;
    repertoire: { date: string; songs: string[] }[];
    participants: { going: EventParticipant[]; leitoes: EventParticipant[]; notGoing: EventParticipant[] };
  } | null;
};

/** One answer in "Quem vai" (members only). */
export type EventParticipant = {
  name: string;
  fullName: string | null;
  avatarUrl: string;
  badge: string | null;
  instrument: string | null;
  notes: string | null;
};

export type InstrumentOption = { value: string; label: string };

export type EventEnrollment = {
  event: EventSummary;
  state: 'open' | 'past' | 'cancelled';
  status: 'going' | 'notGoing' | null;
  instrument: string | null;
  notes: string | null;
  isLeitao: boolean;
  instruments: InstrumentOption[];
  defaultInstrument: string | null;
  canRemove: boolean;
};

export type EnrollmentInput = { willAttend: boolean; instrument: string | null; notes: string | null };

/** Every answer maps onto one of these; nothing throws. */
export type Outcome<T> =
  | { kind: 'ok'; data: T }
  | { kind: 'invalid'; errors: Record<string, string> }
  | { kind: 'signin' }
  | { kind: 'closed' }
  | { kind: 'notfound' }
  | { kind: 'forbidden' }
  | { kind: 'inuse' }
  | { kind: 'failed' };

let token: Promise<string> | null = null;

/** One antiforgery token per page load (it is bound to the session). */
function antiforgeryToken(refresh = false): Promise<string> {
  if (!token || refresh) {
    token = fetch('/api/public/antiforgery-token', { credentials: 'same-origin' })
      .then((r) => (r.ok ? r.json() : Promise.reject(new Error(`HTTP ${r.status}`))))
      .then((t: { token: string }) => t.token)
      .catch((e) => {
        token = null;
        throw e;
      });
  }
  return token;
}

async function call<T>(method: string, url: string, body?: unknown, retried = false): Promise<Outcome<T>> {
  try {
    const headers: Record<string, string> = { Accept: 'application/json' };
    if (method !== 'GET') headers['X-CSRF-TOKEN'] = await antiforgeryToken();
    if (body !== undefined) headers['Content-Type'] = 'application/json';

    const r = await fetch(url, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      credentials: 'same-origin',
    });
    if (r.status === 204) return { kind: 'ok', data: undefined as T };
    if (r.ok) return { kind: 'ok', data: (await r.json()) as T };

    const problem = (await r.json().catch(() => ({}))) as { errors?: Record<string, string[]>; type?: string };
    if (r.status === 400 && problem.errors) {
      const errors: Record<string, string> = {};
      for (const [k, v] of Object.entries(problem.errors)) errors[k.charAt(0).toLowerCase() + k.slice(1)] = v[0];
      return { kind: 'invalid', errors };
    }
    // A refused antiforgery token (expired page, new session): fetch a fresh one and try once more.
    if (r.status === 400 && method !== 'GET' && !retried) {
      await antiforgeryToken(true);
      return call<T>(method, url, body, true);
    }
    if (r.status === 401) return { kind: 'signin' };
    if (r.status === 404) return { kind: 'notfound' };
    if (r.status === 403) return { kind: 'forbidden' };
    if (r.status === 409) return { kind: problem.type === 'events:in-use' ? 'inuse' : 'closed' };
    return { kind: 'failed' };
  } catch {
    return { kind: 'failed' };
  }
}

export const eventsApi = {
  agenda: () => call<EventAgenda>('GET', '/api/events'),
  event: (id: number) => call<EventDetail>('GET', `/api/events/${id}`),
  getEnrollment: (id: number) => call<EventEnrollment>('GET', `/api/events/${id}/enrollment`),
  saveEnrollment: (id: number, input: EnrollmentInput) =>
    call<EventEnrollment>('PUT', `/api/events/${id}/enrollment`, input),
  removeEnrollment: (id: number) => call<EventEnrollment>('DELETE', `/api/events/${id}/enrollment`),
  videoPlayed: (videoId: number) => call<void>('POST', `/api/events/videos/${videoId}/plays`),
  // Admin/Owner; the server refuses everyone else (401/403).
  eventForEdit: (id: number) => call<EventEdit>('GET', `/api/events/${id}/edit`),
  createEvent: (input: EventInput) => call<EventSummary>('POST', '/api/events', input),
  updateEvent: (id: number, input: EventInput) => call<EventSummary>('PUT', `/api/events/${id}`, input),
  deleteEvent: (id: number) => call<void>('DELETE', `/api/events/${id}`),
};

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
