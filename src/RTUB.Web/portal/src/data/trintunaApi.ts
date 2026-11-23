// Trintuna types and API client (/api/trintuna, Endpoints/TrintunaEndpoints.cs).
import { call, outcomeText, type Outcome } from './api';
import type { MeetingPoll } from './meetingsTypes';

// The Owner and the members on the access list only; the server decides every rule (TrintunaService). Hiding a
// button here is only a convenience.

/** `key`: opaque per member (not the user id), unique even when names repeat. */
export type Person = { key: string; displayName: string; avatarUrl: string | null };
/** `poll` is the /meetings poll shape (PollCard); each option also carries its `date` ("yyyy-MM-dd"). */
export type Gathering = {
  id: number;
  number: number;
  title: string;
  place: string;
  description: string | null;
  date: string | null;
  organizer: Person | null;
  /** Owner only (the edit form). */
  organizerUserId: string | null;
  canEdit: boolean;
  past: boolean;
  goingCount: number;
  /** Who answered Vou / Não vou, by name (no user ids). */
  going: Person[];
  notGoing: Person[];
  myStatus: 'going' | 'notGoing' | null;
  poll: Omit<MeetingPoll, 'options'> & { options: { id: number; text: string; date: string; votes: number }[] };
};
/** One Hall of Fame row: `key` opaque per member (not the user id); dense `place` (ties share it, by name; 0 included); `count` = past gatherings answered Vou. */
export type Rank = { key: string; place: number; displayName: string; avatarUrl: string | null; count: number };
export type Board = { canManage: boolean; upcoming: Gathering[]; past: Gathering[]; hallOfFame: Rank[] };
export type Member = { id: string; displayName: string; avatarUrl: string | null };
export type GatheringInput = { place: string; description: string; organizerUserId: string | null; pollDates: string[] };

const base = '/api/trintuna';
const one = (id: number) => `${base}/gatherings/${id}`;

export const trintunaApi = {
  board: () => call<Board>('GET', base),
  create: (input: GatheringInput) => call<Gathering>('POST', `${base}/gatherings`, input),
  update: (id: number, input: GatheringInput) => call<Gathering>('PUT', one(id), input),
  remove: (id: number) => call<void>('DELETE', one(id)),
  settle: (id: number, optionId: number) => call<Gathering>('POST', `${one(id)}/settle`, { optionId }),
  vote: (id: number, optionId: number) => call<Gathering>('PUT', `${one(id)}/vote`, { optionId }),
  answer: (id: number, willAttend: boolean) => call<Gathering>('PUT', `${one(id)}/presence`, { willAttend }),
  withdraw: (id: number) => call<void>('DELETE', `${one(id)}/presence`),
  /** The Owner(s) and the access list, for the Organizador dropdown (Owner only). */
  organizers: () => call<Member[]>('GET', `${base}/organizers`),
  access: () => call<Member[]>('GET', `${base}/access`),
  setAccess: (userIds: string[]) => call<Member[]>('PUT', `${base}/access`, { userIds }),
};

export const MAX_PLACE = 200;
export const MAX_DESCRIPTION = 2000;
export const MAX_POLL_DATES = 12;

export const problem = (o: Outcome<unknown>, what: string) =>
  outcomeText(o, what, {
    forbidden: 'Não tem permissão para esta ação.',
    notfound: 'Este almoço já não existe.',
    closed: 'Este almoço já não aceita esta alteração.',
    conflict: 'Outro almoço foi criado ao mesmo tempo. Tente outra vez.',
  });
