import { call } from './eventsApi';

// /api/meetings (Endpoints/MeetingEndpoints.cs, task 034). Signed-in members only; a Leitão is refused everything
// (403) and a meeting the member does not see is a 404. The server decides every rule (MeetingBoardService): the
// `access`, `can` and `canX` flags only spare people buttons that would be refused. Dates travel as local
// "yyyy-MM-ddTHH:mm" text and are never handed to a Date parser, so no time zone can move them.

export type MeetingOption = { value: string; label: string };
/** A person to pick: id + what the old select showed. */
export type MeetingPersonOption = { id: string; label: string };

export type MeetingBoardAccess = {
  canCreate: boolean;
  meetingTypes: MeetingOption[];
  canProposeCv: boolean;
  canProposeDirecao: boolean;
  canProposeAg: boolean;
  canSeeRequests: boolean;
  canAddParticipants: boolean;
};

/** The card's buttons, decided by the server. */
export type MeetingCan = {
  manage: boolean;
  notify: boolean;
  uncancel: boolean;
  respond: boolean;
  removeOwn: boolean;
  participants: boolean;
  writeAta: boolean;
  viewAta: boolean;
};

export type MeetingMyParticipation = { id: number; willAttend: boolean; notes: string | null };

export type MeetingCard = {
  id: number;
  type: string;
  typeLabel: string;
  title: string;
  date: string;
  location: string | null;
  statement: string;
  organizerName: string | null;
  organizerPosition: string | null;
  tunoRepresentative: string | null;
  tunoRepresentativeId: string | null;
  cancelled: boolean;
  completed: boolean;
  past: boolean;
  goingCount: number;
  myParticipation: MeetingMyParticipation | null;
  ataStatus: 'draft' | 'published' | null;
  can: MeetingCan;
};

/** GET /api/meetings. `fiscalYear` null = every year. */
export type MeetingBoard = {
  upcoming: MeetingCard[];
  past: MeetingCard[];
  fiscalYears: string[];
  currentFiscalYear: string;
  fiscalYear: string | null;
  access: MeetingBoardAccess;
};

export type MeetingForm = { types: MeetingOption[]; tunoRepresentatives: MeetingPersonOption[] };

export type MeetingInput = {
  type: string;
  title: string;
  date: string;
  location: string;
  statement: string;
  tunoRepresentativeId: string | null;
};

/** `card` is null when the member who saved it cannot see it. */
export type MeetingSaved = { id: number; card: MeetingCard | null };

/** Someone in a "who receives" list: name, picture and, for a CV, years as Tuno. Never an email address. */
export type MeetingPerson = { name: string; avatarUrl: string; detail: string | null };

export type MeetingEmailDraft = {
  subject: string;
  body: string;
  recipients: MeetingPerson[];
  notReceiving: MeetingPerson[];
  tunoOptions: MeetingPersonOption[];
};

export type MeetingNoticeResult = { sent: number; warning: string | null };
export type MeetingCancelDraft = { recipients: MeetingPerson[]; notReceiving: MeetingPerson[]; total: number };
export type MeetingPushDraft = { recipients: MeetingPerson[]; notSubscribed: MeetingPerson[]; tunoOptions: MeetingPersonOption[] };

export type MeetingParticipant = {
  id: number;
  nickname: string;
  fullName: string;
  avatarUrl: string;
  notes: string | null;
  badge: string | null;
  canRemove: boolean;
};

export type MeetingParticipants = {
  meetingId: number;
  title: string;
  going: MeetingParticipant[];
  notGoing: MeetingParticipant[];
  canAdd: boolean;
};

export type MeetingCandidate = { id: string; name: string; avatarUrl: string };

export type MeetingAgendaPoint = {
  number: number;
  title: string;
  discussion: string | null;
  decision: string | null;
  votesFor: number | null;
  votesAgainst: number | null;
  votesAbstain: number | null;
  result: string | null;
};

export type MeetingAtaKind = 'cv' | 'ag' | 'direcao';

/** The ata editor. `id` null = not saved yet. */
export type MeetingAtaEditor = {
  id: number | null;
  status: 'draft' | 'published';
  kind: MeetingAtaKind;
  meetingTypeLabel: string;
  meetingTitle: string;
  meetingDate: string;
  ataNumber: string | null;
  actualStartTime: string;
  actualEndTime: string | null;
  location: string;
  quorumBasis: string;
  presidentName: string | null;
  firstSecretaryId: string | null;
  secondSecretaryId: string | null;
  firstSecretaryOptions: MeetingPersonOption[];
  secondSecretaryOptions: MeetingPersonOption[];
  present: string[];
  agendaPoints: MeetingAgendaPoint[];
  closingText: string | null;
  generatedAt: string | null;
};

export type MeetingAgendaPointInput = Omit<MeetingAgendaPoint, 'number'>;

export type MeetingAtaInput = {
  ataNumber: string | null;
  actualStartTime: string;
  actualEndTime: string | null;
  location: string;
  quorumBasis: string | null;
  firstSecretaryId: string | null;
  secondSecretaryId: string | null;
  agendaPoints: MeetingAgendaPointInput[];
  closingText: string | null;
};

/** "Ver Ata". `mine` only for someone who attended a published ata; `mine.confirmed` null = not answered yet. */
export type MeetingAtaView = {
  meetingId: number;
  status: 'draft' | 'published';
  kind: MeetingAtaKind;
  meetingTypeLabel: string;
  ataNumber: string | null;
  meetingDate: string;
  location: string;
  presidentName: string | null;
  firstSecretaryName: string | null;
  secondSecretaryName: string | null;
  present: string[];
  confirmed: string[];
  mine: { confirmed: boolean | null; notes: string | null } | null;
  agendaPoints: MeetingAgendaPoint[];
  closingText: string | null;
};

export type MeetingRequestStatus = 'Pending' | 'Confirmed' | 'Rejected' | 'Analysing';

export type MeetingRequest = {
  id: number;
  type: string;
  typeLabel: string;
  title: string;
  authorName: string;
  proposedDate: string;
  location: string | null;
  description: string;
  status: MeetingRequestStatus;
  canDelete: boolean;
  canRemind: boolean;
  canDecide: boolean;
};

export type MeetingRequestPage = { items: MeetingRequest[]; total: number; page: number; pageSize: number };

/** A proposal. `type`: ConselhoVeteranos, ReuniaoDirecao or AssembleiaGeralExtraordinaria. */
export type MeetingRequestInput = { type: string; title: string; proposedDate: string; location: string; description: string };

/** An accepted request, as the create form opens prefilled with it. */
export type MeetingDraft = { type: string; title: string; date: string; location: string | null; statement: string };

const query = (params: Record<string, string | number | null | undefined>) => {
  const p = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) if (v !== null && v !== undefined && v !== '') p.set(k, String(v));
  const s = p.toString();
  return s ? `?${s}` : '';
};

const base = '/api/meetings';

export const meetingsApi = {
  // fy: null = the server's default (the current fiscal year), "all" = every year, else "YYYY-YYYY".
  board: (fy: string | null, q: string) => call<MeetingBoard>('GET', `${base}${query({ fy, q })}`),
  meeting: (id: number) => call<MeetingCard>('GET', `${base}/${id}`),
  form: () => call<MeetingForm>('GET', `${base}/form`),
  create: (input: MeetingInput) => call<MeetingSaved>('POST', base, input),
  update: (id: number, input: MeetingInput) => call<MeetingSaved>('PUT', `${base}/${id}`, input),
  remove: (id: number) => call<void>('DELETE', `${base}/${id}`),
  cancelDraft: (id: number) => call<MeetingCancelDraft>('GET', `${base}/${id}/cancel`),
  cancel: (id: number, reason: string, notifyByEmail: boolean) =>
    call<MeetingNoticeResult>('POST', `${base}/${id}/cancel`, { reason, notifyByEmail }),
  uncancel: (id: number) => call<MeetingCard>('POST', `${base}/${id}/uncancel`),
  emailDraft: (id: number) => call<MeetingEmailDraft>('GET', `${base}/${id}/email`),
  emailPreview: (id: number, body: string) => call<{ html: string }>('POST', `${base}/${id}/email/preview`, { body }),
  sendEmail: (id: number, subject: string, body: string, tunoId: string | null) =>
    call<MeetingNoticeResult>('POST', `${base}/${id}/email`, { subject, body, tunoId }),
  pushDraft: (id: number) => call<MeetingPushDraft>('GET', `${base}/${id}/push`),
  sendPush: (id: number, message: string, tunoId: string | null) =>
    call<MeetingNoticeResult>('POST', `${base}/${id}/push`, { message, tunoId }),
  // Participations.
  respond: (id: number, willAttend: boolean, notes: string | null) =>
    call<MeetingCard>('PUT', `${base}/${id}/participation`, { willAttend, notes }),
  participants: (id: number) => call<MeetingParticipants>('GET', `${base}/${id}/participants`),
  removeParticipation: (id: number, participationId: number) =>
    call<MeetingParticipants>('DELETE', `${base}/${id}/participants/${participationId}`),
  candidates: (id: number, q: string) => call<MeetingCandidate[]>('GET', `${base}/${id}/participants/candidates${query({ q })}`),
  addParticipant: (id: number, userId: string) => call<MeetingParticipants>('POST', `${base}/${id}/participants`, { userId }),
  // Atas.
  ata: (id: number) => call<MeetingAtaView>('GET', `${base}/${id}/ata`),
  ataEditor: (id: number) => call<MeetingAtaEditor>('GET', `${base}/${id}/ata/edit`),
  saveAta: (id: number, input: MeetingAtaInput) => call<MeetingAtaEditor>('PUT', `${base}/${id}/ata`, input),
  publishAta: (id: number) => call<MeetingAtaEditor>('POST', `${base}/${id}/ata/publish`),
  confirmAta: (id: number, confirm: boolean) => call<MeetingAtaView>('POST', `${base}/${id}/ata/confirmation`, { confirm }),
  ataPdfUrl: (id: number) => `${base}/${id}/ata/pdf`,
  // Meeting requests ("Pedidos de Reuniões").
  requests: (status: string, fy: string | null, page: number, pageSize: number) =>
    call<MeetingRequestPage>('GET', `${base}/requests${query({ status, fy, page, pageSize })}`),
  propose: (input: MeetingRequestInput) => call<{ id: number }>('POST', `${base}/requests`, input),
  accept: (requestId: number) => call<MeetingDraft>('POST', `${base}/requests/${requestId}/accept`),
  reject: (requestId: number) => call<void>('POST', `${base}/requests/${requestId}/reject`),
  removeRequest: (requestId: number) => call<void>('DELETE', `${base}/requests/${requestId}`),
  remind: (requestId: number) => call<void>('POST', `${base}/requests/${requestId}/reminder`),
};

// ---------- dates: "yyyy-MM-ddTHH:mm" local text in, Portuguese text out (no Date parsing, no time zone) ----------

const WEEKDAYS = ['Domingo', 'Segunda', 'Terça', 'Quarta', 'Quinta', 'Sexta', 'Sábado'];
const WEEKDAYS_LONG = ['domingo', 'segunda-feira', 'terça-feira', 'quarta-feira', 'quinta-feira', 'sexta-feira', 'sábado'];
const MONTHS = ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez'];
const MONTHS_LONG = ['janeiro', 'fevereiro', 'março', 'abril', 'maio', 'junho', 'julho', 'agosto', 'setembro', 'outubro', 'novembro', 'dezembro'];

type Parts = { y: number; m: number; d: number; hh: string; mm: string };

function parts(value: string): Parts | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})(?:T(\d{2}):(\d{2}))?/.exec(value);
  if (!match) return null;
  return { y: Number(match[1]), m: Number(match[2]), d: Number(match[3]), hh: match[4] ?? '00', mm: match[5] ?? '00' };
}

const pad = (n: number) => String(n).padStart(2, '0');
// The calendar weekday of a date (UTC arithmetic on the date alone, so the reader's zone plays no part).
const weekdayIndex = (p: Parts) => new Date(Date.UTC(p.y, p.m - 1, p.d)).getUTCDay();

/** "06 out 2026" */
export const shortDate = (value: string) => {
  const p = parts(value);
  return p ? `${pad(p.d)} ${MONTHS[p.m - 1]} ${p.y}` : value;
};

/** "Segunda" */
export const weekdayName = (value: string) => {
  const p = parts(value);
  return p ? WEEKDAYS[weekdayIndex(p)] : '';
};

/** "20:00" */
export const timeOf = (value: string) => {
  const p = parts(value);
  return p ? `${p.hh}:${p.mm}` : '';
};

/** "06/10/2026" */
export const numericDate = (value: string) => {
  const p = parts(value);
  return p ? `${pad(p.d)}/${pad(p.m)}/${p.y}` : value;
};

/** "06/10/2026 20:00" */
export const numericDateTime = (value: string) => {
  const p = parts(value);
  return p ? `${pad(p.d)}/${pad(p.m)}/${p.y} ${p.hh}:${p.mm}` : value;
};

/** "segunda-feira, 06 de outubro de 2026 • 20:00", as the old email and notice dialogs showed. */
export const longDateTime = (value: string) => {
  const p = parts(value);
  return p ? `${WEEKDAYS_LONG[weekdayIndex(p)]}, ${pad(p.d)} de ${MONTHS_LONG[p.m - 1]} de ${p.y} • ${p.hh}:${p.mm}` : value;
};

/** The reader's local "now" as "yyyy-MM-ddTHH:mm", moved by whole days (for datetime-local defaults). */
export function localDateTime(addDays = 0, hour?: number): string {
  const now = new Date();
  const at = new Date(now.getFullYear(), now.getMonth(), now.getDate() + addDays, hour ?? now.getHours(), hour === undefined ? now.getMinutes() : 0);
  return `${at.getFullYear()}-${pad(at.getMonth() + 1)}-${pad(at.getDate())}T${pad(at.getHours())}:${pad(at.getMinutes())}`;
}

/** "HOJE" / "AMANHÃ" for a meeting today or tomorrow (the reader's calendar), else null. */
export function dayBadge(value: string): 'HOJE' | 'AMANHÃ' | null {
  const day = value.slice(0, 10);
  if (day === localDateTime(0).slice(0, 10)) return 'HOJE';
  if (day === localDateTime(1).slice(0, 10)) return 'AMANHÃ';
  return null;
}
