// Types of the /api/events client, split out of eventsApi.ts.
// what the caller may see and do; `isMember` and `member` only spare people buttons that would be
// refused. Dates are local "yyyy-MM-dd" / "HH:mm" text, never parsed as UTC.

type EventMemberSummary = {
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
  /** "Só membros". Only ever true for a member: the server never sends such an event to a visitor. */
  membersOnly: boolean;
};

export type EventTypeOption = { value: string; label: string };

/** `types` only for Mod and above (the create and edit form); null for everyone else. `canEdit`: create / edit. */
export type EventAgenda = {
  isMember: boolean;
  canManage: boolean;
  canEdit: boolean;
  upcoming: EventSummary[];
  past: EventSummary[];
  types: EventTypeOption[] | null;
};

// ---------- discussion (members) and contacts (Mod and above) ----------

export type EventAuthor = { name: string; fullName: string | null; avatarUrl: string; badge: string | null };

type EventComment = {
  id: number;
  body: string;
  author: EventAuthor;
  createdAt: string;
  edited: boolean;
  canEdit: boolean;
  canDelete: boolean;
};

type EventPassenger = { id: number; member: EventAuthor; mine: boolean; canRemove: boolean };

type EventTransport = { vehicle: string; totalSeats: number; notes: string | null; canManage: boolean; passengers: EventPassenger[] };

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

/** GET /api/events/stats (members): "vou" answers per member for events dated from..to. */
type EventMemberStats = {
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
  membersOnly: boolean;
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
  /** true = members only, false = public. The server keeps the current value on an update that omits it; the form always sends it. */
  membersOnly: boolean;
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

/** One prize with its id, for the Admin/Owner management modal. */
export type EventPrize = { id: number; name: string };

/** One video for the Admin/Owner management modal: `title` null when none was given. */
export type ManagedVideo = { id: number; title: string | null };

/** The repertoire for the Admin/Owner modal: every day it can hold songs, each in running order. */
export type RepertoireManage = { days: { date: string; items: { id: number; title: string }[] }[] };

/** A song Admin/Owner may add: only what Music shows them, not already in the event. */
export type RepertoireSong = { id: number; title: string; album: string | null };

/** Every answer for the Admin/Owner manager, grouped as "Quem vai"; `id` is the answer's row. */
export type EnrollmentList = {
  canAdd: boolean;
  going: { id: number; participant: EventParticipant }[];
  leitoes: { id: number; participant: EventParticipant }[];
  notGoing: { id: number; participant: EventParticipant }[];
};

/** A member the manager may add (not answered yet, not expelled). */
export type MemberOption = { id: string; name: string; fullName: string | null; avatarUrl: string };

/** The limit, also enforced by the server. */
export const MAX_EVENT_VIDEO_BYTES = 100 * 1024 * 1024;

export type EventVideo = { id: number; title: string; url: string; mimeType: string };

export type EventDetail = {
  isMember: boolean;
  canManage: boolean;
  /** Admin/Owner on a past festival: prizes can be added here. */
  canManagePrizes: boolean;
  /** Mod and above: can open the contact tracking page. */
  canTrackContacts: boolean;
  /** Admin / Owner, or the current Ensaiador / Magister for an upcoming, not cancelled event. */
  canManageRepertoire: boolean;
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
