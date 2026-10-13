// Typed client for /api/rehearsals (React track 014, Endpoints/RehearsalEndpoints.cs). Rehearsals speak of
// presença (attendance); events keep their own answer word. The server decides what the caller may see and do.
// Dates are local "yyyy-MM-dd" / "HH:mm" text, never parsed as UTC.
import { call, type EventAuthor, type MemberOption } from './eventsApi';

/** The caller's own presença: "pending" (vou, not approved yet), "approved" or "notGoing". */
export type PresenceStatus = 'pending' | 'approved' | 'notGoing';

export type RehearsalSummary = {
  id: number;
  date: string;
  start: string;
  end: string;
  location: string;
  theme: string | null;
  description: string | null;
  cancelled: boolean;
  cancellationReason: string | null;
  past: boolean;
  /** Presenças can be approved: past, or today when it starts at 21:00 or later. */
  approvable: boolean;
  season: string;
  goingCount: number;
  /** Admin/Owner only (0 for everyone else). */
  pendingCount: number;
  mine: { status: PresenceStatus; instrument: string | null; notes: string | null } | null;
};

export type RehearsalAgenda = { canManage: boolean; upcoming: RehearsalSummary[]; past: RehearsalSummary[] };

export type Attendee = {
  id: number;
  member: EventAuthor;
  status: PresenceStatus;
  instrument: string | null;
  otherInstruments: string | null;
  notes: string | null;
  mine: boolean;
  canApprove: boolean;
  canRemove: boolean;
};

export type InstrumentCount = { instrument: string; count: number };

export type RehearsalDetail = {
  rehearsal: RehearsalSummary;
  notes: string | null;
  canManage: boolean;
  attendance: { going: Attendee[]; leitoes: Attendee[]; notGoing: Attendee[] };
  instruments: InstrumentCount[];
  otherInstruments: InstrumentCount[];
};

/** GET/PUT /api/rehearsals/{id}/attendance: the caller's presença form ("open", "past" or "cancelled"). */
export type MyAttendance = {
  rehearsal: RehearsalSummary;
  state: 'open' | 'past' | 'cancelled';
  status: PresenceStatus | null;
  instrument: string | null;
  notes: string | null;
  isLeitao: boolean;
  instruments: { value: string; label: string }[];
  defaultInstrument: string | null;
};

export type RehearsalInput = { date: string; location: string; theme: string; description: string; notes: string };

export type RehearsalStats = {
  from: string;
  to: string;
  pastRehearsals: number;
  members: {
    name: string;
    fullName: string | null;
    avatarUrl: string;
    categories: string[];
    groups: ('tuno' | 'caloiro' | 'leitao')[];
    approved: number;
    pending: number;
  }[];
};

export type NoticeAudience = { subscribed: number; total: number; leitoesCaloirosSubscribed: number; leitoesCaloirosTotal: number };

const base = '/api/rehearsals';

export const rehearsalsApi = {
  agenda: () => call<RehearsalAgenda>('GET', base),
  rehearsal: (id: number) => call<RehearsalDetail>('GET', `${base}/${id}`),
  stats: (from?: string, to?: string) => call<RehearsalStats>('GET', `${base}/stats${from && to ? `?from=${from}&to=${to}` : ''}`),
  myAttendance: (id: number) => call<MyAttendance>('GET', `${base}/${id}/attendance`),
  saveAttendance: (id: number, willAttend: boolean, instrument: string | null, notes: string | null) =>
    call<MyAttendance>('PUT', `${base}/${id}/attendance`, { willAttend, instrument, notes }),
  removeAttendance: (id: number) => call<MyAttendance>('DELETE', `${base}/${id}/attendance`),
  // Admin/Owner; the server refuses everyone else (401/403).
  create: (input: RehearsalInput) => call<RehearsalSummary>('POST', base, input),
  createRange: (input: { from: string; to: string; location: string; theme: string; description: string }) =>
    call<{ created: number; skipped: number }>('POST', `${base}/range`, input),
  update: (id: number, input: RehearsalInput) => call<RehearsalSummary>('PUT', `${base}/${id}`, input),
  remove: (id: number) => call<void>('DELETE', `${base}/${id}`),
  cancel: (id: number, reason: string) => call<void>('POST', `${base}/${id}/cancel`, { reason }),
  reactivate: (id: number) => call<void>('POST', `${base}/${id}/reactivate`),
  noticeAudience: (id: number) => call<NoticeAudience>('GET', `${base}/${id}/notice`),
  sendNotice: (id: number, message: string, onlyLeitoesAndCaloiros: boolean) =>
    call<{ sent: number; failed: number }>('POST', `${base}/${id}/notice`, { message, onlyLeitoesAndCaloiros }),
  approve: (id: number, attendanceId: number) => call<void>('POST', `${base}/${id}/attendances/${attendanceId}/approve`),
  removeAttendee: (id: number, attendanceId: number) => call<void>('DELETE', `${base}/${id}/attendances/${attendanceId}`),
  members: (id: number, q: string) => call<MemberOption[]>('GET', `${base}/${id}/attendances/members?q=${encodeURIComponent(q)}`),
  addAttendee: (id: number, userId: string) => call<void>('POST', `${base}/${id}/attendances`, { userId }),
};

/** "Vou" / "Fui" wording follows the rehearsal: before or after its day. */
export function presenceLabel(status: PresenceStatus, past: boolean) {
  if (status === 'notGoing') return past ? 'Não fui' : 'Não vou';
  if (status === 'approved') return past ? 'Fui' : 'Vou · confirmada';
  return past ? 'Pendente' : 'Vou';
}
