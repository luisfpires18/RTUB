import { call } from './eventsApi';

// /api/members (Endpoints/MemberEndpoints.cs, React tracks 017 and 018). Signed-in members only; the writes are
// Admin/Owner and the server decides every rule (MemberAdminService).

export type Badge = { label: string; kind: string };
export type MemberCard = {
  id: string;
  displayName: string;
  fullName: string | null;
  avatarUrl: string | null;
  badges: Badge[];
  position: string | null;
  noRoles: boolean;
  instrument: string | null;
  online: boolean;
  inactive: boolean;
  expelled: boolean;
};
export type Option = { value: string; label: string };
export type Directory = { members: MemberCard[]; leitoes: MemberCard[]; instruments: Option[]; canManage: boolean; canDeleteMembers: boolean };
export type DirectoryFilters = { q: string; category: string; subCategory: string; instrument: string; activeOnly: boolean };

export type TimelineItem = { label: string; years: string; kind: string; state: string; accent: string; notes: string | null };
export type Activity = { date: string; name: string; type: string; isRehearsal: boolean };
export type MemberState = {
  retired: boolean | null;
  progress: string | null;
  progressMonths: number | null;
  progressTotalMonths: number | null;
  encourage: boolean;
  lastRehearsal: string | null;
  lastEvent: string | null;
  activities: Activity[];
};
export type MemberDetail = {
  id: string;
  displayName: string;
  fullName: string | null;
  avatarUrl: string | null;
  badges: Badge[];
  positions: string[];
  email: string | null;
  phone: string | null;
  city: string | null;
  birthDate: string | null;
  age: number | null;
  degree: string | null;
  showInstruments: boolean;
  instruments: string[];
  showMentor: boolean;
  mentor: string | null;
  timeline: TimelineItem[];
  state: MemberState | null;
  leitao: boolean;
  expelled: boolean;
};
export type ActiveMember = {
  id: string;
  displayName: string;
  fullName: string | null;
  avatarUrl: string | null;
  instrument: string | null;
  retired: boolean;
  lastRehearsal: string | null;
  lastEvent: string | null;
  progress: string | null;
  encourage: boolean;
  canMakeActive: boolean;
};
export type Birthday = {
  id: string;
  displayName: string;
  fullName: string | null;
  avatarUrl: string | null;
  age: number;
  city: string | null;
  badges: Badge[];
  birthday: string;
  date: string;
};
export type TreeNode = { id: string; displayName: string; fullName: string | null; avatarUrl: string | null; children: TreeNode[] };

const query = (params: Record<string, string | boolean>) => {
  const p = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) if (v) p.set(k, String(v));
  const s = p.toString();
  return s ? `?${s}` : '';
};

// ---------- member admin (Admin/Owner, React track 018) ----------

export type MemberInstrument = { id: number; instrument: string; label: string; primary: boolean };
export type MemberEdit = {
  id: string;
  firstName: string | null;
  lastName: string | null;
  nickname: string | null;
  phoneNumber: string | null;
  email: string | null;
  city: string | null;
  degree: string | null;
  dateOfBirth: string | null;
  category: string;
  fundador: boolean;
  honorario: boolean;
  yearLeitao: number | null;
  monthLeitao: number | null;
  yearCaloiro: number | null;
  monthCaloiro: number | null;
  yearTuno: number | null;
  monthTuno: number | null;
  mentorId: string | null;
  mentorName: string | null;
  instruments: MemberInstrument[];
  lockedDates: { leitao: boolean; caloiro: boolean; tuno: boolean };
};
export type MemberInput = Omit<MemberEdit, 'id' | 'mentorName' | 'instruments' | 'lockedDates'> & {
  noNickname: boolean;
  instruments: { instrument: string; primary: boolean }[] | null;
};
export type Mentor = { id: string; displayName: string; fullName: string | null; avatarUrl: string | null };

const at = (id: string) => `/api/members/${encodeURIComponent(id)}`;

export const memberAdminApi = {
  edit: (id: string) => call<MemberEdit>('GET', `${at(id)}/edit`),
  mentors: (q: string, exclude?: string) => call<Mentor[]>('GET', `/api/members/mentors${query({ q, exclude: exclude ?? '' })}`),
  create: (input: MemberInput) => call<{ id: string }>('POST', '/api/members', input),
  update: (id: string, input: MemberInput) => call<MemberEdit>('PUT', at(id), input),
  remove: (id: string) => call<void>('DELETE', at(id)),
  addInstrument: (id: string, instrument: string) => call<MemberInstrument[]>('POST', `${at(id)}/instruments`, { instrument, primary: false }),
  removeInstrument: (id: string, instrumentId: number) => call<MemberInstrument[]>('DELETE', `${at(id)}/instruments/${instrumentId}`),
  primaryInstrument: (id: string, instrumentId: number) => call<MemberInstrument[]>('PUT', `${at(id)}/instruments/${instrumentId}/primary`),
  nickname: (id: string, nickname: string) => call<void>('PUT', `${at(id)}/nickname`, { nickname }),
  expel: (id: string) => call<void>('POST', `${at(id)}/expel`),
  reactivate: (id: string) => call<void>('POST', `${at(id)}/reactivate`),
  activate: (id: string) => call<void>('POST', `${at(id)}/activate`),
  reminder: (id: string) => call<void>('POST', `${at(id)}/reminder`),
};

export const membersApi = {
  directory: (f: DirectoryFilters) => call<Directory>('GET', `/api/members${query(f)}`),
  member: (id: string) => call<MemberDetail>('GET', `/api/members/${encodeURIComponent(id)}`),
  active: (status: string, q: string) => call<ActiveMember[]>('GET', `/api/members/active${query({ status, q })}`),
  birthdays: (q: string) => call<Birthday[]>('GET', `/api/members/birthdays${query({ q })}`),
  hierarchy: () => call<TreeNode[]>('GET', '/api/members/hierarchy'),
};

export const DEFAULT_AVATAR = '/images/default-avatar.webp';

/** "12 out 2025", as the old page's "dd MMM yyyy" in pt-PT. */
export const shortDate = (iso: string | null) => {
  if (!iso) return null;
  const d = new Date(iso);
  const month = d.toLocaleDateString('pt-PT', { month: 'short' }).replace('.', '');
  return `${String(d.getDate()).padStart(2, '0')} ${month} ${d.getFullYear()}`;
};
