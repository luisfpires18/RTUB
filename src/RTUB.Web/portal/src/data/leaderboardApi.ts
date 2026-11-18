// Leaderboard types, API client and small helpers.
import { call, type Outcome, queryString, outcomeText } from './api';
import type { Option } from './sharedTypes';

// /api/leaderboard (Endpoints/LeaderboardEndpoints.cs). Signed-in members only; the server decides
// every rule (LeaderboardService). Hiding a button here is only a convenience.

export type Entry = {
  position: number;
  id: string;
  displayName: string;
  fullName: string | null;
  avatarUrl: string | null;
  level: number;
  rankName: string;
  xp: number;
  rehearsals: number;
  events: number;
};
export type Level = { level: number; name: string; xpThreshold: number };
export type Story = { title: string; content: string; isActive: boolean };
export type Table = { fiscalYears: Option[]; fiscalYear: string; total: number; entries: Entry[]; levels: Level[]; story: Story | null; me: Entry | null };
type Progress = { level: number; rankName: string; xp: number; xpToNextLevel: number; progressPercentage: number; isMaxLevel: boolean; nextRankName: string | null };
type Breakdown = {
  totalXp: number;
  rehearsalCount: number;
  rehearsalXpPerUnit: number;
  rehearsalXpTotal: number;
  eventsByType: { type: string; count: number; xpPerUnit: number; totalXp: number }[];
};
type Activity = { date: string; name: string; type: string; xpEarned: number; isRehearsal: boolean };
export type MemberDetail = { id: string; displayName: string; fullName: string | null; avatarUrl: string | null; progress: Progress; breakdown: Breakdown; activities: Activity[] };
export type Comment = {
  id: number;
  authorName: string;
  authorAvatarUrl: string | null;
  text: string;
  createdAt: string;
  likes: number;
  likedByMe: boolean;
  canDelete: boolean;
  likedBy: string[];
};

const member = (id: string) => `/api/leaderboard/members/${encodeURIComponent(id)}`;

export const api = {
  table: (fiscalYear: string, q: string) => call<Table>('GET', `/api/leaderboard${queryString({ fiscalYear, q })}`),
  member: (id: string, fiscalYear: string) => call<MemberDetail>('GET', `${member(id)}${queryString({ fiscalYear })}`),
  comments: (id: string) => call<Comment[]>('GET', `${member(id)}/comments`),
  comment: (id: string, text: string) => call<Comment[]>('POST', `${member(id)}/comments`, { text }),
  like: (commentId: number) => call<{ liked: boolean }>('POST', `/api/leaderboard/comments/${commentId}/like`),
  remove: (commentId: number) => call<void>('DELETE', `/api/leaderboard/comments/${commentId}`),
};

export const MAX_COMMENT = 1000;
export const PAGE = 20;
export const medal = (position: number) => ['🥇', '🥈', '🥉'][position - 1] ?? String(position);

export const problem = (o: Outcome<unknown>, what: string) =>
  outcomeText(o, what, { forbidden: 'Não tem permissão para esta ação.', notfound: 'Já não existe.' });

/** "agora", minutes, hours, days, then the date. The server's times are UTC. */
export function ago(iso: string, now = Date.now()) {
  const date = new Date(/[zZ]|[+-]\d\d:?\d\d$/.test(iso) ? iso : `${iso}Z`);
  const minutes = (now - date.getTime()) / 60000;
  if (minutes < 1) return 'agora';
  if (minutes < 60) return `${Math.floor(minutes)}m`;
  if (minutes < 60 * 24) return `${Math.floor(minutes / 60)}h`;
  if (minutes < 60 * 24 * 7) return `${Math.floor(minutes / 1440)}d`;
  return date.toLocaleDateString('pt-PT', { day: '2-digit', month: '2-digit', year: 'numeric' });
}
