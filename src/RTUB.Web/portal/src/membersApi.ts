import { call } from './eventsApi';

// /api/members (Endpoints/MemberEndpoints.cs, React track 017). Signed-in members only; read-only.

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
export type Directory = { members: MemberCard[]; leitoes: MemberCard[]; instruments: Option[]; canManage: boolean };
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
