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

// ---------- discussion (members) and contacts (Mod and above), React track 013 ----------

export type EventAuthor = { name: string; fullName: string | null; avatarUrl: string; badge: string | null };

export type EventComment = {
  id: number;
  body: string;
  author: EventAuthor;
  createdAt: string;
  edited: boolean;
  canEdit: boolean;
  canDelete: boolean;
};

export type EventPassenger = { id: number; member: EventAuthor; mine: boolean; canRemove: boolean };

export type EventTransport = { vehicle: string; totalSeats: number; notes: string | null; canManage: boolean; passengers: EventPassenger[] };

export type EventPost = {
  id: number;
  title: string;
  body: string;
  author: EventAuthor;
  createdAt: string;
  lastActivityAt: string;
  edited: boolean;
  pinned: boolean;
  locked: boolean;
  mine: boolean;
  canEdit: boolean;
  canDelete: boolean;
  canComment: boolean;
  comments: EventComment[];
  transport: EventTransport | null;
};

/** GET /api/events/{id}/discussion: pinned first, then latest activity; every write answers it again. */
export type EventDiscussion = { canModerate: boolean; posts: EventPost[] };

export type EventContactRow = {
  userId: string;
  name: string;
  fullName: string | null;
  avatarUrl: string;
  phone: string | null;
  willAttend: boolean | null;
  notes: string | null;
  contactedAt: string | null;
};

export type EventContacts = { contacted: EventContactRow[]; notContacted: EventContactRow[] };

/** GET /api/events/stats (members, 012F): "vou" answers per member for events dated from..to. */
export type EventMemberStats = {
  name: string;
  fullName: string | null;
  avatarUrl: string;
  categories: string[];
  groups: ('tuno' | 'caloiro' | 'leitao')[];
  went: number;
  going: number;
};

export type EventStats = { from: string; to: string; pastEvents: number; members: EventMemberStats[] };

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
  imageUrl: string | null;
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

/** GET /api/events/{id}/notices (Admin/Owner): who a notice would reach, as counts. */
export type NoticeAudience = {
  emailSubscribed: number;
  emailTotal: number;
  pushSubscribed: number;
  pushTotal: number;
  pushLeitoesCaloirosSubscribed: number;
  pushLeitoesCaloirosTotal: number;
};

export type NoticeInput =
  | { channel: 'email'; kind: 'new' | 'reminder'; message: null; onlyLeitoesAndCaloiros: false }
  | { channel: 'push'; kind: null; message: string; onlyLeitoesAndCaloiros: boolean };

/** What a notice (or the cancellation email) did; `warning` when part of it did not go out. */
export type NoticeResult = { sent: number; failed: number; warning: string | null };

/** One prize with its id, for the Admin/Owner management modal (012B). */
export type EventPrize = { id: number; name: string };

/** One video for the Admin/Owner management modal (012C): `title` null when none was given. */
export type ManagedVideo = { id: number; title: string | null };

/** The repertoire for the Admin/Owner modal (012D): every day it can hold songs, each in running order. */
export type RepertoireManage = { days: { date: string; items: { id: number; title: string }[] }[] };

/** A song Admin/Owner may add: only what Music shows them, not already in the event. */
export type RepertoireSong = { id: number; title: string; album: string | null };

/** Every answer for the Admin/Owner manager (012E), grouped as "Quem vai"; `id` is the answer's row. */
export type EnrollmentList = {
  canAdd: boolean;
  going: { id: number; participant: EventParticipant }[];
  leitoes: { id: number; participant: EventParticipant }[];
  notGoing: { id: number; participant: EventParticipant }[];
};

/** A member the manager may add (not answered yet, not expelled). */
export type MemberOption = { id: string; name: string; fullName: string | null; avatarUrl: string };

/** The old page's limit, also enforced by the server. */
export const MAX_EVENT_VIDEO_BYTES = 100 * 1024 * 1024;

export type EventVideo = { id: number; title: string; url: string; mimeType: string };

export type EventDetail = {
  isMember: boolean;
  canManage: boolean;
  /** Admin/Owner on a past festival: prizes can be added here (012B). */
  canManagePrizes: boolean;
  /** Mod and above: the link to the Blazor contacts page (012E). */
  canTrackContacts: boolean;
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

export async function call<T>(method: string, url: string, body?: unknown, retried = false): Promise<Outcome<T>> {
  try {
    const headers: Record<string, string> = { Accept: 'application/json' };
    if (method !== 'GET') headers['X-CSRF-TOKEN'] = await antiforgeryToken();
    if (body !== undefined && !(body instanceof FormData)) headers['Content-Type'] = 'application/json';

    const r = await fetch(url, {
      method,
      headers,
      body: body === undefined || body instanceof FormData ? body : JSON.stringify(body),
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
  // Discussion (members, 013).
  discussion: (id: number) => call<EventDiscussion>('GET', `/api/events/${id}/discussion`),
  addPost: (id: number, title: string, body: string) => call<EventDiscussion>('POST', `/api/events/${id}/discussion/posts`, { title, body }),
  editPost: (id: number, postId: number, title: string, body: string) =>
    call<EventDiscussion>('PUT', `/api/events/${id}/discussion/posts/${postId}`, { title, body }),
  deletePost: (id: number, postId: number) => call<EventDiscussion>('DELETE', `/api/events/${id}/discussion/posts/${postId}`),
  setPostFlags: (id: number, postId: number, pinned: boolean, locked: boolean) =>
    call<EventDiscussion>('PUT', `/api/events/${id}/discussion/posts/${postId}/flags`, { pinned, locked }),
  addComment: (id: number, postId: number, body: string) =>
    call<EventDiscussion>('POST', `/api/events/${id}/discussion/posts/${postId}/comments`, { body }),
  editComment: (id: number, commentId: number, body: string) =>
    call<EventDiscussion>('PUT', `/api/events/${id}/discussion/comments/${commentId}`, { body }),
  deleteComment: (id: number, commentId: number) => call<EventDiscussion>('DELETE', `/api/events/${id}/discussion/comments/${commentId}`),
  addTransport: (id: number, vehicle: string, seats: number, notes: string) =>
    call<EventDiscussion>('POST', `/api/events/${id}/discussion/transport`, { vehicle, seats, notes }),
  editTransport: (id: number, postId: number, vehicle: string, seats: number, notes: string) =>
    call<EventDiscussion>('PUT', `/api/events/${id}/discussion/posts/${postId}/transport`, { vehicle, seats, notes }),
  passengerMembers: (id: number, postId: number, q: string) =>
    call<MemberOption[]>('GET', `/api/events/${id}/discussion/posts/${postId}/passengers/members?q=${encodeURIComponent(q)}`),
  addPassenger: (id: number, postId: number, userId: string) =>
    call<EventDiscussion>('POST', `/api/events/${id}/discussion/posts/${postId}/passengers`, { userId }),
  removePassenger: (id: number, postId: number, passengerId: number) =>
    call<EventDiscussion>('DELETE', `/api/events/${id}/discussion/posts/${postId}/passengers/${passengerId}`),
  // Contact tracking (Mod and above, 013).
  contacts: (id: number) => call<EventContacts>('GET', `/api/events/${id}/contacts`),
  saveContact: (id: number, userId: string, willAttend: boolean | null, notes: string) =>
    call<EventContacts>('PUT', `/api/events/${id}/contacts/${encodeURIComponent(userId)}`, { willAttend, notes }),
  resetContact: (id: number, userId: string) => call<EventContacts>('DELETE', `/api/events/${id}/contacts/${encodeURIComponent(userId)}`),
  stats: (from?: string, to?: string) =>
    call<EventStats>('GET', `/api/events/stats${from && to ? `?from=${from}&to=${to}` : ''}`),
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
  // Admin/Owner, 012A. The image arrives already cropped (WebP, JPEG where WebP cannot be encoded).
  setImage: (id: number, image: Blob) => {
    const form = new FormData();
    form.set('image', image, image.type === 'image/jpeg' ? 'event-image.jpg' : 'event-image.webp');
    return call<void>('POST', `/api/events/${id}/image`, form);
  },
  removeImage: (id: number) => call<void>('DELETE', `/api/events/${id}/image`),
  cancelEvent: (id: number, reason: string, notifyByEmail: boolean) =>
    call<NoticeResult>('POST', `/api/events/${id}/cancel`, { reason, notifyByEmail }),
  reactivateEvent: (id: number) => call<void>('POST', `/api/events/${id}/reactivate`),
  noticeAudience: (id: number) => call<NoticeAudience>('GET', `/api/events/${id}/notices`),
  sendNotice: (id: number, input: NoticeInput) => call<NoticeResult>('POST', `/api/events/${id}/notices`, input),
  // Admin/Owner, 012B. Every write answers the event's prizes as they now are.
  prizes: (id: number) => call<EventPrize[]>('GET', `/api/events/${id}/prizes`),
  addPrize: (id: number, name: string) => call<EventPrize[]>('POST', `/api/events/${id}/prizes`, { name }),
  renamePrize: (id: number, prizeId: number, name: string) =>
    call<EventPrize[]>('PUT', `/api/events/${id}/prizes/${prizeId}`, { name }),
  deletePrize: (id: number, prizeId: number) => call<EventPrize[]>('DELETE', `/api/events/${id}/prizes/${prizeId}`),
  // Admin/Owner, 012C. Every write answers the event's videos in order.
  videos: (id: number) => call<ManagedVideo[]>('GET', `/api/events/${id}/videos`),
  uploadVideo: (id: number, file: File, title: string) => {
    const form = new FormData();
    form.set('file', file);
    form.set('title', title);
    return call<ManagedVideo[]>('POST', `/api/events/${id}/videos`, form);
  },
  renameVideo: (id: number, videoId: number, title: string) =>
    call<ManagedVideo[]>('PUT', `/api/events/${id}/videos/${videoId}`, { title }),
  reorderVideos: (id: number, videoIds: number[]) => call<ManagedVideo[]>('POST', `/api/events/${id}/videos/reorder`, { videoIds }),
  deleteVideo: (id: number, videoId: number) => call<ManagedVideo[]>('DELETE', `/api/events/${id}/videos/${videoId}`),
  // Admin/Owner, 012D. Every write answers the whole repertoire.
  repertoire: (id: number) => call<RepertoireManage>('GET', `/api/events/${id}/repertoire`),
  repertoireSongs: (id: number, q: string) =>
    call<RepertoireSong[]>('GET', `/api/events/${id}/repertoire/songs?q=${encodeURIComponent(q)}`),
  addToRepertoire: (id: number, songId: number, date: string) =>
    call<RepertoireManage>('POST', `/api/events/${id}/repertoire`, { songId, date }),
  removeFromRepertoire: (id: number, itemId: number) => call<RepertoireManage>('DELETE', `/api/events/${id}/repertoire/${itemId}`),
  clearRepertoireDay: (id: number, date: string) => call<RepertoireManage>('DELETE', `/api/events/${id}/repertoire/days/${date}`),
  reorderRepertoire: (id: number, date: string, itemIds: number[]) =>
    call<RepertoireManage>('POST', `/api/events/${id}/repertoire/reorder`, { date, itemIds }),
  // Admin/Owner, 012E: other members' answers. Every write answers the whole list.
  enrollments: (id: number) => call<EnrollmentList>('GET', `/api/events/${id}/enrollments`),
  enrollmentMembers: (id: number, q: string) =>
    call<MemberOption[]>('GET', `/api/events/${id}/enrollments/members?q=${encodeURIComponent(q)}`),
  addEnrollment: (id: number, userId: string) => call<EnrollmentList>('POST', `/api/events/${id}/enrollments`, { userId }),
  removeMemberEnrollment: (id: number, enrollmentId: number) =>
    call<EnrollmentList>('DELETE', `/api/events/${id}/enrollments/${enrollmentId}`),
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
